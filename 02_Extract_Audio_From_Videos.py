import ffmpeg
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
    
    if not os.path.exists(output_audio_file_wav):        
        try:
            # Extract audio using ffmpeg-python
            ffmpeg.input(input_file).output(output_audio_file_wav, format='wav').run()
            # Reporting to console
            print(f'Audio extracted from FileId: {id}')
        except:
            print(f"Error on processing videoId: {id}")
