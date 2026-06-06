using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NpgsqlTypes;

namespace defconflix.Models;

public class Summary
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [Required]
    public long FileId { get; set; }

    [ForeignKey("FileId")]
    public virtual Files File { get; set; } = null!;

    [Required]
    [MaxLength(500)]
    public string ShortSummary { get; set; } = string.Empty;

    [MaxLength(5000)]
    public string? FullSummary { get; set; }

    public List<string> KeyTopics { get; set; } = new();

    public List<string> Keywords { get; set; } = new();

    [MaxLength(50)]
    public string GeneratedBy { get; set; } = "ollama";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Full-text search vector. Auto-generated from ShortSummary, FullSummary, Keywords.
    /// </summary>
    public NpgsqlTsVector? SearchVector { get; set; }
}
