using System.ComponentModel.DataAnnotations;

namespace PGManagement.Models
{
    public class Owner
    {
        public int OwnerId { get; set; }

        [Required]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        public string Phone { get; set; } = string.Empty;

        public string Address { get; set; } = string.Empty;

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public string Status { get; set; } = "Active";

        // Profile Image
        public string? ProfileImagePath { get; set; }
    }
}