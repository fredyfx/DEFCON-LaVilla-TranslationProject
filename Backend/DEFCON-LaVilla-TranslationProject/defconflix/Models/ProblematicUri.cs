using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace defconflix.Models
{
    public class ProblematicUri
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(2000)]
        public string OriginalUri { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? SanitizedUri { get; set; }

        [Required]
        [MaxLength(500)]
        public string FileName { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? Extension { get; set; }

        [Required]
        [MaxLength(100)]
        public string ErrorType { get; set; } = string.Empty; // e.g., "NULL_BYTES", "CONTROL_CHARS", "ENCODING_ERROR"

        [MaxLength(2000)]
        public string? ErrorDetails { get; set; } // Detailed analysis of the problem

        [MaxLength(1000)]
        public string? PostgresqlError { get; set; } // The actual PostgreSQL error message

        [Required]
        public int CrawlerJobId { get; set; }

        [ForeignKey("CrawlerJobId")]
        public virtual CrawlerJob? CrawlerJob { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsResolved { get; set; } = false;

        public DateTime? ResolvedAt { get; set; }

        [MaxLength(1000)]
        public string? ResolutionNotes { get; set; }

        // Helper properties for analysis
        [NotMapped]
        public bool HasNullBytes => !string.IsNullOrEmpty(ErrorDetails) && ErrorDetails.Contains("Null bytes");

        [NotMapped]
        public bool HasControlChars => !string.IsNullOrEmpty(ErrorDetails) && ErrorDetails.Contains("Control char");

        [NotMapped]
        public bool HasEncodingIssues => ErrorType.Contains("ENCODING");

        [NotMapped]
        public string FileDirectory => Path.GetDirectoryName(OriginalUri) ?? string.Empty;
    }
}
