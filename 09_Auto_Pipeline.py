#!/usr/bin/env python3
"""
Automatic Media Processing Pipeline

Processes videos and documents:
- Videos: Download → Audio extraction → WhisperX transcription → Translation → Summary
- Documents (PDF, DOCX, etc.): Download → MarkItDown extraction → Summary + Keywords

Submits results to backend API with resume capability.
"""

import json
import os
import sys
import time
import logging
import subprocess
from datetime import datetime
from dataclasses import dataclass
from typing import List, Dict, Tuple, Optional
from urllib.parse import urlparse

import requests
import ffmpeg
import whisperx
import gc
from markitdown import MarkItDown

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

    @property
    def ollama_config(self) -> dict:
        return self.config.get('ollama', {
            'enabled': True,
            'model': 'qwen2.5:7b',
            'host': 'http://localhost:11434',
            'target_languages': ['es', 'pt'],
            'batch_size': 5,
            'timeout': 120
        })

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


class OllamaTranslator:
    """Translate VTT cues using Ollama local LLM."""

    LANGUAGE_NAMES = {
        'es': 'Spanish',
        'pt': 'Portuguese'
    }

    def __init__(self, config: dict, vtt_dir: str):
        self.enabled = config.get('enabled', True)
        self.model = config.get('model', 'qwen2.5:7b')
        self.host = config.get('host', 'http://localhost:11434')
        self.target_languages = config.get('target_languages', ['es', 'pt'])
        self.batch_size = config.get('batch_size', 5)
        self.timeout = config.get('timeout', 120)
        self.vtt_dir = vtt_dir

    def ensure_ollama_running(self) -> bool:
        """Check if Ollama is running, attempt to start if not."""
        try:
            response = requests.get(f"{self.host}/api/tags", timeout=5)
            if response.status_code == 200:
                logger.info("Ollama is running")
                return True
        except requests.RequestException:
            pass

        # Try to start Ollama
        logger.info("Starting Ollama...")
        try:
            subprocess.Popen(
                ['ollama', 'serve'],
                stdout=subprocess.DEVNULL,
                stderr=subprocess.DEVNULL
            )
            time.sleep(3)

            # Check again
            response = requests.get(f"{self.host}/api/tags", timeout=5)
            if response.status_code == 200:
                logger.info("Ollama started successfully")
                return True
        except Exception as e:
            logger.error(f"Failed to start Ollama: {e}")

        return False

    def ensure_model_available(self) -> bool:
        """Check if model is available, pull if not."""
        try:
            response = requests.get(f"{self.host}/api/tags", timeout=10)
            if response.status_code == 200:
                models = response.json().get('models', [])
                model_names = [m.get('name', '') for m in models]

                # Check for exact match or partial match
                if any(self.model in name or name in self.model for name in model_names):
                    logger.info(f"Model {self.model} is available")
                    return True

                # Pull the model
                logger.info(f"Pulling model {self.model}...")
                pull_response = requests.post(
                    f"{self.host}/api/pull",
                    json={"name": self.model},
                    timeout=600,
                    stream=True
                )

                for line in pull_response.iter_lines():
                    if line:
                        data = json.loads(line)
                        status = data.get('status', '')
                        if 'pulling' in status or 'downloading' in status:
                            logger.info(f"  {status}")

                logger.info(f"Model {self.model} pulled successfully")
                return True

        except Exception as e:
            logger.error(f"Failed to ensure model availability: {e}")

        return False

    def translate_cue(self, text: str, target_lang: str) -> Optional[str]:
        """Translate a single cue text."""
        lang_name = self.LANGUAGE_NAMES.get(target_lang, target_lang)

        prompt = f"""Translate the following subtitle text from English to {lang_name}.
Keep the translation natural and conversational, suitable for subtitles.
Only output the translation, nothing else.

Text to translate:
{text}

Translation:"""

        try:
            response = requests.post(
                f"{self.host}/api/generate",
                json={
                    "model": self.model,
                    "prompt": prompt,
                    "stream": False,
                    "options": {
                        "temperature": 0.3,
                        "num_predict": 256
                    }
                },
                timeout=self.timeout
            )

            if response.status_code == 200:
                result = response.json()
                translation = result.get('response', '').strip()
                return translation if translation else None

        except Exception as e:
            logger.warning(f"Translation failed: {e}")

        return None

    def translate_batch(self, cues: List[VttCue], target_lang: str) -> List[Tuple[VttCue, str]]:
        """Translate a batch of cues, return list of (source_cue, translated_text)."""
        results = []

        for cue in cues:
            translation = self.translate_cue(cue.text, target_lang)
            if translation:
                results.append((cue, translation))
            else:
                logger.warning(f"Cue {cue.sequence_order} translation failed, skipping")

        return results

    def translate_all(self, cues: List[VttCue], target_lang: str) -> Dict[int, str]:
        """Translate all cues, return dict of {sequence_order: translated_text}."""
        if not self.enabled:
            logger.info("Ollama translation disabled")
            return {}

        lang_name = self.LANGUAGE_NAMES.get(target_lang, target_lang)
        logger.info(f"Translating {len(cues)} cues to {lang_name}")

        translations = {}
        total = len(cues)

        for i in range(0, total, self.batch_size):
            batch = cues[i:i + self.batch_size]
            batch_num = (i // self.batch_size) + 1
            total_batches = (total + self.batch_size - 1) // self.batch_size

            logger.info(f"Translating batch {batch_num}/{total_batches}")

            for cue in batch:
                translation = self.translate_cue(cue.text, target_lang)
                if translation:
                    translations[cue.sequence_order] = translation

            # Small delay between batches to avoid overloading
            if i + self.batch_size < total:
                time.sleep(0.5)

        logger.info(f"Translated {len(translations)}/{total} cues to {lang_name}")
        return translations

    def save_translated_vtt(
        self,
        source_cues: List[VttCue],
        translations: Dict[int, str],
        item_id: int,
        target_lang: str
    ):
        """Save translated VTT file locally."""
        output_path = os.path.join(self.vtt_dir, f"{item_id}_{target_lang}.vtt")

        with open(output_path, 'w', encoding='utf-8') as f:
            f.write("WEBVTT\n\n")
            for cue in source_cues:
                if cue.sequence_order in translations:
                    f.write(f"{cue.start_time} --> {cue.end_time}\n")
                    f.write(f"{translations[cue.sequence_order]}\n\n")

        logger.info(f"Translated VTT saved: {output_path}")
        return output_path


class OllamaSummarizer:
    """Generate summaries from text content using Ollama local LLM."""

    def __init__(self, config: dict):
        self.enabled = config.get('summarizer_enabled', True)
        self.model = config.get('model', 'qwen2.5:7b')
        self.host = config.get('host', 'http://localhost:11434')
        self.timeout = config.get('summary_timeout', 180)
        self.max_cues = config.get('max_cues_for_summary', 500)
        self.max_chars = config.get('max_chars_for_summary', 50000)

    def generate_summary(self, cues: List[VttCue], title: str = "") -> Optional[dict]:
        """Generate structured summary from VTT cues.

        Returns dict: {'short_summary', 'key_topics', 'keywords', 'full_summary'} or None
        """
        if not self.enabled:
            return None

        transcript = " ".join(c.text for c in cues[:self.max_cues])
        return self.generate_summary_from_text(transcript, title, content_type="video transcript")

    def generate_summary_from_text(self, text: str, title: str = "", content_type: str = "document") -> Optional[dict]:
        """Generate structured summary from raw text.

        Args:
            text: Raw text content to summarize
            title: Optional title for context
            content_type: Type of content (e.g., "video transcript", "PDF document")

        Returns dict: {'short_summary', 'key_topics', 'keywords', 'full_summary'} or None
        """
        if not self.enabled:
            return None

        # Truncate text if too long
        truncated_text = text[:self.max_chars]
        prompt = self._build_prompt(truncated_text, title, content_type)

        try:
            response = requests.post(
                f"{self.host}/api/generate",
                json={
                    "model": self.model,
                    "prompt": prompt,
                    "stream": False,
                    "options": {"temperature": 0.3, "num_predict": 1024}
                },
                timeout=self.timeout
            )
            if response.status_code == 200:
                return self._parse_response(response.json().get('response', ''))
        except Exception as e:
            logger.error(f"Summary generation failed: {e}")
        return None

    def _build_prompt(self, text: str, title: str, content_type: str = "document") -> str:
        title_ctx = f"Title: {title}\n\n" if title else ""
        return f"""{title_ctx}Analyze this {content_type} and provide:

1. SHORT_SUMMARY: 1-2 sentences (max 200 chars) description
2. KEY_TOPICS: 3-5 bullet points of main topics discussed
3. KEYWORDS: 5-15 single words or short phrases for search/tagging (comma-separated)
4. FULL_SUMMARY: 2-3 paragraphs detailed overview

Content:
{text}

Respond in this exact format:
SHORT_SUMMARY: <text>
KEY_TOPICS:
- <topic>
KEYWORDS: <word1>, <word2>, <word3>
FULL_SUMMARY:
<text>"""

    def _parse_response(self, raw: str) -> Optional[dict]:
        result = {'short_summary': '', 'key_topics': [], 'keywords': [], 'full_summary': ''}
        try:
            if 'SHORT_SUMMARY:' in raw:
                start = raw.index('SHORT_SUMMARY:') + 14
                end = raw.index('KEY_TOPICS:') if 'KEY_TOPICS:' in raw else len(raw)
                result['short_summary'] = raw[start:end].strip()[:500]

            if 'KEY_TOPICS:' in raw:
                start = raw.index('KEY_TOPICS:') + 11
                end = raw.index('KEYWORDS:') if 'KEYWORDS:' in raw else (raw.index('FULL_SUMMARY:') if 'FULL_SUMMARY:' in raw else len(raw))
                topics = raw[start:end].strip()
                result['key_topics'] = [l.lstrip('- ').strip() for l in topics.split('\n') if l.strip().startswith('-')][:10]

            if 'KEYWORDS:' in raw:
                start = raw.index('KEYWORDS:') + 9
                end = raw.index('FULL_SUMMARY:') if 'FULL_SUMMARY:' in raw else len(raw)
                keywords_raw = raw[start:end].strip()
                # Handle both comma-separated and newline-separated
                keywords = [k.strip().lower() for k in keywords_raw.replace('\n', ',').split(',') if k.strip()]
                result['keywords'] = keywords[:20]

            if 'FULL_SUMMARY:' in raw:
                start = raw.index('FULL_SUMMARY:') + 13
                result['full_summary'] = raw[start:].strip()[:5000]

            return result if result['short_summary'] or result['full_summary'] else None
        except Exception:
            return None


class PdfProcessor:
    """Extract text from PDF files using MarkItDown."""

    SUPPORTED_EXTENSIONS = {'.pdf', '.docx', '.doc', '.pptx', '.ppt', '.xlsx', '.xls', '.html', '.htm', '.epub'}

    def __init__(self, config: dict):
        self.md = MarkItDown()
        self.output_dir = config.get('pdf_output_dir', './pdf_extracts')
        os.makedirs(self.output_dir, exist_ok=True)

    @classmethod
    def is_supported(cls, filepath: str) -> bool:
        """Check if file type is supported for text extraction."""
        ext = os.path.splitext(filepath)[1].lower()
        return ext in cls.SUPPORTED_EXTENSIONS

    def extract_text(self, filepath: str) -> Optional[str]:
        """Extract text content from document.

        Returns extracted text or None if extraction fails.
        """
        if not os.path.exists(filepath):
            logger.error(f"File not found: {filepath}")
            return None

        try:
            logger.info(f"Extracting text from: {filepath}")
            result = self.md.convert(filepath)
            text = result.text_content

            if not text or not text.strip():
                logger.warning(f"No text extracted from: {filepath}")
                return None

            # Save extracted text for reference
            self._save_extracted(filepath, text)

            logger.info(f"Extracted {len(text)} chars from {filepath}")
            return text

        except Exception as e:
            logger.error(f"Text extraction failed for {filepath}: {e}")
            return None

    def _save_extracted(self, source_path: str, text: str):
        """Save extracted text to markdown file."""
        basename = os.path.splitext(os.path.basename(source_path))[0]
        output_path = os.path.join(self.output_dir, f"{basename}.md")
        try:
            with open(output_path, 'w', encoding='utf-8') as f:
                f.write(text)
            logger.debug(f"Saved extracted text: {output_path}")
        except Exception as e:
            logger.warning(f"Failed to save extracted text: {e}")


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

    # --- Translation Endpoints ---

    def start_translation(self, file_id: int, target_language: str) -> Optional[int]:
        """Start translation for a file, returns target VTT file ID."""
        url = f"{self.base_url}/api/translate/{file_id}/start"
        payload = {"targetLanguage": target_language}

        try:
            response = requests.post(url, json=payload, headers=self.headers, timeout=30)
            response.raise_for_status()
            data = response.json()
            target_vtt_id = data.get('targetVttFileId')
            logger.info(f"Started translation for file {file_id} -> {target_language} (VTT ID: {target_vtt_id})")
            return target_vtt_id
        except requests.RequestException as e:
            logger.error(f"Failed to start translation: {e}")
            return None

    def submit_translations_bulk(self, vtt_file_id: int, translations: List[Dict]) -> bool:
        """Submit translated cues in bulk.

        Args:
            vtt_file_id: Target VTT file ID
            translations: List of {"sourceCueId": int, "text": str}
        """
        url = f"{self.base_url}/api/translate/{vtt_file_id}/cues/bulk"
        payload = {"cues": translations}

        for attempt in range(self.MAX_RETRIES):
            try:
                response = requests.post(url, json=payload, headers=self.headers, timeout=60)
                response.raise_for_status()
                logger.info(f"Submitted {len(translations)} translations to VTT {vtt_file_id}")
                return True
            except requests.RequestException as e:
                logger.warning(f"Bulk submit attempt {attempt + 1} failed: {e}")
                if attempt < self.MAX_RETRIES - 1:
                    time.sleep(self.BACKOFF_FACTOR ** attempt)

        logger.error(f"Bulk submit failed after {self.MAX_RETRIES} attempts")
        return False

    def get_translation_progress(self, vtt_file_id: int) -> Optional[Dict]:
        """Get translation progress."""
        url = f"{self.base_url}/api/translate/{vtt_file_id}/progress"

        try:
            response = requests.get(url, headers=self.headers, timeout=30)
            response.raise_for_status()
            return response.json()
        except requests.RequestException as e:
            logger.error(f"Failed to get progress: {e}")
            return None

    def complete_translation(self, vtt_file_id: int) -> bool:
        """Mark translation as complete."""
        url = f"{self.base_url}/api/translate/{vtt_file_id}/complete"

        try:
            response = requests.post(url, headers=self.headers, timeout=30)
            response.raise_for_status()
            logger.info(f"Marked translation {vtt_file_id} as complete")
            return True
        except requests.RequestException as e:
            logger.error(f"Failed to complete translation: {e}")
            return False

    def submit_full_translation(
        self,
        file_id: int,
        source_cues: List[VttCue],
        translations: Dict[int, str],
        target_language: str
    ) -> bool:
        """Submit a full translation for a file.

        Args:
            file_id: Source file ID
            source_cues: List of source VttCue objects
            translations: Dict mapping sequence_order -> translated text
            target_language: Target language code (e.g., 'es', 'pt')

        Returns:
            True if successful
        """
        # Start translation (creates target VTT file)
        target_vtt_id = self.start_translation(file_id, target_language)
        if not target_vtt_id:
            return False

        # Build bulk payload using sequence order (pipeline doesn't have DB IDs)
        bulk_cues = []
        for cue in source_cues:
            if cue.sequence_order in translations:
                bulk_cues.append({
                    "sequenceOrder": cue.sequence_order,
                    "text": translations[cue.sequence_order]
                })

        if not bulk_cues:
            logger.warning(f"No translations to submit for file {file_id}")
            return False

        # Submit in batches of 50
        batch_size = 50
        for i in range(0, len(bulk_cues), batch_size):
            batch = bulk_cues[i:i + batch_size]
            if not self.submit_translations_bulk(target_vtt_id, batch):
                return False

        # Mark complete
        return self.complete_translation(target_vtt_id)

    def submit_summary(self, file_id: int, summary: dict) -> bool:
        """Submit generated summary to backend."""
        url = f"{self.base_url}/api/summary"
        payload = {
            "fileId": file_id,
            "shortSummary": summary.get('short_summary', ''),
            "keyTopics": summary.get('key_topics', []),
            "keywords": summary.get('keywords', []),
            "fullSummary": summary.get('full_summary', ''),
            "generatedBy": "ollama"
        }

        for attempt in range(self.MAX_RETRIES):
            try:
                response = requests.post(url, json=payload, headers=self.headers, timeout=30)
                response.raise_for_status()
                logger.info(f"Summary submitted for file {file_id}")
                return True
            except requests.RequestException as e:
                logger.warning(f"Summary submit attempt {attempt + 1} failed: {e}")
                if attempt < self.MAX_RETRIES - 1:
                    time.sleep(self.BACKOFF_FACTOR ** attempt)
        return False


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


def cleanup_temp_files(video_path: str, audio_path: str, keep_video: bool = True, keep_audio: bool = False):
    """Remove temporary files after successful processing."""
    try:
        if not keep_video and os.path.exists(video_path):
            os.remove(video_path)
            logger.debug(f"Removed: {video_path}")
        if not keep_audio and os.path.exists(audio_path):
            os.remove(audio_path)
            logger.debug(f"Removed: {audio_path}")
    except OSError as e:
        logger.warning(f"Cleanup failed: {e}")


def get_file_type(filepath: str) -> str:
    """Determine file type based on extension.

    Returns: 'video', 'document', or 'unknown'
    """
    ext = os.path.splitext(filepath)[1].lower()

    video_exts = {'.mp4', '.mkv', '.avi', '.mov', '.webm', '.flv', '.wmv', '.m4v'}
    document_exts = {'.pdf', '.docx', '.doc', '.pptx', '.ppt', '.xlsx', '.xls', '.html', '.htm', '.epub'}

    if ext in video_exts:
        return 'video'
    elif ext in document_exts:
        return 'document'
    else:
        return 'unknown'


def main():
    if len(sys.argv) < 2:
        print("Usage: python 09_Auto_Pipeline.py <input_file> [--config <config.json>]")
        print("\nInput file format (one per line):")
        print("  12345 https://example.com/video.mp4")
        print("  12346 https://example.com/document.pdf")
        print("\nSupported: .mp4, .mkv, .avi, .mov (video) | .pdf, .docx, .pptx, .xlsx (documents)")
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

    # Initialize Ollama translator and summarizer
    ollama_config = config.ollama_config
    translator = OllamaTranslator(ollama_config, config.get('vtt_dir'))
    pdf_processor = PdfProcessor(ollama_config)
    summarizer = OllamaSummarizer(ollama_config)

    # Check Ollama availability if enabled
    if ollama_config.get('enabled', True):
        if not translator.ensure_ollama_running():
            logger.warning("Ollama not available, translations will be skipped")
            ollama_config['enabled'] = False
        elif not translator.ensure_model_available():
            logger.warning(f"Model {ollama_config.get('model')} not available, translations will be skipped")
            ollama_config['enabled'] = False

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
        file_path = None
        audio_path = None

        try:
            # Download file
            file_path = downloader.download(url, item_id)
            filename = os.path.basename(file_path)
            file_type = get_file_type(file_path)

            logger.info(f"Detected file type: {file_type} for {filename}")

            if file_type == 'document':
                # === DOCUMENT PROCESSING (PDF, DOCX, etc.) ===
                text = pdf_processor.extract_text(file_path)

                if not text:
                    progress.mark_failed(item_id, "Text extraction failed")
                    failed += 1
                    continue

                # Generate summary + keywords from extracted text
                if ollama_config.get('summarizer_enabled', True):
                    try:
                        logger.info(f"Generating summary for document ID {item_id}")
                        summary = summarizer.generate_summary_from_text(
                            text, filename, content_type="PDF document"
                        )
                        if summary:
                            backend.submit_summary(item_id, summary)
                            logger.info(f"Summary submitted for document ID {item_id}")
                        else:
                            logger.warning(f"No summary generated for document ID {item_id}")
                    except Exception as e:
                        logger.error(f"Document summary generation failed: {e}")

                progress.mark_processed(item_id)
                processed += 1

            else:
                # === VIDEO PROCESSING ===
                # Extract audio
                audio_path = extractor.extract(file_path, item_id)

                # Generate VTT
                cues = whisperx_proc.process(audio_path, item_id)

                # Submit to backend
                success, failed_cues = backend.submit_cues(item_id, cues, filename)

                if success:
                    # Translation step (if Ollama enabled)
                    if translator.enabled:
                        for target_lang in translator.target_languages:
                            try:
                                logger.info(f"Translating ID {item_id} to {target_lang}")
                                translations = translator.translate_all(cues, target_lang)

                                if translations:
                                    # Save translated VTT locally
                                    translator.save_translated_vtt(cues, translations, item_id, target_lang)

                                    # Submit to backend
                                    trans_success = backend.submit_full_translation(
                                        item_id, cues, translations, target_lang
                                    )
                                    if trans_success:
                                        logger.info(f"Translation to {target_lang} complete for ID {item_id}")
                                    else:
                                        logger.warning(f"Translation submission to {target_lang} failed for ID {item_id}")
                                else:
                                    logger.warning(f"No translations generated for {target_lang}")

                            except Exception as e:
                                logger.error(f"Translation to {target_lang} failed: {e}")
                                # Continue with other languages, don't fail the whole item

                    # Summary generation (after translations)
                    if ollama_config.get('summarizer_enabled', True):
                        try:
                            logger.info(f"Generating summary for ID {item_id}")
                            summary = summarizer.generate_summary(cues, filename)
                            if summary:
                                backend.submit_summary(item_id, summary)
                                logger.info(f"Summary submitted for ID {item_id}")
                        except Exception as e:
                            logger.error(f"Summary generation failed: {e}")
                            # Don't fail whole item if summary fails

                    progress.mark_processed(item_id)
                    processed += 1

                    # Cleanup temp files (keep video by default)
                    keep_video = config.get('keep_video', True)
                    cleanup_temp_files(file_path, audio_path, keep_video=keep_video)
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
