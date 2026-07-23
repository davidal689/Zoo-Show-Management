using System;

namespace Zoo_Show_Mnm.Models
{
    public class SeatLock
    {
        public int Id { get; set; }
        
        public int ShowId { get; set; }
        public virtual Show? Show { get; set; }
        
        public int TicketQuantity { get; set; }
        
        public string LockedBySession { get; set; } = string.Empty;
        
        public DateTime ExpiresAt { get; set; }
        
        public bool IsReleased { get; set; } = false;
        
        public string? SelectedSeats { get; set; }
    }
}
