using defconflix.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using System.Text.RegularExpressions;

namespace defconflix.Models
{
    public class VttFile
    {
        public VttFile()
        {
            Cues = new List<VttCue>();
            CreatedAt = DateTime.UtcNow;
        }

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public string FileName { get; set; }
        public string Language { get; set; }
        public int FileId { get; set; }

        public string Header { get; set; } = "WEBVTT";

        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<VttCue> Cues { get; set; }

        [NotMapped]
        public TimeSpan TotalDuration => Cues?.Any() == true ? Cues.Max(c => c.EndTime) : TimeSpan.Zero;

        [NotMapped]
        public int CueCount => Cues?.Count ?? 0;

        /// <summary>
        /// Splits all long cues in the VTT file into smaller, more readable cues
        /// </summary>
        /// <param name="maxWordsPerCue">Maximum words per cue</param>
        /// <param name="maxCharactersPerCue">Maximum characters per cue</param>
        /// <param name="splitOnSentences">Whether to prefer splitting on sentence boundaries</param>
        public void SplitLongCues(int maxWordsPerCue = 15, int maxCharactersPerCue = 80, bool splitOnSentences = true)
        {
            var newCues = new List<VttCue>();

            foreach (var cue in Cues)
            {
                var splitCues = cue.SplitCue(maxWordsPerCue, maxCharactersPerCue, splitOnSentences);
                newCues.AddRange(splitCues);
            }

            Cues.Clear();
            foreach (var cue in newCues)
            {
                cue.VttFile = this;
                Cues.Add(cue);
            }
        }

        public string ExtractPureText(VttTextExtractionOptions? options = null)
        {
            options ??= new VttTextExtractionOptions();

            if (!Cues.Any())
                return string.Empty;

            var sortedCues = Cues.OrderBy(c => c.StartTime).ToList();
            var textBuilder = new StringBuilder();

            for (int i = 0; i < sortedCues.Count; i++)
            {
                var cue = sortedCues[i];
                var cleanText = cue.CleanText;

                // Skip empty cues if option is set
                if (options.SkipEmptyLines && string.IsNullOrWhiteSpace(cleanText))
                    continue;

                // Add timestamp if requested
                if (options.IncludeTimestamps)
                {
                    textBuilder.AppendLine($"[{cue.FormattedTimestamp}]");
                }

                // Add the text
                textBuilder.Append(cleanText);

                // Add separator between cues
                if (i < sortedCues.Count - 1)
                {
                    switch (options.SeparatorType)
                    {
                        case TextSeparatorType.NewLine:
                            textBuilder.AppendLine();
                            break;
                        case TextSeparatorType.DoubleNewLine:
                            textBuilder.AppendLine();
                            textBuilder.AppendLine();
                            break;
                        case TextSeparatorType.Space:
                            textBuilder.Append(' ');
                            break;
                        case TextSeparatorType.Custom:
                            textBuilder.Append(options.CustomSeparator ?? " ");
                            break;
                    }
                }
            }

            var result = textBuilder.ToString();

            // Apply additional formatting options
            if (options.RemoveExtraWhitespace)
            {
                // Remove multiple consecutive spaces and normalize line breaks
                result = Regex.Replace(result, @"\s+", " ");
                result = Regex.Replace(result, @"(\r\n|\n\r|\n|\r)", Environment.NewLine);
                result = Regex.Replace(result, @"(\r\n){2,}", Environment.NewLine + Environment.NewLine);
            }

            if (options.TrimResult)
            {
                result = result.Trim();
            }

            return result;
        }
    }
}
