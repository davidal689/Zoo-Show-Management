using System;
using System.Linq;
using Zoo_Show_Mnm.Models;
using BCrypt.Net;

namespace Zoo_Show_Mnm.Data
{
    public static class DbInitializer
    {
        public static void Initialize(ApplicationDbContext context)
        {
            context.Database.EnsureCreated();

            // Seed Users
            if (!context.Users.Any())
            {
                context.Users.AddRange(
                    new User
                    {
                        Name = "Admin Account",
                        Username = "admin",
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                        Role = "Administrator",
                        IsTemporaryPassword = false
                    },
                    new User
                    {
                        Name = "Manager Account",
                        Username = "manager",
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("manager123"),
                        Role = "Show Manager",
                        IsTemporaryPassword = false
                    },
                    new User
                    {
                        Name = "Cashier Account",
                        Username = "cashier",
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("cashier123"),
                        Role = "Cashier",
                        IsTemporaryPassword = false
                    },
                    new User
                    {
                        Name = "Visitor Account",
                        Username = "visitor",
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("visitor123"),
                        Role = "Visitor",
                        IsTemporaryPassword = false
                    }
                );
                context.SaveChanges();
            }

            // Seed Shows
            if (!context.Shows.Any())
            {
                context.Shows.AddRange(
                    new Show
                    {
                        Name = "Dolphin & Seal Wonders",
                        Description = "Marvel at the intelligence and speed of our playful dolphins and acrobatic seals in a stunning aquatic theater.",
                        DateTime = DateTime.Today.AddDays(1).AddHours(14), // Tomorrow 2 PM
                        Venue = "Splash Amphitheater",
                        SeatCapacity = 100,
                        RemainingSeatCapacity = 100,
                        TicketPrice = 15.50m,
                        Status = "Published"
                    },
                    new Show
                    {
                        Name = "Wings of the Wild Bird Show",
                        Description = "Witness majestic birds of prey soaring above, performing natural fly-bys and demonstrating precision hunting skills.",
                        DateTime = DateTime.Today.AddDays(1).AddHours(10).AddMinutes(30), // Tomorrow 10:30 AM
                        Venue = "Eagle Ridge Arena",
                        SeatCapacity = 80,
                        RemainingSeatCapacity = 80,
                        TicketPrice = 12.00m,
                        Status = "Published"
                    },
                    new Show
                    {
                        Name = "Majestic Lions Feed & Show",
                        Description = "Meet the pride and see the caretakers present interesting facts and run high-energy training demonstrations.",
                        DateTime = DateTime.Today.AddDays(2).AddHours(16), // Day after tomorrow 4 PM
                        Venue = "Savannah Lookout",
                        SeatCapacity = 50,
                        RemainingSeatCapacity = 50,
                        TicketPrice = 20.00m,
                        Status = "Published"
                    },
                    new Show
                    {
                        Name = "Secret Night Creatures (Draft)",
                        Description = "An upcoming nocturnal show exhibiting the incredible abilities of bats, owls, and night hunters.",
                        DateTime = DateTime.Today.AddDays(5).AddHours(20),
                        Venue = "Nocturnal Jungle Dome",
                        SeatCapacity = 40,
                        RemainingSeatCapacity = 40,
                        TicketPrice = 18.00m,
                        Status = "Draft"
                    }
                );
                context.SaveChanges();
            }
        }
    }
}
