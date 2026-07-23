using System;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using Zoo_Show_Mnm.Data;
using Zoo_Show_Mnm.Models;
using Zoo_Show_Mnm.Views;

namespace Zoo_Show_Mnm
{
    public partial class MainWindow : Window
    {
        private LoginView? _loginView;
        private VisitorDashboard? _visitorDashboard;
        private ManagerDashboard? _managerDashboard;
        private CashierDashboard? _cashierDashboard;
        private AdminDashboard? _adminDashboard;

        private DispatcherTimer? _backgroundLockCleaner;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Initialize Database and Seed Data
            using (var db = new ApplicationDbContext())
            {
                DbInitializer.Initialize(db);

                // Auto-repair potentially corrupted password hashes in SQL Server database
                var admin = db.Users.FirstOrDefault(u => u.Username == "admin");
                if (admin != null) admin.PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123");

                var manager = db.Users.FirstOrDefault(u => u.Username == "manager");
                if (manager != null) manager.PasswordHash = BCrypt.Net.BCrypt.HashPassword("manager123");

                var cashier = db.Users.FirstOrDefault(u => u.Username == "cashier");
                if (cashier != null) cashier.PasswordHash = BCrypt.Net.BCrypt.HashPassword("cashier123");

                var visitor = db.Users.FirstOrDefault(u => u.Username == "visitor");
                if (visitor != null) visitor.PasswordHash = BCrypt.Net.BCrypt.HashPassword("visitor123");

                db.SaveChanges();
            }

            // Start background seat lock release timer (ticks every 30 seconds)
            StartBackgroundLockCleaner();

            // Display Show List (Guest Mode) by default
            ShowVisitorDashboard(null);
        }

        public void ShowLoginScreen()
        {
            _loginView = new LoginView();
            _loginView.LoginSuccess += LoginView_LoginSuccess;
            ContentArea.Content = _loginView;
        }

        private void LoginView_LoginSuccess(object? sender, User user)
        {
            // Transition user to their role-appropriate dashboard
            switch (user.Role)
            {
                case "Visitor":
                    ShowVisitorDashboard(user);
                    break;
                    
                case "Show Manager":
                    _managerDashboard = new ManagerDashboard { CurrentUser = user };
                    ContentArea.Content = _managerDashboard;
                    _managerDashboard.LoadData();
                    break;
                    
                case "Cashier":
                    _cashierDashboard = new CashierDashboard { CurrentUser = user };
                    ContentArea.Content = _cashierDashboard;
                    _cashierDashboard.LoadData();
                    break;
                    
                case "Administrator":
                    _adminDashboard = new AdminDashboard { CurrentUser = user };
                    ContentArea.Content = _adminDashboard;
                    _adminDashboard.LoadData();
                    break;
                    
                default:
                    MessageBox.Show("Unknown user role.", "Access Denied", MessageBoxButton.OK, MessageBoxImage.Error);
                    ShowLoginScreen();
                    break;
            }
        }

        public void ShowVisitorDashboard(User? user)
        {
            _visitorDashboard = new VisitorDashboard { CurrentUser = user };
            ContentArea.Content = _visitorDashboard;
            _visitorDashboard.LoadData();
        }

        public void LogOut()
        {
            ShowVisitorDashboard(null);
        }

        private void StartBackgroundLockCleaner()
        {
            _backgroundLockCleaner = new DispatcherTimer();
            _backgroundLockCleaner.Interval = TimeSpan.FromSeconds(30);
            _backgroundLockCleaner.Tick += BackgroundLockCleaner_Tick;
            _backgroundLockCleaner.Start();
        }

        private async void BackgroundLockCleaner_Tick(object? sender, EventArgs e)
        {
            // Runs a background clean of expired seat locks (BR-12)
            try
            {
                using (var db = new ApplicationDbContext())
                {
                    var now = DateTime.UtcNow;
                    var expiredLocks = await db.SeatLocks
                        .Include(l => l.Show)
                        .Where(l => l.ExpiresAt <= now && !l.IsReleased)
                        .ToListAsync();

                    if (expiredLocks.Any())
                    {
                        foreach (var seatLock in expiredLocks)
                        {
                            seatLock.IsReleased = true;
                            if (seatLock.Show != null)
                            {
                                seatLock.Show.RemainingSeatCapacity = Math.Min(
                                    seatLock.Show.SeatCapacity, 
                                    seatLock.Show.RemainingSeatCapacity + seatLock.TicketQuantity
                                );
                            }

                            // Cancel associated pending bookings
                            var pendingBookings = await db.Bookings
                                .Where(b => b.ShowId == seatLock.ShowId && 
                                            b.BookingStatus == "Pending" && 
                                            b.SelectedSeats == seatLock.SelectedSeats)
                                .ToListAsync();
                            foreach (var b in pendingBookings)
                            {
                                b.BookingStatus = "Cancelled";
                            }
                        }
                        await db.SaveChangesAsync();

                        // Refresh active Visitor / Cashier UI listings if currently active
                        if (ContentArea.Content is VisitorDashboard visitorDb)
                        {
                            visitorDb.LoadData();
                        }
                        else if (ContentArea.Content is CashierDashboard cashierDb)
                        {
                            cashierDb.LoadData();
                        }
                    }
                }
            }
            catch
            {
                // Suppress background errors to keep desktop app running
            }
        }
    }
}