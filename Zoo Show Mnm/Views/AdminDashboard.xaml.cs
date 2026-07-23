using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.EntityFrameworkCore;
using Zoo_Show_Mnm.Data;
using Zoo_Show_Mnm.Models;
using BCrypt.Net;

namespace Zoo_Show_Mnm.Views
{
    public partial class AdminDashboard : UserControl
    {
        public User? CurrentUser { get; set; }

        public AdminDashboard()
        {
            InitializeComponent();
        }

        public void LoadData()
        {
            LoadStaff();
            LoadAuditLogs();
            ResetReports();
        }

        private void LoadStaff()
        {
            if (CurrentUser == null) return;
            using (var db = new ApplicationDbContext())
            {
                GridUsers.ItemsSource = db.Users
                    .Where(u => u.Id != CurrentUser.Id)
                    .OrderBy(u => u.Role)
                    .ToList();
            }
        }

        private void LoadAuditLogs()
        {
            using (var db = new ApplicationDbContext())
            {
                GridAudit.ItemsSource = db.AuditLogs
                    .Include(l => l.ActorAccount)
                    .OrderByDescending(l => l.Timestamp)
                    .Take(200)
                    .ToList();
            }
        }

        private void ResetReports()
        {
            CbReportType.SelectedIndex = 0;
            DpStart.SelectedDate = DateTime.Today.AddDays(-30);
            DpEnd.SelectedDate = DateTime.Today.AddDays(7);
            GridRepTransactions.ItemsSource = null;
            GridRepAttendance.ItemsSource = null;
            TxtReportStat1.Text = "Tổng lượt đặt: 0";
            TxtReportStat2.Text = "Tổng doanh thu: $0.00";
        }

        private void Tab_Checked(object sender, RoutedEventArgs e)
        {
            if (ContentStaff == null || ContentReports == null || ContentAudit == null) return;

            if (sender == TabStaff)
            {
                ContentStaff.Visibility = Visibility.Visible;
                ContentReports.Visibility = Visibility.Collapsed;
                ContentAudit.Visibility = Visibility.Collapsed;
                LoadStaff();
            }
            else if (sender == TabReports)
            {
                ContentStaff.Visibility = Visibility.Collapsed;
                ContentReports.Visibility = Visibility.Visible;
                ContentAudit.Visibility = Visibility.Collapsed;
                ResetReports();
            }
            else if (sender == TabAudit)
            {
                ContentStaff.Visibility = Visibility.Collapsed;
                ContentReports.Visibility = Visibility.Collapsed;
                ContentAudit.Visibility = Visibility.Visible;
                LoadAuditLogs();
            }
        }

        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Window.GetWindow(this) as MainWindow;
            mainWindow?.LogOut();
        }

        private async void BtnCreateStaff_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentUser == null) return;

            string name = TxtStaffName.Text.Trim();
            string username = TxtStaffUsername.Text.Trim(); // Uses TxtStaffUsername instead of TxtStaffEmail
            string password = TxtStaffPassword.Text.Trim();

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password) || CbStaffRole.SelectedIndex < 0)
            {
                MessageBox.Show("Vui lòng điền đầy đủ các thông tin.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (password.Length < 8 || !password.Any(char.IsLetter) || !password.Any(char.IsDigit))
            {
                MessageBox.Show("Mật khẩu tạm thời phải tối thiểu 8 ký tự, chứa ít nhất 1 chữ cái và 1 chữ số.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string role = (CbStaffRole.SelectedItem as ComboBoxItem)!.Content.ToString()!;

            using (var db = new ApplicationDbContext())
            {
                // Check username exists instead of email
                var exists = await db.Users.AnyAsync(u => u.Username.ToLower() == username.ToLower());
                if (exists)
                {
                    MessageBox.Show("Tên đăng nhập này đã tồn tại trong hệ thống.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var staff = new User
                {
                    Name = name,
                    Username = username.ToLower(),
                    Role = role,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                    IsTemporaryPassword = true,
                    IsDeactivated = false
                };

                db.Users.Add(staff);
                await db.SaveChangesAsync();

                db.AuditLogs.Add(new AuditLog
                {
                    ActorAccountId = CurrentUser.Id,
                    ActionType = "Account Created",
                    TargetEntity = $"User ID {staff.Id} ({staff.Username} - Role: {staff.Role})",
                    Timestamp = DateTime.UtcNow
                });
                await db.SaveChangesAsync();

                MessageBox.Show($"Tài khoản nhân viên '{staff.Name}' được tạo thành công với mật khẩu tạm.", "Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);
                
                TxtStaffName.Text = "";
                TxtStaffUsername.Text = "";
                TxtStaffPassword.Text = "";
                CbStaffRole.SelectedIndex = -1;

                LoadStaff();
                LoadAuditLogs();
            }
        }

        private async void BtnToggleDeactivate_Click(object sender, RoutedEventArgs e)
        {
            var user = GridUsers.SelectedItem as User;
            if (user == null || CurrentUser == null)
            {
                MessageBox.Show("Vui lòng chọn nhân viên cần thao tác từ bảng trước.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var confirm = MessageBox.Show($"Xác nhận thay đổi trạng thái hoạt động của nhân viên '{user.Name}'?", "Xác Nhận", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm == MessageBoxResult.No) return;

            using (var db = new ApplicationDbContext())
            {
                var dbUser = await db.Users.FindAsync(user.Id);
                if (dbUser != null)
                {
                    dbUser.IsDeactivated = !dbUser.IsDeactivated;
                    await db.SaveChangesAsync();

                    db.AuditLogs.Add(new AuditLog
                    {
                        ActorAccountId = CurrentUser.Id,
                        ActionType = dbUser.IsDeactivated ? "Account Deactivated" : "Account Reactivated",
                        TargetEntity = $"User ID {dbUser.Id} ({dbUser.Username})",
                        Timestamp = DateTime.UtcNow
                    });
                    await db.SaveChangesAsync();

                    LoadStaff();
                    LoadAuditLogs();
                }
            }
        }

        private void CbReportType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GridRepTransactions == null || GridRepAttendance == null) return;
            GridRepTransactions.Visibility = Visibility.Collapsed;
            GridRepAttendance.Visibility = Visibility.Collapsed;

            if (CbReportType.SelectedIndex == 0)
            {
                GridRepTransactions.Visibility = Visibility.Visible;
            }
            else if (CbReportType.SelectedIndex == 1)
            {
                GridRepAttendance.Visibility = Visibility.Visible;
            }
        }

        private async void BtnGenerateReport_Click(object sender, RoutedEventArgs e)
        {
            var start = DpStart.SelectedDate ?? DateTime.Today.AddDays(-30);
            var end = DpEnd.SelectedDate ?? DateTime.Today.AddDays(7);
            var endOfDay = end.Date.AddDays(1).AddTicks(-1);

            if (CbReportType.SelectedIndex == 0)
            {
                using (var db = new ApplicationDbContext())
                {
                    var report = await db.Bookings
                        .Include(b => b.Show)
                        .Where(b => b.BookingDate >= start.Date && b.BookingDate <= endOfDay)
                        .OrderByDescending(b => b.BookingDate)
                        .ToListAsync();

                    GridRepTransactions.ItemsSource = report;

                    int totalBookings = report.Count();
                    decimal totalRev = report.Where(b => b.BookingStatus == "Confirmed").Sum(b => b.TotalPrice);
                    
                    TxtReportStat1.Text = $"Tổng lượt đặt: {totalBookings} (Thành công: {report.Count(b => b.BookingStatus == "Confirmed")})";
                    TxtReportStat2.Text = $"Tổng doanh thu: {totalRev:C}";

                    // Calculate best and worst show sales from bookings
                    var confirmedBookings = report.Where(b => b.BookingStatus == "Confirmed" && b.Show != null).ToList();
                    if (confirmedBookings.Any())
                    {
                        var showSales = confirmedBookings
                            .GroupBy(b => b.Show!.Name)
                            .Select(g => new { ShowName = g.Key, TotalSold = g.Sum(b => b.TicketQuantity) })
                            .OrderByDescending(x => x.TotalSold)
                            .ToList();

                        var best = showSales.First();
                        var worst = showSales.Last();

                        TxtBestSeller.Text = $"Show bán chạy nhất: {best.ShowName} ({best.TotalSold} vé)";
                        TxtWorstSeller.Text = $"Show ít vé nhất: {worst.ShowName} ({worst.TotalSold} vé)";
                    }
                    else
                    {
                        TxtBestSeller.Text = "Show bán chạy nhất: Không có dữ liệu";
                        TxtWorstSeller.Text = "Show ít vé nhất: Không có dữ liệu";
                    }
                }
            }
            else if (CbReportType.SelectedIndex == 1)
            {
                using (var db = new ApplicationDbContext())
                {
                    var shows = await db.Shows
                        .Where(s => s.DateTime >= start.Date && s.DateTime <= endOfDay)
                        .OrderBy(s => s.DateTime)
                        .ToListAsync();

                    var repSource = shows.Select(s => {
                        int sold = s.SeatCapacity - s.RemainingSeatCapacity;
                        double util = s.SeatCapacity > 0 ? ((double)sold / s.SeatCapacity) * 100 : 0;
                        return new
                        {
                            s.Name,
                            s.Venue,
                            s.DateTime,
                            s.SeatCapacity,
                            SoldSeats = sold,
                            Utilization = $"{util:F1}%"
                        };
                    }).ToList();

                    ColSold.Binding = new Binding("SoldSeats");
                    ColUtil.Binding = new Binding("Utilization");

                    GridRepAttendance.ItemsSource = repSource;

                    int totalCapacity = shows.Sum(s => s.SeatCapacity);
                    int totalSold = shows.Sum(s => s.SeatCapacity - s.RemainingSeatCapacity);
                    double aggregateUtil = totalCapacity > 0 ? ((double)totalSold / totalCapacity) * 100 : 0;

                    TxtReportStat1.Text = $"Tổng số show: {shows.Count} | Tổng số vé đã bán: {totalSold}";
                    TxtReportStat2.Text = $"Tỉ lệ tham gia trung bình: {aggregateUtil:F1}%";

                    if (shows.Any())
                    {
                        var showSales = shows
                            .Select(s => new { s.Name, Sold = s.SeatCapacity - s.RemainingSeatCapacity })
                            .OrderByDescending(x => x.Sold)
                            .ToList();

                        var best = showSales.First();
                        var worst = showSales.Last();

                        TxtBestSeller.Text = $"Show bán chạy nhất: {best.Name} ({best.Sold} vé)";
                        TxtWorstSeller.Text = $"Show ít vé nhất: {worst.Name} ({worst.Sold} vé)";
                    }
                    else
                    {
                        TxtBestSeller.Text = "Show bán chạy nhất: Không có dữ liệu";
                        TxtWorstSeller.Text = "Show ít vé nhất: Không có dữ liệu";
                    }
                }
            }
        }
    }
}
