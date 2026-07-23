using Microsoft.EntityFrameworkCore;
using Zoo_Show_Mnm.Models;

namespace Zoo_Show_Mnm.Data
{
    public class ApplicationDbContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<Show> Shows { get; set; }
        public DbSet<Booking> Bookings { get; set; }
        public DbSet<SeatLock> SeatLocks { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // Connect to local Microsoft SQL Server LocalDB
            optionsBuilder.UseSqlServer("Server=.;Database=ZooShowMnmDb;Trusted_Connection=True;TrustServerCertificate=True;");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<User>().HasIndex(u => u.Username).IsUnique(); // Unique username
        }
    }
}
