using System;

namespace Zoo_Show_Mnm.Models
{
    public class SeatLock
    {
        public int Id { get; set; }
        
        public int ShowId { get; set; }
        public virtual Show? Show { get; set; }
        
        public int TicketQuantity { get; set; }
        
        // Use a static session token or machine identifier for WPF desktop checkout session
        public string SessionId { get; set; } = string.Empty;
        
        public DateTime CreatedAt { get; set; }
        
        public DateTime ExpiresAt { get; set; }
        
        public bool IsReleased { get; set; } = false;
    }
}
