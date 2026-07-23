using System;
using System.ComponentModel.DataAnnotations;

namespace Zoo_Show_Mnm.Models
{
    public class Show
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        [Required]
        public DateTime DateTime { get; set; }

        [Required]
        [StringLength(100)]
        public string Venue { get; set; } = string.Empty;

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Capacity must be at least 1")]
        public int SeatCapacity { get; set; }

        [Required]
        public int RemainingSeatCapacity { get; set; }

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than 0")]
        public decimal TicketPrice { get; set; }

        [Required]
        [StringLength(15)]
        public string Status { get; set; } = "Draft"; // Draft, Published, Cancelled
    }
}
