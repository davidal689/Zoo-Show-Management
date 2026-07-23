using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using Zoo_Show_Mnm.Data;
using Zoo_Show_Mnm.Models;

namespace Zoo_Show_Mnm.Views
{
    public partial class CashierDashboard : UserControl
    {
        public User? CurrentUser { get; set; }
        
        private Show? _selectedShow;

        public CashierDashboard()
        {
            InitializeComponent();
        }

        public void LoadData()
        {
            LoadShows();
            ResetPOS();
            TxtSearchQuery.Text = "";
            GridSearch.ItemsSource = null;
        }

        private void LoadShows()
        {
            using (var db = new ApplicationDbContext())
            {
                GridShows.ItemsSource = db.Shows.Where(s => s.Status == "Published").OrderBy(s => s.DateTime).ToList();
            }
        }

        private void ResetPOS()
        {
            _selectedShow = null;
            TxtNoShowSelected.Visibility = Visibility.Visible;
            PanelIssueTickets.Visibility = Visibility.Collapsed;
            TxtQty.Text = "1";
            TxtGuestName.Text = "";
        }

        private void Tab_Checked(object sender, RoutedEventArgs e)
        {
            if (ContentPOS == null || ContentSearch == null) return;

            if (sender == TabPOS)
            {
                ContentPOS.Visibility = Visibility.Visible;
                ContentSearch.Visibility = Visibility.Collapsed;
                LoadShows();
            }
            else if (sender == TabSearch)
            {
                ContentPOS.Visibility = Visibility.Collapsed;
                ContentSearch.Visibility = Visibility.Visible;
            }
        }

        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            ResetPOS();
            var mainWindow = Window.GetWindow(this) as MainWindow;
            mainWindow?.LogOut();
        }

        private void GridShows_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedShow = GridShows.SelectedItem as Show;
            if (_selectedShow == null)
            {
                ResetPOS();
                return;
            }

            TxtNoShowSelected.Visibility = Visibility.Collapsed;
            PanelIssueTickets.Visibility = Visibility.Visible;

            DetailsName.Text = _selectedShow.Name;
            UpdateSeatsDisplay();
            TxtQty.Text = "1";
            TxtGuestName.Text = "";
            calculateTotal();
        }

        private void UpdateSeatsDisplay()
        {
            if (_selectedShow == null) return;
            DetailsSeats.Text = $"Counter seats remaining: {_selectedShow.RemainingSeatCapacity} / {_selectedShow.SeatCapacity}";
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

        private void TxtQty_TextChanged(object sender, TextChangedEventArgs e)
        {
            calculateTotal();
        }

        private void calculateTotal()
        {
            if (TxtTotal == null) return;
            if (_selectedShow != null && int.TryParse(TxtQty.Text, out int qty) && qty > 0)
            {
                TxtTotal.Text = $"{(qty * _selectedShow.TicketPrice):C}";
            }
            else
            {
                TxtTotal.Text = "$0.00";
            }
        }

        private async void BtnIssueTickets_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedShow == null || CurrentUser == null) return;

            string guestName = TxtGuestName.Text.Trim();
            if (string.IsNullOrEmpty(guestName)) guestName = "Walk-In Customer";

            if (!int.TryParse(TxtQty.Text, out int qty) || qty <= 0)
            {
                MessageBox.Show("Please enter a valid ticket quantity.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (RadioFail.IsChecked == true)
            {
                MessageBox.Show("Payment simulation failure. Cannot complete ticketing.", "Payment Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            using (var db = new ApplicationDbContext())
            {
                var dbShow = await db.Shows.FindAsync(_selectedShow.Id);
                if (dbShow == null || dbShow.Status != "Published")
                {
                    MessageBox.Show("Show no longer available.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    ResetPOS();
                    LoadShows();
                    return;
                }

                if (qty > dbShow.RemainingSeatCapacity)
                {
                    MessageBox.Show($"Requested quantity exceeds capacity. Only {dbShow.RemainingSeatCapacity} left.", "Capacity Limit", MessageBoxButton.OK, MessageBoxImage.Warning);
                    _selectedShow = dbShow;
                    UpdateSeatsDisplay();
                    calculateTotal();
                    return;
                }

                string refNum = GenerateRefNumber(db);

                var booking = new Booking
                {
                    ReferenceNumber = refNum,
                    ShowId = dbShow.Id,
                    BookingDate = DateTime.UtcNow,
                    TicketQuantity = qty,
                    TotalPrice = qty * dbShow.TicketPrice,
                    BookingStatus = "Confirmed",
                    BookingChannel = "Counter",
                    IssuingCashierId = CurrentUser.Id,
                    WalkInVisitorName = guestName
                };

                dbShow.RemainingSeatCapacity -= qty;
                db.Bookings.Add(booking);
                await db.SaveChangesAsync();

                MessageBox.Show($"Ticket issued successfully! Reference: {refNum}", "POS Confirm", MessageBoxButton.OK, MessageBoxImage.Information);
                
                ResetPOS();
                LoadShows();
            }
        }

        private async void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            string query = TxtSearchQuery.Text.Trim().ToLower();
            if (string.IsNullOrEmpty(query)) return;

            using (var db = new ApplicationDbContext())
            {
                var results = await db.Bookings
                    .Include(b => b.Show)
                    .Include(b => b.VisitorAccount)
                    .Where(b => b.ReferenceNumber.ToLower() == query || 
                               (b.VisitorAccount != null && b.VisitorAccount.Name.ToLower().Contains(query)) ||
                               (b.WalkInVisitorName != null && b.WalkInVisitorName.ToLower().Contains(query)))
                    .OrderByDescending(b => b.BookingDate)
                    .ToListAsync();

                GridSearch.ItemsSource = results;
            }
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
    }
}
