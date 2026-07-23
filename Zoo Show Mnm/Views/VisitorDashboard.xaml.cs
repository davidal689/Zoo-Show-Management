using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using Zoo_Show_Mnm.Data;
using Zoo_Show_Mnm.Models;

namespace Zoo_Show_Mnm.Views
{
    public partial class VisitorDashboard : UserControl
    {
        public User? CurrentUser { get; set; }
        
        private Show? _selectedShow;
        private SeatLock? _currentLock;
        private DispatcherTimer? _checkoutTimer;

        public VisitorDashboard()
        {
            InitializeComponent();
        }

        public void LoadData()
        {
            LoadShows();
            LoadHistory();
            ResetDetailsPanel();
        }

        private void LoadShows(DateTime? filterDate = null)
        {
            using (var db = new ApplicationDbContext())
            {
                var query = db.Shows.Where(s => s.Status == "Published");
                
                if (filterDate.HasValue)
                {
                    var date = filterDate.Value.Date;
                    query = query.Where(s => s.DateTime.Date == date);
                }

                GridShows.ItemsSource = query.OrderBy(s => s.DateTime).ToList();
            }
        }

        private void LoadHistory()
        {
            if (CurrentUser == null) return;
            using (var db = new ApplicationDbContext())
            {
                var history = db.Bookings
                    .Include(b => b.Show)
                    .Where(b => b.VisitorAccountId == CurrentUser.Id)
                    .OrderByDescending(b => b.BookingDate)
                    .ToList();

                GridHistory.ItemsSource = history;
            }
        }

        private void ResetDetailsPanel()
        {
            StopTimer();
            _selectedShow = null;
            _currentLock = null;
            TxtNoShowSelected.Visibility = Visibility.Visible;
            PanelShowDetails.Visibility = Visibility.Collapsed;
            PanelPurchaseForm.Visibility = Visibility.Visible;
            PanelCheckout.Visibility = Visibility.Collapsed;
        }

        private void Tab_Checked(object sender, RoutedEventArgs e)
        {
            if (ContentBrowse == null || ContentHistory == null) return;
            
            if (sender == TabBrowse)
            {
                ContentBrowse.Visibility = Visibility.Visible;
                ContentHistory.Visibility = Visibility.Collapsed;
                LoadShows();
            }
            else if (sender == TabHistory)
            {
                ContentBrowse.Visibility = Visibility.Collapsed;
                ContentHistory.Visibility = Visibility.Visible;
                LoadHistory();
            }
        }

        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            ResetDetailsPanel();
            // Clear parent MainWindow view
            var mainWindow = Window.GetWindow(this) as MainWindow;
            mainWindow?.LogOut();
        }

        private void GridShows_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_currentLock != null)
            {
                // Warn or release lock
                ReleaseCurrentLockDirectly();
            }

            _selectedShow = GridShows.SelectedItem as Show;
            if (_selectedShow == null)
            {
                ResetDetailsPanel();
                return;
            }

            TxtNoShowSelected.Visibility = Visibility.Collapsed;
            PanelShowDetails.Visibility = Visibility.Visible;

            DetailsName.Text = _selectedShow.Name;
            DetailsDesc.Text = _selectedShow.Description;
            DetailsVenue.Text = $"Venue: {_selectedShow.Venue}";
            DetailsDateTime.Text = $"Date & Time: {_selectedShow.DateTime:MM/dd/yyyy hh:mm tt}";
            
            UpdateSeatsDisplay();

            PanelPurchaseForm.Visibility = Visibility.Visible;
            PanelCheckout.Visibility = Visibility.Collapsed;
            TxtQty.Text = "1";
        }

        private void UpdateSeatsDisplay()
        {
            if (_selectedShow == null) return;
            if (_selectedShow.RemainingSeatCapacity <= 0)
            {
                DetailsSeats.Text = "Fully Booked / Sold Out";
                BtnBook.IsEnabled = false;
                BtnBook.Content = "Sold Out";
            }
            else
            {
                DetailsSeats.Text = $"Seats remaining: {_selectedShow.RemainingSeatCapacity} / {_selectedShow.SeatCapacity}";
                BtnBook.IsEnabled = true;
                BtnBook.Content = "Proceed to Checkout";
            }
        }

        private void BtnQtyDec_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(TxtQty.Text, out int qty) && qty > 1)
            {
                TxtQty.Text = (qty - 1).ToString();
            }
        }

        private void BtnQtyInc_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(TxtQty.Text, out int qty) && _selectedShow != null && qty < _selectedShow.RemainingSeatCapacity)
            {
                TxtQty.Text = (qty + 1).ToString();
            }
        }

        private async void BtnBook_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedShow == null || CurrentUser == null) return;
            if (!int.TryParse(TxtQty.Text, out int qty) || qty <= 0)
            {
                MessageBox.Show("Please enter a valid quantity.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            using (var db = new ApplicationDbContext())
            {
                var dbShow = await db.Shows.FindAsync(_selectedShow.Id);
                if (dbShow == null || dbShow.Status != "Published")
                {
                    MessageBox.Show("Show no longer available.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    ResetDetailsPanel();
                    LoadShows();
                    return;
                }

                if (qty > dbShow.RemainingSeatCapacity)
                {
                    MessageBox.Show($"Not enough seats available. Only {dbShow.RemainingSeatCapacity} remaining.", "Capacity Limit", MessageBoxButton.OK, MessageBoxImage.Warning);
                    _selectedShow = dbShow;
                    UpdateSeatsDisplay();
                    return;
                }

                // Deduct show seats
                dbShow.RemainingSeatCapacity -= qty;

                // Create hold
                _currentLock = new SeatLock
                {
                    ShowId = dbShow.Id,
                    TicketQuantity = qty,
                    SessionId = "WPF_" + CurrentUser.Id,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                    IsReleased = false
                };

                db.SeatLocks.Add(_currentLock);
                await db.SaveChangesAsync();

                // Refresh listings capacity
                _selectedShow = dbShow;
                LoadShows();

                // Swap panel states
                PanelPurchaseForm.Visibility = Visibility.Collapsed;
                PanelCheckout.Visibility = Visibility.Visible;
                TxtCheckoutTotal.Text = $"Total Price: ${(qty * dbShow.TicketPrice):C}";

                StartTimer();
            }
        }

        private void StartTimer()
        {
            StopTimer();
            _checkoutTimer = new DispatcherTimer();
            _checkoutTimer.Interval = TimeSpan.FromSeconds(1);
            _checkoutTimer.Tick += CheckoutTimer_Tick;
            _checkoutTimer.Start();
        }

        private void StopTimer()
        {
            if (_checkoutTimer != null)
            {
                _checkoutTimer.Stop();
                _checkoutTimer = null;
            }
        }

        private void CheckoutTimer_Tick(object? sender, EventArgs e)
        {
            if (_currentLock == null) return;
            var diff = _currentLock.ExpiresAt - DateTime.UtcNow;
            if (diff <= TimeSpan.Zero)
            {
                StopTimer();
                TxtTimer.Text = "Expired!";
                MessageBox.Show("Checkout session timed out (10-minute limit). Reserved seats have been released.", "Session Expired", MessageBoxButton.OK, MessageBoxImage.Warning);
                ReleaseCurrentLockDirectly();
                return;
            }

            TxtTimer.Text = $"Seats locked: {diff.Minutes:D2}:{diff.Seconds:D2}";
        }

        private async void ReleaseCurrentLockDirectly()
        {
            if (_currentLock == null) return;
            StopTimer();

            using (var db = new ApplicationDbContext())
            {
                var dbLock = await db.SeatLocks.FindAsync(_currentLock.Id);
                if (dbLock != null && !dbLock.IsReleased)
                {
                    dbLock.IsReleased = true;
                    var dbShow = await db.Shows.FindAsync(dbLock.ShowId);
                    if (dbShow != null)
                    {
                        dbShow.RemainingSeatCapacity = Math.Min(dbShow.SeatCapacity, dbShow.RemainingSeatCapacity + dbLock.TicketQuantity);
                    }
                    await db.SaveChangesAsync();
                }
            }

            _currentLock = null;
            LoadShows();
            ResetDetailsPanel();
        }

        private async void BtnPaySuccess_Click(object sender, RoutedEventArgs e)
        {
            if (_currentLock == null || CurrentUser == null) return;
            StopTimer();

            using (var db = new ApplicationDbContext())
            {
                var dbLock = await db.SeatLocks.FindAsync(_currentLock.Id);
                if (dbLock == null || dbLock.IsReleased || DateTime.UtcNow > dbLock.ExpiresAt)
                {
                    MessageBox.Show("Hold expired. Please try booking again.", "Hold Expired", MessageBoxButton.OK, MessageBoxImage.Warning);
                    ReleaseCurrentLockDirectly();
                    return;
                }

                // Generate booking ref (BR-17)
                string refNum = GenerateRefNumber(db);

                var booking = new Booking
                {
                    ReferenceNumber = refNum,
                    ShowId = dbLock.ShowId,
                    BookingDate = DateTime.UtcNow,
                    TicketQuantity = dbLock.TicketQuantity,
                    TotalPrice = dbLock.TicketQuantity * db.Shows.Find(dbLock.ShowId)!.TicketPrice,
                    BookingStatus = "Confirmed",
                    BookingChannel = "Online",
                    VisitorAccountId = CurrentUser.Id
                };

                dbLock.IsReleased = true;
                db.Bookings.Add(booking);
                await db.SaveChangesAsync();

                MessageBox.Show($"Booking confirmed! Reference number: {refNum}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                _currentLock = null;
                LoadShows();
                ResetDetailsPanel();
            }
        }

        private void BtnPayFail_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Payment simulation failed or order cancelled. Seats released.", "Cancelled", MessageBoxButton.OK, MessageBoxImage.Error);
            ReleaseCurrentLockDirectly();
        }

        private string GenerateRefNumber(ApplicationDbContext db)
        {
            var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            while (true)
            {
                var refNum = new string(Enumerable.Repeat(chars, 10)
                    .Select(s => s[random.Next(s.Length)]).ToArray());
                if (!db.Bookings.Any(b => b.ReferenceNumber == refNum))
                    return refNum;
            }
        }

        private async void BtnCancelBooking_Click(object sender, RoutedEventArgs e)
        {
            var booking = GridHistory.SelectedItem as Booking;
            if (booking == null)
            {
                MessageBox.Show("Please select a booking from the history table to cancel.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (booking.BookingStatus == "Cancelled")
            {
                MessageBox.Show("This booking is already cancelled.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (booking.Show != null && booking.Show.DateTime <= DateTime.Now)
            {
                MessageBox.Show("You cannot cancel bookings for shows that have already taken place.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var confirm = MessageBox.Show($"Are you sure you want to cancel booking {booking.ReferenceNumber}? This will restore available show capacity.", "Confirm Cancellation", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm == MessageBoxResult.No) return;

            using (var db = new ApplicationDbContext())
            {
                var dbBooking = await db.Bookings.Include(b => b.Show).FirstOrDefaultAsync(b => b.Id == booking.Id);
                if (dbBooking != null && dbBooking.BookingStatus == "Confirmed")
                {
                    dbBooking.BookingStatus = "Cancelled";
                    if (dbBooking.Show != null)
                    {
                        dbBooking.Show.RemainingSeatCapacity = Math.Min(dbBooking.Show.SeatCapacity, dbBooking.Show.RemainingSeatCapacity + dbBooking.TicketQuantity);
                    }
                    await db.SaveChangesAsync();
                    MessageBox.Show("Booking cancelled successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadHistory();
                }
            }
        }

        private void BtnFilter_Click(object sender, RoutedEventArgs e)
        {
            LoadShows(DpFilterDate.SelectedDate);
        }

        private void BtnClearFilter_Click(object sender, RoutedEventArgs e)
        {
            DpFilterDate.SelectedDate = null;
            LoadShows();
        }
    }
}
