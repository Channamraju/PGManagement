namespace PGManagement.Models
{
    public class Admin
    {
        public int AdminId { get; set; }

        public string FullName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string PasswordHash { get; set; } = string.Empty;

        public string Phone { get; set; } = string.Empty;

        public string Address { get; set; } = string.Empty;

        public DateTime CreatedDate { get; set; }

        public string Status { get; set; } = "Active";
    }
}