using System;
using System.ComponentModel.DataAnnotations;

namespace Zoo_Show_Mnm.Models
{
    public class Booking
    {
        public int Id { get; set; }

        [Required]
        [StringLength(10)]
        public string ReferenceNumber { get; set; } = string.Empty;

        [Required]
        public int ShowId { get; set; }
        public virtual Show? Show { get; set; }

        [Required]
        public DateTime BookingDate { get; set; }

        [Required]
        [Range(1, 999)]
        public int TicketQuantity { get; set; }

        [Required]
        public decimal TotalPrice { get; set; }

        [Required]
        [StringLength(10)]
        public string BookingStatus { get; set; } = "Confirmed"; // Confirmed, Cancelled

        [Required]
        [StringLength(7)]
        public string BookingChannel { get; set; } = "Online"; // Online, Counter

        public int? VisitorAccountId { get; set; }
        public virtual User? VisitorAccount { get; set; }

        public int? IssuingCashierId { get; set; }
        public virtual User? IssuingCashier { get; set; }
        
        public string? WalkInVisitorName { get; set; }
    }
}
