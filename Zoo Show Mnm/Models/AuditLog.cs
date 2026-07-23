using System;
using System.ComponentModel.DataAnnotations;

namespace Zoo_Show_Mnm.Models
{
    public class AuditLog
    {
        public int Id { get; set; }
        
        public int? ActorAccountId { get; set; }
        public virtual User? ActorAccount { get; set; }
        
        [Required]
        [StringLength(30)]
        public string ActionType { get; set; } = string.Empty;
        
        [Required]
        public string TargetEntity { get; set; } = string.Empty;
        
        [Required]
        public DateTime Timestamp { get; set; }
    }
}
