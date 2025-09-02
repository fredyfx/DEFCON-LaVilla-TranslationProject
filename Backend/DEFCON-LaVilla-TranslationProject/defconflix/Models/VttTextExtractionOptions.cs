using defconflix.Enums;

namespace defconflix.Models
{
    public class VttTextExtractionOptions
    {
        public bool IncludeTimestamps { get; set; } = false;             
        public bool SkipEmptyLines { get; set; } = true;                
        public bool RemoveExtraWhitespace { get; set; } = true;        
        public bool TrimResult { get; set; } = true;        
        public TextSeparatorType SeparatorType { get; set; } = TextSeparatorType.NewLine;        
        public string? CustomSeparator { get; set; }
    }
}
