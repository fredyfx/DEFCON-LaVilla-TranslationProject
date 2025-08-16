import re
import time
from typing import List, Dict
from openai import OpenAI
import os
import sqlite3

# Important detail, this was the initial PoC
# The sqlite is not going to be used for long term.

class VTTTranslator:
    def __init__(self, api_key: str = None):
        """
        Initialize the VTT translator with OpenAI API key
        
        Args:
            api_key (str): OpenAI API key. If None, will look for OPENAI_API_KEY env variable
        """
        if api_key is None:
            api_key = os.getenv('OPENAI_API_KEY')
            if api_key is None:
                raise ValueError("Please provide OpenAI API key or set OPENAI_API_KEY environment variable")
        
        self.client = OpenAI(api_key=api_key)
        
    def parse_vtt_segments(self, vtt_content: str) -> List[Dict]:
        """
        Extract segments from VTT content
        
        Args:
            vtt_content (str): Raw VTT file content
            
        Returns:
            List[Dict]: List of segments with start, end, and text
        """
        segments = []
        lines = vtt_content.split('\n')
        i = 0
        
        while i < len(lines):
            line = lines[i].strip()
            
            # Look for timestamp line (HH:MM:SS.mmm --> HH:MM:SS.mmm)
            if '-->' in line:
                timestamp_match = re.match(
                    r'(\d{2}:\d{2}:\d{2}\.\d{3})\s*-->\s*(\d{2}:\d{2}:\d{2}\.\d{3})',
                    line
                )
                if timestamp_match:
                    start_time = timestamp_match.group(1)
                    end_time = timestamp_match.group(2)
                    
                    # Get subtitle text (next non-empty line(s))
                    text_lines = []
                    i += 1
                    while i < len(lines) and lines[i].strip():
                        # Remove speaker tags and HTML tags if present
                        clean_line = re.sub(r'<[^>]+>', '', lines[i].strip())
                        clean_line = re.sub(r'<v [^>]+>', '', clean_line)
                        if clean_line:
                            text_lines.append(clean_line)
                        i += 1
                    
                    if text_lines:
                        segments.append({
                            'start': start_time,
                            'end': end_time,
                            'text': ' '.join(text_lines).strip()
                        })
            i += 1
        
        return segments

    def parse_translation_response(self, response_text: str) -> List[str]:
        """
        Parse the translation response from OpenAI
        
        Args:
            response_text (str): Response from OpenAI
            
        Returns:
            List[str]: List of translated texts
        """
        lines = response_text.strip().split('\n')
        translations = []
        
        for line in lines:
            line = line.strip()
            if line:
                # Remove numbering (1., 2., etc.) if present
                clean_line = re.sub(r'^\d+\.\s*', '', line)
                if clean_line:
                    translations.append(clean_line)
        
        return translations

    def translate_batch(self, spanish_texts: List[str], batch_number: int = 1) -> List[str]:
        """
        Translate a batch of Spanish texts to English
        
        Args:
            spanish_texts (List[str]): List of Spanish texts to translate
            batch_number (int): Batch number for progress tracking
            
        Returns:
            List[str]: List of translated English texts
        """
        print(f"Translating batch {batch_number} ({len(spanish_texts)} segments)...")
        
        # Create numbered list of Spanish texts
        numbered_texts = [f"{i+1}. {text}" for i, text in enumerate(spanish_texts)]
        spanish_list = '\n'.join(numbered_texts)
        
        prompt = f"""Translate the following Spanish subtitle segments to natural English, maintaining:
                    - Natural flow and timing appropriate for subtitles
                    - Cultural context and idioms (adapt when necessary)
                    - Speaker tone and emotional content
                    - Subtitle length constraints (keep lines readable)
                    - Conversational style with contractions when appropriate

                    Spanish segments:
                    {spanish_list}

                    Important: Provide ONLY the English translations, numbered the same way (1., 2., etc.). Do not include any other text or explanations."""

        try:
            response = self.client.chat.completions.create(
                model="gpt-4",  # Use gpt-3.5-turbo for faster/cheaper option
                messages=[
                    {
                        "role": "system", 
                        "content": "You are a professional subtitle translator specializing in Spanish to English translation. You understand cultural nuances and subtitle formatting requirements."
                    },
                    {
                        "role": "user", 
                        "content": prompt
                    }
                ],
                temperature=0.3,  # Low temperature for consistent translations
                max_tokens=2000   # Adjust based on batch size
            )
            
            # Parse the response
            english_texts = self.parse_translation_response(response.choices[0].message.content)
            
            # Ensure we have the same number of translations as inputs
            if len(english_texts) != len(spanish_texts):
                print(f"Warning: Expected {len(spanish_texts)} translations, got {len(english_texts)}")
                # Pad with original texts if needed
                while len(english_texts) < len(spanish_texts):
                    english_texts.append(spanish_texts[len(english_texts)])
            
            return english_texts[:len(spanish_texts)]  # Trim if too many
            
        except Exception as e:
            print(f"Error translating batch {batch_number}: {e}")
            print("Falling back to original Spanish text...")
            return spanish_texts  # Fallback to original text

    def write_vtt_file(self, segments: List[Dict], output_path: str):
        """
        Write segments to VTT file
        
        Args:
            segments (List[Dict]): List of segments with start, end, and text
            output_path (str): Output file path
        """
        with open(output_path, 'w', encoding='utf-8') as f:
            f.write("WEBVTT\n\n")
            
            for i, segment in enumerate(segments):
                # Add sequence number for better compatibility
                f.write(f"{i + 1}\n")
                f.write(f"{segment['start']} --> {segment['end']}\n")
                f.write(f"{segment['text']}\n\n")
        
        print(f"Translated VTT file saved to: {output_path}")

    def translate_vtt_file(self, input_vtt_path: str, output_vtt_path: str, batch_size: int = 10):
        """
        Translate a Spanish VTT file to English
        
        Args:
            input_vtt_path (str): Path to input Spanish VTT file
            output_vtt_path (str): Path to output English VTT file
            batch_size (int): Number of segments to translate in each API call
        """
        print(f"Reading VTT file: {input_vtt_path}")
        
        # Read the VTT file
        try:
            with open(input_vtt_path, 'r', encoding='utf-8') as f:
                content = f.read()
        except FileNotFoundError:
            raise FileNotFoundError(f"Input file not found: {input_vtt_path}")
        except UnicodeDecodeError:
            # Try with different encoding
            with open(input_vtt_path, 'r', encoding='latin-1') as f:
                content = f.read()
        
        # Extract segments
        segments = self.parse_vtt_segments(content)
        print(f"Found {len(segments)} subtitle segments")
        
        if not segments:
            raise ValueError("No subtitle segments found in the VTT file")
        
        # Translate in batches
        translated_segments = []
        total_batches = (len(segments) + batch_size - 1) // batch_size
        
        for i in range(0, len(segments), batch_size):
            batch = segments[i:i+batch_size]
            batch_number = i // batch_size + 1
            
            spanish_texts = [seg['text'] for seg in batch]
            
            # Translate batch
            english_texts = self.translate_batch(spanish_texts, batch_number)
            
            # Update segments with translations
            for j, english_text in enumerate(english_texts):
                if i + j < len(segments):
                    translated_segments.append({
                        'start': batch[j]['start'],
                        'end': batch[j]['end'], 
                        'text': english_text
                    })
            
            # Small delay between batches to respect rate limits
            if batch_number < total_batches:
                time.sleep(1)
            
            print(f"Completed batch {batch_number}/{total_batches}")
        
        # Write translated VTT file
        self.write_vtt_file(translated_segments, output_vtt_path)
        print(f"Translation complete! Translated {len(translated_segments)} segments.")

def main():
    """
    Example usage of the VTT translator
    """
    # Option 1: Set API key directly
    translator = VTTTranslator(api_key="")
    
    # Option 2: Use environment variable (recommended)
    # Set OPENAI_API_KEY environment variable first
    # try:
    #     translator = VTTTranslator()
    # except ValueError as e:
    #     print(f"Error: {e}")
    #     print("Please set your OpenAI API key:")
    #     print("Option 1: Set environment variable: export OPENAI_API_KEY='your-key-here'")
    #     print("Option 2: Pass directly: VTTTranslator(api_key='your-key-here')")
    #     return
    
    
    database_file = 'file_data.db'
    # Establish a connection to the SQLite database
    connection = sqlite3.connect(database_file)
    cursor = connection.cursor()
    
    cursor.execute("SELECT * FROM files WHERE full_path LIKE '%DEFCON%'")
    matching_files = cursor.fetchall()
    row_count = len(matching_files) + 0
    # Close the database connection
    connection.close()

    for row in matching_files:
        id, full_path, filename, extension, for_processing = row
        input_file = full_path
        current_folder = os.path.dirname(full_path)
        file_name_without_extension = os.path.splitext(os.path.basename(filename))[0]
        original_spanish_output_vtt_file = os.path.join(current_folder, file_name_without_extension + '.vtt')
        translated_english_output_vtt_file = os.path.join(current_folder, file_name_without_extension + '-en' + '.vtt')
        
        # Translate VTT file
        input_file = original_spanish_output_vtt_file # Change to your input file
        output_file = translated_english_output_vtt_file  # Change to your desired output file
        
        try:
            translator.translate_vtt_file(
                input_vtt_path=input_file,
                output_vtt_path=output_file,
                batch_size=8  # Adjust based on your needs and rate limits
            )
            
            print("\n✅ Translation completed successfully!")
            print(f"📁 Input file: {input_file}")
            print(f"📁 Output file: {output_file}")
            
        except FileNotFoundError as e:
            print(f"❌ Error: {e}")
        except Exception as e:
            print(f"❌ Unexpected error: {e}")

if __name__ == "__main__":
    main()