
import whisperx
import gc
import sqlite3
import os
import time

database_file = 'file_data.db'

connection = sqlite3.connect(database_file)
cursor = connection.cursor()
cursor.execute("SELECT * FROM files WHERE full_path LIKE '%DEFCON%' AND for_processing=1")
rows = cursor.fetchall()

row_count = len(rows) + 0

device = "cuda"
model = whisperx.load_model("medium", device, compute_type="float16")
print('ready for processing')
for row in rows:
    start = time.time()
    id, full_path, filename, extension, for_processing = row
    filename = os.path.basename(full_path)
    current_folder = os.path.dirname(full_path)
    file_name_without_extension = os.path.splitext(os.path.basename(filename))[0]
    audio_file = os.path.join(current_folder, file_name_without_extension +'.wav')
    output_vtt_file = os.path.join(current_folder, file_name_without_extension + '.vtt')
    output_text_file = os.path.join(current_folder, file_name_without_extension + '.txt')
    
    if os.path.exists(output_vtt_file):
        print(f"skipping {audio_file } - already exists.")
        continue
    
    if os.path.exists(audio_file):
        # Load audio        
        batch_size = 16

        # 1. Transcribe with original whisper (batched)        
        audio = whisperx.load_audio(audio_file)
        result = model.transcribe(audio, batch_size=batch_size)

        # 2. Align whisper output
        model_a, metadata = whisperx.load_align_model(language_code=result["language"], device=device)
        result = whisperx.align(result["segments"], model_a, metadata, audio, device, return_char_alignments=False)

        # 3. Generate VTT
        def generate_vtt(result, output_file):
            with open(output_file, 'w', encoding='utf-8') as f:
                f.write("WEBVTT\n\n")
                
                for i, segment in enumerate(result["segments"]):
                    start_time = format_time(segment["start"])
                    end_time = format_time(segment["end"])
                    text = segment["text"].strip()                    
                    
                    f.write(f"{start_time} --> {end_time}\n")
                    f.write(f"{text}\n\n")

        # 4. Generate Text File
        def generate_text(result, output_file):
            with open(output_file, 'w', encoding='utf-8') as f:
                for segment in result["segments"]:
                    text = segment["text"].strip()
                    f.write(f"{text}\n")

        def format_time(seconds):
            """Convert seconds to VTT time format (HH:MM:SS.mmm)"""
            hours = int(seconds // 3600)
            minutes = int((seconds % 3600) // 60)
            seconds = seconds % 60
            return f"{hours:02d}:{minutes:02d}:{seconds:06.3f}"

        # Generate the VTT file        
        generate_vtt(result, output_vtt_file)
        
        # Generate the Text file
        generate_text(result, output_text_file)
        end = time.time()
        #print(f"\n Transcription time = {(end - start) / 60:.2f} minutes")
        print(f"\n Transcription time = {(end - start):.2f} seconds")
connection.close()