namespace PGManagement.Models
{
    public class User
    {
        public int UserId { get; set; }

        public string FullName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string PasswordHash { get; set; } = string.Empty;

        public string Phone { get; set; } = string.Empty;

        public string Address { get; set; } = string.Empty;

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public string Status { get; set; } = "Active";

        // Profile photo
        public string? ProfileImagePath { get; set; }

        // Aadhaar number
        public string? AadhaarNumber { get; set; }

        // Aadhaar document image
        public string? AadhaarImagePath { get; set; }
    }
}