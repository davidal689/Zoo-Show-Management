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
        private readonly System.Collections.Generic.List<int> _selectedSeatIndices = new System.Collections.Generic.List<int>();

        public VisitorDashboard()
        {
            InitializeComponent();
        }

        public void LoadData()
        {
            if (CurrentUser == null)
            {
                TabHistory.Visibility = Visibility.Collapsed;
                BtnLogout.Content = "Đăng nhập / Đăng ký";
            }
            else
            {
                TabHistory.Visibility = Visibility.Visible;
                BtnLogout.Content = "Đăng xuất";
            }

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
                    .Where(b => b.UserAccountId == CurrentUser.Id)
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
            if (PanelSeatMap != null)
            {
                PanelSeatMap.Children.Clear();
            }
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
            var mainWindow = Window.GetWindow(this) as MainWindow;
            if (CurrentUser == null)
            {
                mainWindow?.ShowLoginScreen();
            }
            else
            {
                mainWindow?.LogOut();
            }
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
            DetailsVenue.Text = $"Địa điểm: {_selectedShow.Venue}";
            DetailsDateTime.Text = $"Thời gian: {_selectedShow.DateTime:MM/dd/yyyy hh:mm tt}";
            
            _selectedSeatIndices.Clear();
            UpdateSeatsDisplay();

            PanelPurchaseForm.Visibility = Visibility.Visible;
            PanelCheckout.Visibility = Visibility.Collapsed;
            TxtQty.Text = "0";
        }

        private Border CreateSeatVisual(System.Windows.Media.Brush fill, System.Windows.Media.Brush border)
        {
            return new Border
            {
                Width = 12,
                Height = 12,
                Background = fill,
                BorderBrush = border,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                Margin = new Thickness(2)
            };
        }

        private void UpdateSeatsDisplay()
        {
            if (_selectedShow == null) return;
            
            // Render Dynamic Seat Map
            PanelSeatMap.Children.Clear();
            
            int totalSeats = _selectedShow.SeatCapacity;
            var occupiedConfirmed = new System.Collections.Generic.HashSet<int>();
            var occupiedLocked = new System.Collections.Generic.HashSet<int>();

            try
            {
                using (var db = new ApplicationDbContext())
                {
                    var bookings = db.Bookings
                        .Where(b => b.ShowId == _selectedShow.Id && b.BookingStatus == "Confirmed")
                        .ToList();

                    var unassignedConfirmedQty = new System.Collections.Generic.List<int>();
                    foreach (var b in bookings)
                    {
                        if (!string.IsNullOrEmpty(b.SelectedSeats))
                        {
                            foreach (var part in b.SelectedSeats.Split(','))
                            {
                                if (int.TryParse(part, out int seatIdx))
                                    occupiedConfirmed.Add(seatIdx);
                            }
                        }
                        else
                        {
                            unassignedConfirmedQty.Add(b.TicketQuantity);
                        }
                    }

                    var locks = db.SeatLocks
                        .Where(l => l.ShowId == _selectedShow.Id && !l.IsReleased && l.ExpiresAt > DateTime.UtcNow)
                        .ToList();

                    var unassignedLockedQty = new System.Collections.Generic.List<int>();
                    foreach (var l in locks)
                    {
                        if (_currentLock != null && l.Id == _currentLock.Id) continue;

                        if (!string.IsNullOrEmpty(l.SelectedSeats))
                        {
                            foreach (var part in l.SelectedSeats.Split(','))
                            {
                                if (int.TryParse(part, out int seatIdx))
                                    occupiedLocked.Add(seatIdx);
                            }
                        }
                        else
                        {
                            unassignedLockedQty.Add(l.TicketQuantity);
                        }
                    }

                    // Also account for Pending bookings that are not in the current session
                    var pendingBookings = db.Bookings
                        .Where(b => b.ShowId == _selectedShow.Id && b.BookingStatus == "Pending")
                        .ToList();
                    foreach (var pb in pendingBookings)
                    {
                        // Check if this pending booking's seats are already covered in activeLocks
                        // If not, we block them as locked
                        if (!string.IsNullOrEmpty(pb.SelectedSeats))
                        {
                            foreach (var part in pb.SelectedSeats.Split(','))
                            {
                                if (int.TryParse(part, out int seatIdx))
                                    occupiedLocked.Add(seatIdx);
                            }
                        }
                    }

                    // Dynamically allocate unassigned seats
                    int capacity = _selectedShow.SeatCapacity;
                    foreach (var qty in unassignedConfirmedQty)
                    {
                        int allocated = 0;
                        for (int i = 0; i < capacity && allocated < qty; i++)
                        {
                            if (!occupiedConfirmed.Contains(i) && !occupiedLocked.Contains(i))
                            {
                                occupiedConfirmed.Add(i);
                                allocated++;
                            }
                        }
                    }
                    foreach (var qty in unassignedLockedQty)
                    {
                        int allocated = 0;
                        for (int i = 0; i < capacity && allocated < qty; i++)
                        {
                            if (!occupiedConfirmed.Contains(i) && !occupiedLocked.Contains(i))
                            {
                                occupiedLocked.Add(i);
                                allocated++;
                            }
                        }
                    }
                }
            }
            catch
            {
                // Fallback using capacities
                int confirmedCount = _selectedShow.SeatCapacity - _selectedShow.RemainingSeatCapacity;
                for (int i = 0; i < confirmedCount; i++)
                {
                    occupiedConfirmed.Add(i);
                }
            }

            // Draw interactive seats
            for (int i = 0; i < totalSeats; i++)
            {
                int seatIndex = i;
                System.Windows.Media.Brush fill = System.Windows.Media.Brushes.White;
                System.Windows.Media.Brush border = System.Windows.Media.Brushes.DarkGray;
                bool isSelectable = true;

                if (occupiedConfirmed.Contains(seatIndex))
                {
                    fill = System.Windows.Media.Brushes.Red;
                    border = System.Windows.Media.Brushes.DarkRed;
                    isSelectable = false;
                }
                else if (occupiedLocked.Contains(seatIndex))
                {
                    fill = System.Windows.Media.Brushes.Yellow;
                    border = System.Windows.Media.Brushes.Goldenrod;
                    isSelectable = false;
                }
                else if (_selectedSeatIndices.Contains(seatIndex))
                {
                    fill = System.Windows.Media.Brushes.DodgerBlue;
                    border = System.Windows.Media.Brushes.DarkBlue;
                }

                var seatVisual = CreateSeatVisual(fill, border);
                
                if (isSelectable)
                {
                    seatVisual.Cursor = System.Windows.Input.Cursors.Hand;
                    seatVisual.MouseDown += (s, e) =>
                    {
                        if (_selectedSeatIndices.Contains(seatIndex))
                        {
                            _selectedSeatIndices.Remove(seatIndex);
                        }
                        else
                        {
                            _selectedSeatIndices.Add(seatIndex);
                        }
                        TxtQty.Text = _selectedSeatIndices.Count.ToString();
                        UpdateSeatsDisplay();
                    };
                }

                PanelSeatMap.Children.Add(seatVisual);
            }

            if (_selectedShow.RemainingSeatCapacity <= 0)
            {
                DetailsSeats.Text = "Hết ghế / Bán hết vé";
                BtnBook.IsEnabled = false;
                BtnBook.Content = "Bán hết vé";
            }
            else
            {
                DetailsSeats.Text = $"Ghế còn trống: {_selectedShow.RemainingSeatCapacity} / {_selectedShow.SeatCapacity} (Giá vé: {_selectedShow.TicketPrice:C})";
                BtnBook.IsEnabled = true;
                BtnBook.Content = "Tiến Hành Đặt Vé";
            }
        }

        private void BtnQtyDec_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Vui lòng click chọn ghế trực tiếp trên sơ đồ.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnQtyInc_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Vui lòng click chọn ghế trực tiếp trên sơ đồ.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void BtnBook_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedShow == null) return;

            if (CurrentUser == null)
            {
                MessageBox.Show("Vui lòng đăng nhập hoặc đăng ký tài khoản để tiến hành đặt vé.", "Yêu cầu đăng nhập", MessageBoxButton.OK, MessageBoxImage.Information);
                var mainWindow = Window.GetWindow(this) as MainWindow;
                mainWindow?.ShowLoginScreen();
                return;
            }

            if (_selectedSeatIndices.Count == 0)
            {
                MessageBox.Show("Vui lòng chọn ít nhất một ghế trên sơ đồ.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int qty = _selectedSeatIndices.Count;

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

                string selectedSeatsStr = string.Join(",", _selectedSeatIndices);

                // Create hold
                _currentLock = new SeatLock
                {
                    ShowId = dbShow.Id,
                    TicketQuantity = qty,
                    LockedBySession = "WPF_" + CurrentUser.Id,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                    IsReleased = false,
                    SelectedSeats = selectedSeatsStr
                };

                db.SeatLocks.Add(_currentLock);
                await db.SaveChangesAsync();

                // Create Pending Booking (BR-17)
                string refNum = GenerateRefNumber(db);
                var booking = new Booking
                {
                    ReferenceNumber = refNum,
                    ShowId = dbShow.Id,
                    BookingDate = DateTime.UtcNow,
                    TicketQuantity = qty,
                    TotalPrice = qty * dbShow.TicketPrice,
                    BookingStatus = "Pending",
                    BookingChannel = "Online",
                    UserAccountId = CurrentUser.Id,
                    SelectedSeats = selectedSeatsStr
                };

                db.Bookings.Add(booking);
                await db.SaveChangesAsync();

                // Clear selected seats so they are not kept in memory
                _selectedSeatIndices.Clear();

                // Swap to History tab
                TabHistory.IsChecked = true;
                
                ContentBrowse.Visibility = Visibility.Collapsed;
                ContentHistory.Visibility = Visibility.Visible;
                LoadHistory();
                ResetDetailsPanel();
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

                    // Cancel associated pending booking
                    if (CurrentUser != null)
                    {
                        var pendingBooking = await db.Bookings
                            .FirstOrDefaultAsync(b => b.ShowId == dbLock.ShowId && 
                                                      b.UserAccountId == CurrentUser.Id && 
                                                      b.BookingStatus == "Pending" && 
                                                      b.SelectedSeats == dbLock.SelectedSeats);
                        if (pendingBooking != null)
                        {
                            pendingBooking.BookingStatus = "Cancelled";
                        }
                    }

                    await db.SaveChangesAsync();
                }
            }

            _currentLock = null;
            _selectedSeatIndices.Clear();
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

                // Confirm pending booking
                var pendingBooking = await db.Bookings
                    .FirstOrDefaultAsync(b => b.ShowId == dbLock.ShowId && 
                                              b.UserAccountId == CurrentUser.Id && 
                                              b.BookingStatus == "Pending" && 
                                              b.SelectedSeats == dbLock.SelectedSeats);
                if (pendingBooking != null)
                {
                    pendingBooking.BookingStatus = "Confirmed";
                }

                dbLock.IsReleased = true;
                await db.SaveChangesAsync();

                MessageBox.Show($"Booking confirmed! Reference number: {pendingBooking?.ReferenceNumber}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                _currentLock = null;
                _selectedSeatIndices.Clear();
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
                if (dbBooking != null && (dbBooking.BookingStatus == "Confirmed" || dbBooking.BookingStatus == "Pending"))
                {
                    dbBooking.BookingStatus = "Cancelled";
                    if (dbBooking.Show != null)
                    {
                        dbBooking.Show.RemainingSeatCapacity = Math.Min(dbBooking.Show.SeatCapacity, dbBooking.Show.RemainingSeatCapacity + dbBooking.TicketQuantity);
                    }

                    // Also release associated lock if it was pending
                    var associatedLock = await db.SeatLocks
                        .FirstOrDefaultAsync(l => l.ShowId == dbBooking.ShowId && 
                                                  !l.IsReleased && 
                                                  l.SelectedSeats == dbBooking.SelectedSeats);
                    if (associatedLock != null)
                    {
                        associatedLock.IsReleased = true;
                    }

                    await db.SaveChangesAsync();
                    MessageBox.Show("Booking cancelled successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadHistory();
                }
            }
        }

        private async void BtnPayPending_Click(object sender, RoutedEventArgs e)
        {
            var booking = GridHistory.SelectedItem as Booking;
            if (booking == null)
            {
                MessageBox.Show("Vui lòng chọn một vé có trạng thái 'Pending' từ bảng lịch sử để thanh toán.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (booking.BookingStatus != "Pending")
            {
                MessageBox.Show("Vé này không ở trạng thái Chờ thanh toán (Pending).", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirm = MessageBox.Show($"Xác nhận thanh toán vé {booking.ReferenceNumber} với số tiền {booking.TotalPrice:C}?", "Thanh Toán", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm == MessageBoxResult.No) return;

            using (var db = new ApplicationDbContext())
            {
                var dbBooking = await db.Bookings.FindAsync(booking.Id);
                if (dbBooking != null && dbBooking.BookingStatus == "Pending")
                {
                    dbBooking.BookingStatus = "Confirmed";

                    // Release associated lock
                    var associatedLock = await db.SeatLocks
                        .FirstOrDefaultAsync(l => l.ShowId == dbBooking.ShowId && 
                                                  !l.IsReleased && 
                                                  l.SelectedSeats == dbBooking.SelectedSeats);
                    if (associatedLock != null)
                    {
                        associatedLock.IsReleased = true;
                    }

                    await db.SaveChangesAsync();
                    MessageBox.Show("Thanh toán thành công! Vé đã được xác nhận (Confirmed).", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
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
