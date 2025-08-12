import sqlite3
import os

# Replace 'your_database.db' with your SQLite database file name
database_file = 'file_data.db'

# Establish a connection to the SQLite database
connection = sqlite3.connect(database_file)
cursor = connection.cursor()

cursor.execute("SELECT * FROM files WHERE full_path LIKE '%DEFCON%'")
matching_files = cursor.fetchall()

row_count = len(matching_files) + 0

# Close the database connection
connection.close()

files_processed = 0

files = []
for row in matching_files:
    id, full_path, filename, extension, for_processing = row
    input_file = full_path
    # print(f"Processing file: {full_path}")
    current_folder = os.path.dirname(full_path)
    # print(current_folder)
    filename = os.path.basename(full_path)
    # print(filename)
    file_name_without_extension = os.path.splitext(os.path.basename(filename))[0]
    
    files_processed = files_processed + 1
    print(f'Files Processed: {files_processed}/{row_count}')
    print("\n")
    output_audio_file_wav = os.path.join(current_folder, file_name_without_extension +'.wav')
    output_vtt_file = os.path.join(current_folder, file_name_without_extension + '.vtt')
    
    if os.path.exists(output_vtt_file):        
        try:
            # Add to the file list
            files.append(output_vtt_file)
            
            # Reporting to console
            print(f'File Identified: {id}')
        except:
            print(f"Error on processing videoId: {id}")



def has_long_subtitles(vtt_file, max_chars=120):
    """Returns True if any subtitle content > max_chars"""
    with open(vtt_file, 'r', encoding='utf-8') as f:
        content = f.read()
    
    # Extract all subtitle text (everything after timestamp lines)
    import re
    blocks = re.split(r'\n\s*\n', content)
    
    for block in blocks:
        lines = block.strip().split('\n')
        # Find content after timestamp
        for i, line in enumerate(lines):
            if '-->' in line and i + 1 < len(lines):
                subtitle_text = ' '.join(lines[i+1:]).strip()
                if len(subtitle_text) > max_chars:
                    return True
    return False

            
for file in files:
    print(f"{file}: {'🚩 TOO LONG' if has_long_subtitles(file) else '✅ OK'}")