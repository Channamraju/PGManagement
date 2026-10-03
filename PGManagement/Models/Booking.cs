using System.ComponentModel.DataAnnotations;

namespace PGManagement.Models
{
    public class Booking
    {
        public int BookingId { get; set; }

        public int UserId { get; set; }

        public int PGId { get; set; }

        public int RoomId { get; set; }

        public int BedId { get; set; }

        public DateTime BookingDate { get; set; } = DateTime.Now;

        [Required]
        public string StayType { get; set; } = "Monthly";

        public DateTime StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        public decimal TotalAmount { get; set; }

        public string Status { get; set; } = "Pending";

        public User? User { get; set; }

        public PG? PG { get; set; }

        public Room? Room { get; set; }

        public Bed? Bed { get; set; }

        public ICollection<Payment> Payments { get; set; }
            = new List<Payment>();
    }
}