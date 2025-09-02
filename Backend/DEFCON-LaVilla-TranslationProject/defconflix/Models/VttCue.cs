using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace defconflix.Models
{
    public class VttCue
    {     
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
             
        [Required]
        public int VttFileId { get; set; }
                
        [ForeignKey("VttFileId")]
        public virtual VttFile VttFile { get; set; } = null!;
                
        [MaxLength(50)]
        public string? CueId { get; set; }
                
        [Required]
        public TimeSpan StartTime { get; set; }
                
        [Required]
        public TimeSpan EndTime { get; set; }
                
        [Required]
        [MaxLength(500)]
        public string Text { get; set; } = string.Empty;
                
        public string? Settings { get; set; }
                
        public int SequenceOrder { get; set; }
                
        [NotMapped]
        public TimeSpan Duration => EndTime - StartTime;
                
        [NotMapped]
        public string FormattedTimestamp =>
            $"{FormatTimeSpan(StartTime)} --> {FormatTimeSpan(EndTime)}";

        [NotMapped]
        public string CleanText =>
            System.Text.RegularExpressions.Regex.Replace(Text, @"<[^>]+>", "");

        [NotMapped]
        public int WordCount =>
            string.IsNullOrWhiteSpace(CleanText) ? 0 :
            CleanText.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

        private static string FormatTimeSpan(TimeSpan timeSpan)
        {
            return $"{(int)timeSpan.TotalHours:D2}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}.{timeSpan.Milliseconds:D3}";
        }

        public List<VttCue> SplitCue(int maxWordsPerCue = 15, int maxCharactersPerCue = 80, bool splitOnSentences = true)
        {
            var result = new List<VttCue>();

            // If the cue is already short enough, return as-is
            if (WordCount <= maxWordsPerCue && CleanText.Length <= maxCharactersPerCue)
            {
                result.Add(this);
                return result;
            }

            var sentences = SplitIntoSentences(CleanText);
            var currentText = "";
            var currentWordCount = 0;
            var totalDuration = Duration;
            var currentStartTime = StartTime;
            var cueIndex = 0;

            foreach (var sentence in sentences)
            {
                var sentenceWords = sentence.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
                var potentialText = string.IsNullOrEmpty(currentText) ? sentence : $"{currentText} {sentence}";

                // Check if adding this sentence would exceed limits
                if ((currentWordCount + sentenceWords > maxWordsPerCue ||
                     potentialText.Length > maxCharactersPerCue) &&
                    !string.IsNullOrEmpty(currentText))
                {
                    // Create a cue with current accumulated text
                    var cueEndTime = CalculateEndTime(currentStartTime, totalDuration, currentWordCount, WordCount);

                    result.Add(new VttCue
                    {
                        VttFileId = VttFileId,
                        CueId = string.IsNullOrEmpty(CueId) ? null : $"{CueId}_{cueIndex + 1}",
                        StartTime = currentStartTime,
                        EndTime = cueEndTime,
                        Text = currentText.Trim(),
                        Settings = Settings,
                        SequenceOrder = SequenceOrder + cueIndex
                    });

                    // Reset for next cue
                    currentStartTime = cueEndTime;
                    currentText = sentence;
                    currentWordCount = sentenceWords;
                    cueIndex++;
                }
                else
                {
                    // Add sentence to current cue
                    currentText = potentialText;
                    currentWordCount += sentenceWords;
                }
            }

            // Add the last cue if there's remaining text
            if (!string.IsNullOrEmpty(currentText))
            {
                result.Add(new VttCue
                {
                    VttFileId = VttFileId,
                    CueId = string.IsNullOrEmpty(CueId) ? null : $"{CueId}_{cueIndex + 1}",
                    StartTime = currentStartTime,
                    EndTime = EndTime,
                    Text = currentText.Trim(),
                    Settings = Settings,
                    SequenceOrder = SequenceOrder + cueIndex
                });
            }

            return result;
        }

        private List<string> SplitIntoSentences(string text)
        {
            // Split on common sentence endings, but keep the punctuation
            var sentences = System.Text.RegularExpressions.Regex.Split(text, @"(?<=[.!?])\s+")
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();

            // If no sentence breaks found, split by commas or phrases
            if (sentences.Count == 1)
            {
                sentences = text.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();
            }

            // Last resort: split by words if still too long
            if (sentences.Count == 1 && sentences[0].Split(' ').Length > 20)
            {
                var words = sentences[0].Split(' ');
                sentences.Clear();
                for (int i = 0; i < words.Length; i += 10)
                {
                    sentences.Add(string.Join(" ", words.Skip(i).Take(10)));
                }
            }

            return sentences;
        }


        /// <summary>
        /// Calculates proportional end time based on word count
        /// </summary>
        private TimeSpan CalculateEndTime(TimeSpan startTime, TimeSpan totalDuration, int currentWords, int totalWords)
        {
            var proportion = (double)currentWords / totalWords;
            var duration = TimeSpan.FromMilliseconds(totalDuration.TotalMilliseconds * proportion);
            return startTime.Add(duration);
        }

        public override string ToString()
        {
            return $"{FormattedTimestamp} - {CleanText}";
        }
    }
}
