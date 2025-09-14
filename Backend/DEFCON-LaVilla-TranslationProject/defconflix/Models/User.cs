using defconflix.Enums;
using System.ComponentModel.DataAnnotations;

namespace defconflix.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string GitHubId { get; set; }

        [Required]
        public string Username { get; set; }

        public string Email { get; set; }

        [Required]
        public string ApiKey { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? LastAccessedAt { get; set; }
        public UserRole Role { get; set; } = UserRole.User;
    }
}
