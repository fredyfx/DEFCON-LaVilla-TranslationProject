#!/usr/bin/env python3
"""
Automatic Video Downloader & VTT Pipeline

Downloads videos, extracts audio, generates VTT via WhisperX,
and submits cues to backend API with resume capability.
"""

import json
import os
import sys
import time
import logging
from datetime import datetime
from dataclasses import dataclass
from typing import List, Dict, Tuple, Optional
from urllib.parse import urlparse

import requests
import ffmpeg
import whisperx
import gc

logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(levelname)s - %(message)s'
)
logger = logging.getLogger(__name__)


@dataclass
class VttCue:
    """Represents a single VTT cue."""
    start_time: str
    end_time: str
    text: str
    sequence_order: int


class ConfigManager:
    """Load and validate JSON config."""

    def __init__(self, config_path: str = "config.json"):
        self.config_path = config_path
        self.config = self._load_config()
        self._validate_config()
        self._ensure_directories()

    def _load_config(self) -> dict:
        if not os.path.exists(self.config_path):
            raise FileNotFoundError(
                f"Config file not found: {self.config_path}\n"
                f"Copy config.json.example to config.json and fill in your API key."
            )
        with open(self.config_path, 'r') as f:
            return json.load(f)

    def _validate_config(self):
        required = ['api_key', 'base_url', 'download_dir', 'audio_dir', 'vtt_dir', 'progress_file']
        for key in required:
            if key not in self.config:
                raise ValueError(f"Missing required config key: {key}")
        if self.config['api_key'] == 'your-api-key-here':
            raise ValueError("Please set a valid API key in config.json")

    def _ensure_directories(self):
        for dir_key in ['download_dir', 'audio_dir', 'vtt_dir']:
            os.makedirs(self.config[dir_key], exist_ok=True)

    def get(self, key: str, default=None):
        return self.config.get(key, default)

    @property
    def whisperx_config(self) -> dict:
        return self.config.get('whisperx', {
            'model': 'medium',
            'device': 'cuda',
            'compute_type': 'float16',
            'batch_size': 16
        })


class ProgressTracker:
    """Track processed IDs with save/load capability."""

    def __init__(self, progress_file: str):
        self.progress_file = progress_file
        self.data = self._load()

    def _load(self) -> dict:
        if os.path.exists(self.progress_file):
            with open(self.progress_file, 'r') as f:
                return json.load(f)
        return {
            'processed_ids': [],
            'failed_ids': {},
            'last_run': None
        }

    def _save(self):
        self.data['last_run'] = datetime.utcnow().isoformat() + 'Z'
        with open(self.progress_file, 'w') as f:
            json.dump(self.data, f, indent=2)

    def is_processed(self, item_id: int) -> bool:
        return item_id in self.data['processed_ids']

    def mark_processed(self, item_id: int):
        if item_id not in self.data['processed_ids']:
            self.data['processed_ids'].append(item_id)
        if str(item_id) in self.data['failed_ids']:
            del self.data['failed_ids'][str(item_id)]
        self._save()

    def mark_failed(self, item_id: int, reason: str):
        self.data['failed_ids'][str(item_id)] = reason
        self._save()

    def get_stats(self) -> Tuple[int, int]:
        return len(self.data['processed_ids']), len(self.data['failed_ids'])


class VideoDownloader:
    """Download videos via requests with retry logic."""

    MAX_RETRIES = 3
    BACKOFF_FACTOR = 2

    def __init__(self, download_dir: str):
        self.download_dir = download_dir

    def download(self, url: str, item_id: int) -> str:
        """Download video, return local path."""
        filename = self._extract_filename(url, item_id)
        output_path = os.path.join(self.download_dir, filename)

        if os.path.exists(output_path):
            logger.info(f"Video already exists: {output_path}")
            return output_path

        for attempt in range(self.MAX_RETRIES):
            try:
                logger.info(f"Downloading {url} (attempt {attempt + 1})")
                response = requests.get(url, stream=True, timeout=300)
                response.raise_for_status()

                with open(output_path, 'wb') as f:
                    for chunk in response.iter_content(chunk_size=8192):
                        f.write(chunk)

                logger.info(f"Downloaded: {output_path}")
                return output_path

            except requests.RequestException as e:
                logger.warning(f"Download attempt {attempt + 1} failed: {e}")
                if attempt < self.MAX_RETRIES - 1:
                    sleep_time = self.BACKOFF_FACTOR ** attempt
                    time.sleep(sleep_time)
                else:
                    raise RuntimeError(f"Download failed after {self.MAX_RETRIES} attempts: {e}")

        return output_path

    def _extract_filename(self, url: str, item_id: int) -> str:
        parsed = urlparse(url)
        path = parsed.path
        if path:
            basename = os.path.basename(path)
            if basename:
                return f"{item_id}_{basename}"
        return f"{item_id}_video.mp4"


class AudioExtractor:
    """FFmpeg wrapper for WAV extraction."""

    def __init__(self, audio_dir: str):
        self.audio_dir = audio_dir

    def extract(self, video_path: str, item_id: int) -> str:
        """Extract audio to WAV, return audio path."""
        output_path = os.path.join(self.audio_dir, f"{item_id}.wav")

        if os.path.exists(output_path):
            logger.info(f"Audio already exists: {output_path}")
            return output_path

        try:
            logger.info(f"Extracting audio from {video_path}")
            ffmpeg.input(video_path).output(
                output_path,
                format='wav',
                acodec='pcm_s16le',
                ac=1,
                ar='16000'
            ).overwrite_output().run(quiet=True)

            logger.info(f"Audio extracted: {output_path}")
            return output_path

        except ffmpeg.Error as e:
            raise RuntimeError(f"FFmpeg extraction failed: {e}")


class WhisperXProcessor:
    """Generate VTT cues from audio using WhisperX."""

    def __init__(self, config: dict, vtt_dir: str):
        self.model_name = config.get('model', 'medium')
        self.device = config.get('device', 'cuda')
        self.compute_type = config.get('compute_type', 'float16')
        self.batch_size = config.get('batch_size', 16)
        self.vtt_dir = vtt_dir
        self.model = None

    def _load_model(self):
        if self.model is None:
            logger.info(f"Loading WhisperX model: {self.model_name}")
            self.model = whisperx.load_model(
                self.model_name,
                self.device,
                compute_type=self.compute_type
            )

    def process(self, audio_path: str, item_id: int) -> List[VttCue]:
        """Generate VTT cues from audio file."""
        self._load_model()

        logger.info(f"Transcribing: {audio_path}")
        start_time = time.time()

        audio = whisperx.load_audio(audio_path)
        result = self.model.transcribe(audio, batch_size=self.batch_size)

        # Align whisper output
        model_a, metadata = whisperx.load_align_model(
            language_code=result["language"],
            device=self.device
        )
        result = whisperx.align(
            result["segments"],
            model_a,
            metadata,
            audio,
            self.device,
            return_char_alignments=False
        )

        # Clean up alignment model
        del model_a
        gc.collect()

        elapsed = time.time() - start_time
        logger.info(f"Transcription completed in {elapsed:.2f}s")

        # Convert to VttCue objects
        cues = []
        for i, segment in enumerate(result["segments"]):
            cue = VttCue(
                start_time=self._format_time(segment["start"]),
                end_time=self._format_time(segment["end"]),
                text=segment["text"].strip(),
                sequence_order=i + 1
            )
            cues.append(cue)

        # Save local VTT file for reference
        self._save_vtt(cues, item_id)

        return cues

    def _format_time(self, seconds: float) -> str:
        """Convert seconds to VTT time format (HH:MM:SS.mmm)."""
        hours = int(seconds // 3600)
        minutes = int((seconds % 3600) // 60)
        secs = seconds % 60
        return f"{hours:02d}:{minutes:02d}:{secs:06.3f}"

    def _save_vtt(self, cues: List[VttCue], item_id: int):
        """Save VTT file locally for reference."""
        output_path = os.path.join(self.vtt_dir, f"{item_id}.vtt")
        with open(output_path, 'w', encoding='utf-8') as f:
            f.write("WEBVTT\n\n")
            for cue in cues:
                f.write(f"{cue.start_time} --> {cue.end_time}\n")
                f.write(f"{cue.text}\n\n")
        logger.info(f"VTT saved: {output_path}")


class BackendClient:
    """API client for submitting VTT cues to backend."""

    MAX_RETRIES = 3
    BACKOFF_FACTOR = 2

    def __init__(self, base_url: str, api_key: str):
        self.base_url = base_url.rstrip('/')
        self.headers = {
            'X-API-Key': api_key,
            'Content-Type': 'application/json'
        }

    def start_vtt(self, item_id: int, filename: str, language: str = "en") -> bool:
        """Start VTT build, mark as 'In Progress'."""
        url = f"{self.base_url}/api/vttfile/start"
        payload = {
            "FileName": filename,
            "Id": item_id,
            "Language": language
        }

        try:
            response = requests.post(url, json=payload, headers=self.headers, timeout=30)
            response.raise_for_status()
            logger.info(f"Started VTT build for ID {item_id}")
            return True
        except requests.RequestException as e:
            logger.error(f"Failed to start VTT: {e}")
            return False

    def add_cue(self, item_id: int, cue: VttCue) -> bool:
        """Add individual cue to VTT file with retry logic."""
        url = f"{self.base_url}/api/vttcue"
        payload = {
            "VttFileId": item_id,
            "StartTime": cue.start_time,
            "EndTime": cue.end_time,
            "Text": cue.text,
            "SequenceOrder": cue.sequence_order
        }

        for attempt in range(self.MAX_RETRIES):
            try:
                response = requests.post(url, json=payload, headers=self.headers, timeout=30)
                response.raise_for_status()
                return True
            except requests.RequestException as e:
                logger.warning(f"Cue {cue.sequence_order} attempt {attempt + 1} failed: {e}")
                if attempt < self.MAX_RETRIES - 1:
                    sleep_time = self.BACKOFF_FACTOR ** attempt
                    time.sleep(sleep_time)

        logger.error(f"Cue {cue.sequence_order} failed after {self.MAX_RETRIES} attempts")
        return False

    def complete_vtt(self, item_id: int) -> bool:
        """Mark VTT as 'Completed'."""
        url = f"{self.base_url}/api/vttfile/completed/{item_id}"

        try:
            response = requests.post(url, headers=self.headers, timeout=30)
            response.raise_for_status()
            logger.info(f"Completed VTT for ID {item_id}")
            return True
        except requests.RequestException as e:
            logger.error(f"Failed to complete VTT: {e}")
            return False

    def submit_cues(self, item_id: int, cues: List[VttCue], filename: str) -> Tuple[bool, List[int]]:
        """Submit all cues for an item.

        Returns:
            Tuple of (success, failed_cue_sequences)
        """
        # Start VTT build
        if not self.start_vtt(item_id, filename):
            return False, []

        # Add all cues, track failures
        failed_cues = []
        for cue in cues:
            if not self.add_cue(item_id, cue):
                failed_cues.append(cue.sequence_order)

        # If any cue failed after retries, don't mark complete
        if failed_cues:
            logger.error(f"ID {item_id}: {len(failed_cues)} cues failed: {failed_cues}")
            return False, failed_cues

        # All cues succeeded, mark complete
        if not self.complete_vtt(item_id):
            return False, []

        return True, []


def parse_input_file(filepath: str) -> List[Tuple[int, str]]:
    """Parse input file with Id + URL format.

    Expected format (one per line):
        12345 https://example.com/video.mp4
    or
        12345,https://example.com/video.mp4
    """
    items = []

    with open(filepath, 'r') as f:
        for line_num, line in enumerate(f, 1):
            line = line.strip()
            if not line or line.startswith('#'):
                continue

            # Try space-separated first, then comma
            parts = line.split(None, 1)
            if len(parts) != 2:
                parts = line.split(',', 1)

            if len(parts) != 2:
                logger.warning(f"Line {line_num}: Invalid format, skipping: {line}")
                continue

            try:
                item_id = int(parts[0].strip())
                url = parts[1].strip()
                items.append((item_id, url))
            except ValueError:
                logger.warning(f"Line {line_num}: Invalid ID, skipping: {line}")

    logger.info(f"Parsed {len(items)} items from {filepath}")
    return items


def cleanup_temp_files(video_path: str, audio_path: str, keep_audio: bool = False):
    """Remove temporary files after successful processing."""
    try:
        if os.path.exists(video_path):
            os.remove(video_path)
            logger.debug(f"Removed: {video_path}")
        if not keep_audio and os.path.exists(audio_path):
            os.remove(audio_path)
            logger.debug(f"Removed: {audio_path}")
    except OSError as e:
        logger.warning(f"Cleanup failed: {e}")


def main():
    if len(sys.argv) < 2:
        print("Usage: python 09_Auto_Pipeline.py <input_file> [--config <config.json>]")
        print("\nInput file format (one per line):")
        print("  12345 https://example.com/video.mp4")
        sys.exit(1)

    input_file = sys.argv[1]
    config_path = "config.json"

    # Parse optional --config argument
    if "--config" in sys.argv:
        idx = sys.argv.index("--config")
        if idx + 1 < len(sys.argv):
            config_path = sys.argv[idx + 1]

    # Initialize components
    try:
        config = ConfigManager(config_path)
    except (FileNotFoundError, ValueError) as e:
        logger.error(str(e))
        sys.exit(1)

    progress = ProgressTracker(config.get('progress_file'))
    downloader = VideoDownloader(config.get('download_dir'))
    extractor = AudioExtractor(config.get('audio_dir'))
    whisperx_proc = WhisperXProcessor(config.whisperx_config, config.get('vtt_dir'))
    backend = BackendClient(config.get('base_url'), config.get('api_key'))

    # Parse input
    items = parse_input_file(input_file)
    if not items:
        logger.error("No valid items found in input file")
        sys.exit(1)

    # Process each item
    processed, skipped, failed = 0, 0, 0

    for item_id, url in items:
        if progress.is_processed(item_id):
            logger.info(f"Skipping {item_id}: already processed")
            skipped += 1
            continue

        logger.info(f"Processing ID {item_id}: {url}")
        video_path = None
        audio_path = None

        try:
            # Download
            video_path = downloader.download(url, item_id)

            # Extract audio
            audio_path = extractor.extract(video_path, item_id)

            # Generate VTT
            cues = whisperx_proc.process(audio_path, item_id)

            # Submit to backend
            filename = os.path.basename(video_path)
            success, failed_cues = backend.submit_cues(item_id, cues, filename)

            if success:
                progress.mark_processed(item_id)
                processed += 1

                # Cleanup temp files
                cleanup_temp_files(video_path, audio_path)
            else:
                if failed_cues:
                    reason = f"Cues failed after retries: {failed_cues}"
                else:
                    reason = "Backend submission failed"
                progress.mark_failed(item_id, reason)
                failed += 1

        except Exception as e:
            logger.error(f"Failed to process {item_id}: {e}")
            progress.mark_failed(item_id, str(e))
            failed += 1

    # Summary
    total_processed, total_failed = progress.get_stats()
    logger.info(f"\n=== Pipeline Complete ===")
    logger.info(f"This run: {processed} processed, {skipped} skipped, {failed} failed")
    logger.info(f"Total: {total_processed} processed, {total_failed} failed")


if __name__ == "__main__":
    main()
