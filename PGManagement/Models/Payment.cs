using System.ComponentModel.DataAnnotations;

namespace PGManagement.Models
{
    public class Payment
    {
        public int PaymentId { get; set; }

        public int BookingId { get; set; }

        public int UserId { get; set; }

        public decimal Amount { get; set; }

        [Required]
        public string PaymentMethod { get; set; } = string.Empty;

        [Required]
        public string TransactionId { get; set; } = string.Empty;

        public DateTime PaymentDate { get; set; } = DateTime.Now;

        public string Status { get; set; } = "Success";

        [Required]
        public string PaymentType { get; set; } = "Monthly";

        public DateTime? BillingMonth { get; set; }

        public DateTime? FromDate { get; set; }

        public DateTime? ToDate { get; set; }

        public Booking? Booking { get; set; }

        public User? User { get; set; }
    }
}