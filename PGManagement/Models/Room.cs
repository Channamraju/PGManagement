using System.ComponentModel.DataAnnotations;

namespace PGManagement.Models
{
    public class Room
    {
        public int RoomId { get; set; }

        [Required]
        [StringLength(20)]
        public string RoomNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select room type.")]
        public string RoomType { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select sharing type.")]
        public string SharingType { get; set; } = string.Empty;

        public int Capacity { get; set; }

        public int AvailableBeds { get; set; }

        public decimal MonthlyRent { get; set; }

        public decimal DailyRent { get; set; }

        public string Status { get; set; } = "Available";

        public int PGId { get; set; }

        public PG? PG { get; set; }

        public ICollection<Bed> Beds { get; set; }
            = new List<Bed>();
    }
}