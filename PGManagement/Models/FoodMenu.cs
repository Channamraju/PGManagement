using System.ComponentModel.DataAnnotations;

namespace PGManagement.Models
{
    public class FoodMenu
    {
        public int FoodMenuId { get; set; }

        [Required]
        [StringLength(100)]
        public string ItemName { get; set; } = string.Empty;

        [Required]
        [StringLength(30)]
        public string MealType { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string DayOfWeek { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        public bool IsAvailable { get; set; } = true;

        public int PGId { get; set; }

        public PG? PG { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}