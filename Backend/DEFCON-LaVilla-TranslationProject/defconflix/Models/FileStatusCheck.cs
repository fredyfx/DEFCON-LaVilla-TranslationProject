using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace defconflix.Models
{
    public class FileStatusCheck
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [Required]
        public long FileId { get; set; }

        [ForeignKey("FileId")]
        public virtual Files File { get; set; } = null!;

        [Required]
        public DateTime CheckedAt { get; set; }

        [Required]
        public int HttpStatusCode { get; set; }

        [Required]
        public bool IsAccessible { get; set; }

        [MaxLength(500)]
        public string? ErrorMessage { get; set; }

        public long? ResponseTimeMs { get; set; }

        public int? CheckedByUserId { get; set; }

        [ForeignKey("CheckedByUserId")]
        public virtual User? CheckedByUser { get; set; }

        // Helper properties
        [NotMapped]
        public bool Is404 => HttpStatusCode == 404;

        [NotMapped]
        public bool IsServerError => HttpStatusCode >= 500;

        [NotMapped]
        public bool IsClientError => HttpStatusCode >= 400 && HttpStatusCode < 500;

        [NotMapped]
        public string StatusDescription => HttpStatusCode switch
        {
            200 => "OK - File accessible",
            404 => "Not Found - File missing",
            403 => "Forbidden - Access denied",
            500 => "Server Error",
            _ => $"HTTP {HttpStatusCode}"
        };
    }
}
