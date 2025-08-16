import os
import hashlib
import psycopg2
from psycopg2 import sql
from pathlib import Path
import logging
from typing import Optional, Tuple
from datetime import datetime

# Install:
# pip install psycopg2-binary

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(levelname)s - %(message)s'
)
logger = logging.getLogger(__name__)

class FileScanner:
    def __init__(self, db_config: dict):
        """
        Initialize the FileScanner with database configuration.
        
        Args:
            db_config (dict): Database configuration containing host, database, user, password, port
        """
        self.db_config = db_config
        self.connection = None
        
    def connect_to_db(self) -> bool:
        """
        Establish connection to PostgreSQL database.
        
        Returns:
            bool: True if connection successful, False otherwise
        """
        try:
            self.connection = psycopg2.connect(**self.db_config)
            logger.info("Successfully connected to PostgreSQL database")
            return True
        except psycopg2.Error as e:
            logger.error(f"Error connecting to PostgreSQL database: {e}")
            return False
    
    def create_table(self) -> bool:
        """
        Create the files table if it doesn't exist.
        
        Returns:
            bool: True if table creation successful, False otherwise
        """
        create_table_query = """
        CREATE TABLE IF NOT EXISTS files (
            id SERIAL PRIMARY KEY,
            file_path TEXT NOT NULL,
            file_name VARCHAR(255) NOT NULL,
            extension VARCHAR(50),
            size_bytes BIGINT,
            hash VARCHAR(64),
            status VARCHAR(20) DEFAULT 'Not started' CHECK (status IN ('In Progress', 'Completed', 'Not started')),
            created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
            updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
        );
        
        -- Create index on file_path for faster lookups
        CREATE INDEX IF NOT EXISTS idx_files_path ON files(file_path);
        CREATE INDEX IF NOT EXISTS idx_files_hash ON files(hash);
        """
        
        try:
            with self.connection.cursor() as cursor:
                cursor.execute(create_table_query)
                self.connection.commit()
                logger.info("Files table created successfully")
                return True
        except psycopg2.Error as e:
            logger.error(f"Error creating table: {e}")
            return False
    
    def calculate_file_hash(self, file_path: str, algorithm: str = 'sha256') -> Optional[str]:
        """
        Calculate hash of a file.
        
        Args:
            file_path (str): Path to the file
            algorithm (str): Hash algorithm to use (default: sha256)
            
        Returns:
            Optional[str]: File hash or None if error
        """
        try:
            hash_func = getattr(hashlib, algorithm)()
            
            with open(file_path, 'rb') as f:
                # Read file in chunks to handle large files efficiently
                for chunk in iter(lambda: f.read(8192), b""):
                    hash_func.update(chunk)
            
            return hash_func.hexdigest()
        except (OSError, IOError) as e:
            logger.warning(f"Could not calculate hash for {file_path}: {e}")
            return None
    
    def get_file_info(self, file_path: str) -> Tuple[str, str, int, Optional[str]]:
        """
        Extract file information.
        
        Args:
            file_path (str): Path to the file
            
        Returns:
            Tuple: (file_name, extension, size_bytes, hash)
        """
        path_obj = Path(file_path)
        file_name = path_obj.name
        extension = path_obj.suffix.lower() if path_obj.suffix else ''
        
        try:
            size_bytes = os.path.getsize(file_path)
        except OSError:
            size_bytes = 0
            logger.warning(f"Could not get size for {file_path}")
        
        # Calculate hash
        file_hash = self.calculate_file_hash(file_path)
        
        return file_name, extension, size_bytes, file_hash
    
    def file_exists_in_db(self, file_path: str) -> bool:
        """
        Check if file already exists in database.
        
        Args:
            file_path (str): Path to check
            
        Returns:
            bool: True if file exists, False otherwise
        """
        try:
            with self.connection.cursor() as cursor:
                cursor.execute("SELECT 1 FROM files WHERE file_path = %s", (file_path,))
                return cursor.fetchone() is not None
        except psycopg2.Error as e:
            logger.error(f"Error checking file existence: {e}")
            return False
    
    def insert_file_record(self, file_path: str, file_name: str, extension: str, 
                          size_bytes: int, file_hash: Optional[str], status: str = 'Not started') -> bool:
        """
        Insert a file record into the database.
        
        Args:
            file_path (str): Full path to the file
            file_name (str): Name of the file
            extension (str): File extension
            size_bytes (int): File size in bytes
            file_hash (Optional[str]): File hash
            status (str): Processing status
            
        Returns:
            bool: True if insertion successful, False otherwise
        """
        insert_query = """
        INSERT INTO files (file_path, file_name, extension, size_bytes, hash, status)
        VALUES (%s, %s, %s, %s, %s, %s)
        """
        
        try:
            with self.connection.cursor() as cursor:
                cursor.execute(insert_query, (file_path, file_name, extension, size_bytes, file_hash, status))
                self.connection.commit()
                return True
        except psycopg2.Error as e:
            logger.error(f"Error inserting file record: {e}")
            return False
    
    def update_file_status(self, file_path: str, status: str) -> bool:
        """
        Update the status of a file record.
        
        Args:
            file_path (str): Path to the file
            status (str): New status ('In Progress', 'Completed', 'Not started')
            
        Returns:
            bool: True if update successful, False otherwise
        """
        update_query = """
        UPDATE files 
        SET status = %s, updated_at = CURRENT_TIMESTAMP
        WHERE file_path = %s
        """
        
        try:
            with self.connection.cursor() as cursor:
                cursor.execute(update_query, (status, file_path))
                self.connection.commit()
                return cursor.rowcount > 0
        except psycopg2.Error as e:
            logger.error(f"Error updating file status: {e}")
            return False
    
    def scan_directory(self, root_directory: str, skip_existing: bool = True) -> int:
        """
        Scan directory recursively and store file information in database.
        
        Args:
            root_directory (str): Root directory to scan
            skip_existing (bool): Skip files that already exist in database
            
        Returns:
            int: Number of files processed
        """
        if not os.path.exists(root_directory):
            logger.error(f"Directory does not exist: {root_directory}")
            return 0
        
        files_processed = 0
        
        logger.info(f"Starting directory scan: {root_directory}")
        
        for root, dirs, files in os.walk(root_directory):
            for file_name in files:
                file_path = os.path.join(root, file_name)
                
                # Skip if file already exists in database and skip_existing is True
                if skip_existing and self.file_exists_in_db(file_path):
                    logger.debug(f"Skipping existing file: {file_path}")
                    continue
                
                try:
                    # Get file information
                    name, extension, size, file_hash = self.get_file_info(file_path)
                    
                    # Insert into database
                    if self.insert_file_record(file_path, name, extension, size, file_hash):
                        files_processed += 1
                        logger.debug(f"Processed: {file_path}")
                    else:
                        logger.warning(f"Failed to insert: {file_path}")
                        
                except Exception as e:
                    logger.error(f"Error processing file {file_path}: {e}")
                    continue
        
        logger.info(f"Directory scan completed. Files processed: {files_processed}")
        return files_processed
    
    def get_files_by_status(self, status: str) -> list:
        """
        Get files filtered by status.
        
        Args:
            status (str): Status to filter by
            
        Returns:
            list: List of file records
        """
        query = "SELECT * FROM files WHERE status = %s ORDER BY file_path"
        
        try:
            with self.connection.cursor() as cursor:
                cursor.execute(query, (status,))
                return cursor.fetchall()
        except psycopg2.Error as e:
            logger.error(f"Error fetching files by status: {e}")
            return []
    
    def get_file_statistics(self) -> dict:
        """
        Get statistics about files in database.
        
        Returns:
            dict: Statistics including count by status, total size, etc.
        """
        stats_query = """
        SELECT 
            COUNT(*) as total_files,
            SUM(size_bytes) as total_size,
            COUNT(CASE WHEN status = 'Not started' THEN 1 END) as not_started,
            COUNT(CASE WHEN status = 'In Progress' THEN 1 END) as in_progress,
            COUNT(CASE WHEN status = 'Completed' THEN 1 END) as completed,
            COUNT(DISTINCT extension) as unique_extensions
        FROM files
        """
        
        try:
            with self.connection.cursor() as cursor:
                cursor.execute(stats_query)
                result = cursor.fetchone()
                
                if result:
                    return {
                        'total_files': result[0] or 0,
                        'total_size': result[1] or 0,
                        'not_started': result[2] or 0,
                        'in_progress': result[3] or 0,
                        'completed': result[4] or 0,
                        'unique_extensions': result[5] or 0
                    }
        except psycopg2.Error as e:
            logger.error(f"Error getting statistics: {e}")
        
        return {}
    
    def close_connection(self):
        """Close database connection."""
        if self.connection:
            self.connection.close()
            logger.info("Database connection closed")


def main():
    """
    Example usage of the FileScanner class.
    """
    # Database configuration
    db_config = {
        'host': 'localhost',
        'database': 'your_database',
        'user': 'your_username',
        'password': 'your_password',
        'port': 5432
    }
    
    # Directory to scan
    directory_to_scan = "/path/to/your/directory"
    
    # Initialize scanner
    scanner = FileScanner(db_config)
    
    try:
        # Connect to database
        if not scanner.connect_to_db():
            return
        
        # Create table
        if not scanner.create_table():
            return
        
        # Scan directory
        files_processed = scanner.scan_directory(directory_to_scan)
        print(f"Processed {files_processed} files")
        
        # Get statistics
        stats = scanner.get_file_statistics()
        print("\nFile Statistics:")
        for key, value in stats.items():
            print(f"{key}: {value}")
        
        # Example: Update status of some files
        # scanner.update_file_status("/path/to/specific/file.txt", "In Progress")
        
        # Example: Get files with specific status
        # not_started_files = scanner.get_files_by_status("Not started")
        # print(f"Files not started: {len(not_started_files)}")
        
    except Exception as e:
        logger.error(f"An error occurred: {e}")
    
    finally:
        scanner.close_connection()


if __name__ == "__main__":
    main()