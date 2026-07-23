using System.ComponentModel.DataAnnotations;

namespace Zoo_Show_Mnm.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required]
        [StringLength(60)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string Username { get; set; } = string.Empty; // Replacing Email with Username

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        [StringLength(15)]
        public string Role { get; set; } = "Visitor"; // Visitor, Show Manager, Cashier, Administrator

        public bool IsTemporaryPassword { get; set; } = false;
        public bool IsDeactivated { get; set; } = false;
    }
}
