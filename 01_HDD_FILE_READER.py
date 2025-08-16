import os
import sqlite3

# Important detail, this was the initial PoC
# The sqlite is not going to be used for long term.

# Connect to the SQLite database
conn = sqlite3.connect('file_data.db')
cursor = conn.cursor()

# Create a table if it doesn't exist
cursor.execute('''
    CREATE TABLE IF NOT EXISTS files (
        id INTEGER PRIMARY KEY,
        full_path TEXT,
        filename TEXT,
        extension TEXT,
        for_processing INTEGER DEFAULT 0
    )
''')
conn.commit()

# Function to process a directory
def process_directory(directory, last_processed_id=0):
    for root, _, files in os.walk(directory):
        for file in files:
            full_path = os.path.join(root, file)
            filename, extension = os.path.splitext(file)
            
            # Check if the current file was already processed
            cursor.execute('SELECT id FROM files WHERE full_path=?', (full_path,))
            existing_id = cursor.fetchone()
            
            if existing_id:
                # Skip if the file was already processed
                continue
            
            # Insert data into the database
            cursor.execute('''
                INSERT INTO files (full_path, filename, extension)
                VALUES (?, ?, ?)
            ''', (full_path, filename, extension))
            conn.commit()
            
# Replace 'your_directory_path' with the actual directory you want to scan
directory_path = r'D:\DEFCON-Videos'

process_directory(directory_path)

# Close the database connection
conn.close()
