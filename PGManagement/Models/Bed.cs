using System.ComponentModel.DataAnnotations;

namespace PGManagement.Models
{
    public class Bed
    {
        public int BedId { get; set; }

        [Required]
        public string BedNumber { get; set; } = string.Empty;

        public string Status { get; set; } = "Available";

        // Room relationship
        public int RoomId { get; set; }

        public Room? Room { get; set; }
    }
}