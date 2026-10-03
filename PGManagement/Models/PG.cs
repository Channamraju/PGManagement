using System.ComponentModel.DataAnnotations;

namespace PGManagement.Models
{
    public class PG
    {
        public int PGId { get; set; }

        [Required]
        [StringLength(100)]
        public string PGName { get; set; } = string.Empty;

        [Required]
        public string Address { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string City { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string GenderType { get; set; } = "Any";

        public string Status { get; set; } = "Pending";

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        // ==============================
        // PG IMAGE
        // ==============================

        public string? ImagePath { get; set; }

        // ==============================
        // OWNER RELATIONSHIP
        // ==============================

        public int OwnerId { get; set; }

        public Owner? Owner { get; set; }

        // ==============================
        // ROOMS
        // ==============================

        public ICollection<Room> Rooms { get; set; }
            = new List<Room>();
    }
}