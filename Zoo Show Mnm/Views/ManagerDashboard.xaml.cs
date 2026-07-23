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
    public partial class ManagerDashboard : UserControl
    {
        public User? CurrentUser { get; set; }
        
        private Show? _selectedShow;
        private bool _isNewMode = false;

        public ManagerDashboard()
        {
            InitializeComponent();
        }

        public void LoadData()
        {
            LoadShows();
            ResetForm();
        }

        private void LoadShows()
        {
            using (var db = new ApplicationDbContext())
            {
                GridShows.ItemsSource = db.Shows.OrderByDescending(s => s.DateTime).ToList();
            }
        }

        private void ResetForm()
        {
            _selectedShow = null;
            _isNewMode = false;
            TxtNoSelection.Visibility = Visibility.Visible;
            FormShow.Visibility = Visibility.Collapsed;
            ChKConflict.IsChecked = false;
        }

        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            ResetForm();
            var mainWindow = Window.GetWindow(this) as MainWindow;
            mainWindow?.LogOut();
        }

        private void GridShows_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedShow = GridShows.SelectedItem as Show;
            if (_selectedShow == null)
            {
                ResetForm();
                return;
            }

            _isNewMode = false;
            TxtNoSelection.Visibility = Visibility.Collapsed;
            FormShow.Visibility = Visibility.Visible;

            TxtShowName.Text = _selectedShow.Name;
            TxtDescription.Text = _selectedShow.Description;
            TxtDateTime.Text = _selectedShow.DateTime.ToString("MM/dd/yyyy hh:mm tt");
            TxtCapacity.Text = _selectedShow.SeatCapacity.ToString();
            TxtPrice.Text = _selectedShow.TicketPrice.ToString("F2");
            ChKConflict.IsChecked = false;

            // Set Venue Combobox
            CbVenue.SelectedIndex = _selectedShow.Venue switch
            {
                "Splash Amphitheater" => 0,
                "Eagle Ridge Arena" => 1,
                "Savannah Lookout" => 2,
                "Nocturnal Jungle Dome" => 3,
                _ => -1
            };

            // Set Status Combobox
            CbStatus.SelectedIndex = _selectedShow.Status switch
            {
                "Draft" => 0,
                "Published" => 1,
                _ => -1
            };
        }

        private void BtnNewShow_Click(object sender, RoutedEventArgs e)
        {
            GridShows.SelectedItem = null;
            _selectedShow = new Show { DateTime = DateTime.Today.AddDays(1).AddHours(14) };
            _isNewMode = true;

            TxtNoSelection.Visibility = Visibility.Collapsed;
            FormShow.Visibility = Visibility.Visible;

            TxtShowName.Text = "";
            TxtDescription.Text = "";
            TxtDateTime.Text = _selectedShow.DateTime.ToString("MM/dd/yyyy hh:mm tt");
            TxtCapacity.Text = "100";
            TxtPrice.Text = "15.00";
            CbVenue.SelectedIndex = 0;
            CbStatus.SelectedIndex = 0;
            ChKConflict.IsChecked = false;
        }

        private async void BtnSaveShow_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedShow == null || CurrentUser == null) return;

            string name = TxtShowName.Text.Trim();
            string desc = TxtDescription.Text.Trim();
            string dtStr = TxtDateTime.Text.Trim();
            string capStr = TxtCapacity.Text.Trim();
            string priceStr = TxtPrice.Text.Trim();

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(desc) || CbVenue.SelectedIndex < 0 || CbStatus.SelectedIndex < 0)
            {
                MessageBox.Show("All fields are required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!DateTime.TryParse(dtStr, out DateTime dt))
            {
                MessageBox.Show("Please enter a valid Date & Time (e.g., MM/dd/yyyy hh:mm AM/PM).", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(capStr, out int cap) || cap <= 0)
            {
                MessageBox.Show("Capacity must be a positive integer.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!decimal.TryParse(priceStr, out decimal price) || price <= 0)
            {
                MessageBox.Show("Price must be a positive number.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string venue = (CbVenue.SelectedItem as ComboBoxItem)!.Content.ToString()!;
            string status = (CbStatus.SelectedItem as ComboBoxItem)!.Content.ToString()!;

            // Verify if editing past show (BR-30)
            if (!_isNewMode && _selectedShow.DateTime < DateTime.Now)
            {
                MessageBox.Show("Cannot edit shows that have already taken place.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            using (var db = new ApplicationDbContext())
            {
                // Check Sold Seats validation (BR-16)
                if (!_isNewMode)
                {
                    var originalShow = await db.Shows.FindAsync(_selectedShow.Id);
                    if (originalShow != null)
                    {
                        int soldCount = originalShow.SeatCapacity - originalShow.RemainingSeatCapacity;
                        if (cap < soldCount)
                        {
                            MessageBox.Show($"Cannot reduce capacity below tickets already sold ({soldCount} tickets).", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                    }
                }

                // Check Scheduling Venue Conflict (BR-29)
                bool checkConflict = ChKConflict.IsChecked != true;
                if (checkConflict)
                {
                    bool conflict = await db.Shows.AnyAsync(s => 
                        s.Id != _selectedShow.Id && 
                        s.Status != "Cancelled" &&
                        s.Venue == venue && 
                        s.DateTime >= dt.AddHours(-2) && 
                        s.DateTime <= dt.AddHours(2));

                    if (conflict)
                    {
                        MessageBox.Show("WARNING: There is another show scheduled at the same venue close to this time. Check 'Ignore scheduling venue conflicts' to save anyway.", "Scheduling Conflict", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                if (_isNewMode)
                {
                    var show = new Show
                    {
                        Name = name,
                        Description = desc,
                        DateTime = dt,
                        Venue = venue,
                        SeatCapacity = cap,
                        RemainingSeatCapacity = cap,
                        TicketPrice = price,
                        Status = status
                    };
                    db.Shows.Add(show);
                    await db.SaveChangesAsync();

                    db.AuditLogs.Add(new AuditLog
                    {
                        ActorAccountId = CurrentUser.Id,
                        ActionType = "Show Created",
                        TargetEntity = $"Show ID {show.Id} ({show.Name})",
                        Timestamp = DateTime.UtcNow
                    });
                }
                else
                {
                    var show = await db.Shows.FindAsync(_selectedShow.Id);
                    if (show != null)
                    {
                        int capacityDiff = cap - show.SeatCapacity;
                        show.RemainingSeatCapacity += capacityDiff;

                        show.Name = name;
                        show.Description = desc;
                        show.DateTime = dt;
                        show.Venue = venue;
                        show.SeatCapacity = cap;
                        show.TicketPrice = price;
                        show.Status = status;

                        db.AuditLogs.Add(new AuditLog
                        {
                            ActorAccountId = CurrentUser.Id,
                            ActionType = "Show Edited",
                            TargetEntity = $"Show ID {show.Id} ({show.Name})",
                            Timestamp = DateTime.UtcNow
                        });
                    }
                }

                await db.SaveChangesAsync();
                MessageBox.Show("Show details saved successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadShows();
                ResetForm();
            }
        }

        private async void BtnCancelShow_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedShow == null || _isNewMode || CurrentUser == null)
            {
                MessageBox.Show("Please select an existing show from the schedule table.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (_selectedShow.Status == "Cancelled")
            {
                MessageBox.Show("This show is already cancelled.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (_selectedShow.DateTime < DateTime.Now)
            {
                MessageBox.Show("Cannot cancel shows that have already taken place.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var confirm = MessageBox.Show($"Are you sure you want to cancel '{_selectedShow.Name}'? This will remove the listing and automatically cancel all tickets booked.", "Confirm Cancellation", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm == MessageBoxResult.No) return;

            using (var db = new ApplicationDbContext())
            {
                var show = await db.Shows.FindAsync(_selectedShow.Id);
                if (show != null)
                {
                    show.Status = "Cancelled";
                    show.RemainingSeatCapacity = show.SeatCapacity; // release reservations

                    // Cancel bookings
                    var activeBookings = db.Bookings.Where(b => b.ShowId == show.Id && b.BookingStatus == "Confirmed").ToList();
                    foreach (var booking in activeBookings)
                    {
                        booking.BookingStatus = "Cancelled";
                    }

                    db.AuditLogs.Add(new AuditLog
                    {
                        ActorAccountId = CurrentUser.Id,
                        ActionType = "Show Cancelled",
                        TargetEntity = $"Show ID {show.Id} ({show.Name})",
                        Timestamp = DateTime.UtcNow
                    });

                    await db.SaveChangesAsync();
                    MessageBox.Show("Show cancelled successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadShows();
                    ResetForm();
                }
            }
        }
    }
}
