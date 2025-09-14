using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace defconflix.Models
{
    [Table("files")]
    public class Files
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }
        [Column("file_path")]
        public string File_Path { get; set; }
        [Column("file_name")]
        public string File_Name { get; set; }
        [Column("extension")]
        public string Extension { get; set; }
        [Column("size_bytes")]
        public long Size_Bytes { get; set; }
        [Column("hash")]
        public string? Hash { get; set; }
        [Column("status")]
        public string Status { get; set; } // Not started, In Progress, Completed
        [Column("created_at")]
        public DateTime Created_At { get; set; }
        [Column("updated_at")]
        public DateTime? Updated_At { get; set; }
        public long ProcessedBy { get; set; }

        // New columns for file availability tracking
        [Column("last_check_accessible")]
        public bool? LastCheckAccessible { get; set; }

        [Column("last_checked_at")]
        public DateTime? LastCheckedAt { get; set; }

        // Navigation property
        public virtual ICollection<FileStatusCheck> StatusChecks { get; set; } = new List<FileStatusCheck>();

        // Helper method to get the HTTP URL
        [NotMapped]
        public string HttpUrl => GetFTPLocation(File_Path);

        // Helper properties
        [NotMapped]
        public bool NeedsCheck => !LastCheckedAt.HasValue ||
                                  LastCheckedAt.Value.AddHours(24) < DateTime.UtcNow;

        [NotMapped]
        public string AvailabilityStatus => LastCheckAccessible switch
        {
            true => "Available",
            false => "Not Available (404)",
            null => "Not Checked"
        };

        private static string GetFTPLocation(string filePath)
        {
            var removePrefixUntil = filePath.IndexOf("cons");
            filePath = filePath.Remove(0, removePrefixUntil);

            // Remove the "cons\\" prefix if it exists
            string cleanPath = filePath;
            if (cleanPath.StartsWith("cons\\", StringComparison.OrdinalIgnoreCase))
            {
                cleanPath = cleanPath.Substring(5); // Remove "cons\\"
            }

            if (cleanPath.StartsWith("DEF CON", StringComparison.OrdinalIgnoreCase))
            {
                cleanPath = cleanPath.Substring(7); // Remove "DEF CON"
            }

            // Replace backslashes with forward slashes
            cleanPath = cleanPath.Replace('\\', '/');

            // URL encode the path components while preserving forward slashes
            string[] pathParts = cleanPath.Split('/');
            for (int i = 0; i < pathParts.Length; i++)
            {
                pathParts[i] = Uri.EscapeDataString(pathParts[i]);
            }

            // Reconstruct the path with forward slashes
            string urlEncodedPath = string.Join("/", pathParts);

            // Construct the full HTTP URL
            string httpUrl = "https://media.defcon.org" + urlEncodedPath;

            return httpUrl;
        }
    }
}
