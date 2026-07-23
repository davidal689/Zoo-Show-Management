using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using Zoo_Show_Mnm.Data;
using Zoo_Show_Mnm.Models;
using BCrypt.Net;

namespace Zoo_Show_Mnm.Views
{
    public partial class LoginView : UserControl
    {
        public event EventHandler<User>? LoginSuccess;
        
        private User? _tempUser;

        public LoginView()
        {
            InitializeComponent();
        }

        private void ShowError(string msg)
        {
            TxtError.Text = msg;
            TxtError.Visibility = Visibility.Visible;
            TxtSuccess.Visibility = Visibility.Collapsed;
        }

        private void ShowSuccess(string msg)
        {
            TxtSuccess.Text = msg;
            TxtSuccess.Visibility = Visibility.Visible;
            TxtError.Visibility = Visibility.Collapsed;
        }

        private void ClearMessages()
        {
            TxtError.Visibility = Visibility.Collapsed;
            TxtSuccess.Visibility = Visibility.Collapsed;
        }

        private void BtnToggleRegister_Click(object sender, RoutedEventArgs e)
        {
            ClearMessages();
            PanelLogin.Visibility = Visibility.Collapsed;
            PanelRegister.Visibility = Visibility.Visible;
        }

        private void BtnToggleLogin_Click(object sender, RoutedEventArgs e)
        {
            ClearMessages();
            PanelRegister.Visibility = Visibility.Collapsed;
            PanelLogin.Visibility = Visibility.Visible;
        }

        // --- PASSWORD VISIBILITY LOGIC (LOGIN) ---
        private void ChkShowPassword_Checked(object sender, RoutedEventArgs e)
        {
            TxtPasswordVisible.Text = TxtPassword.Password;
            TxtPassword.Visibility = Visibility.Collapsed;
            TxtPasswordVisible.Visibility = Visibility.Visible;
        }

        private void ChkShowPassword_Unchecked(object sender, RoutedEventArgs e)
        {
            TxtPassword.Password = TxtPasswordVisible.Text;
            TxtPasswordVisible.Visibility = Visibility.Collapsed;
            TxtPassword.Visibility = Visibility.Visible;
        }

        // --- PASSWORD VISIBILITY LOGIC (REGISTER) ---
        private void ChkRegShowPassword_Checked(object sender, RoutedEventArgs e)
        {
            TxtRegPasswordVisible.Text = TxtRegPassword.Password;
            TxtRegPassword.Visibility = Visibility.Collapsed;
            TxtRegPasswordVisible.Visibility = Visibility.Visible;

            TxtRegConfirmPasswordVisible.Text = TxtRegConfirmPassword.Password;
            TxtRegConfirmPassword.Visibility = Visibility.Collapsed;
            TxtRegConfirmPasswordVisible.Visibility = Visibility.Visible;
        }

        private void ChkRegShowPassword_Unchecked(object sender, RoutedEventArgs e)
        {
            TxtRegPassword.Password = TxtRegPasswordVisible.Text;
            TxtRegPasswordVisible.Visibility = Visibility.Collapsed;
            TxtRegPassword.Visibility = Visibility.Visible;

            TxtRegConfirmPassword.Password = TxtRegConfirmPasswordVisible.Text;
            TxtRegConfirmPasswordVisible.Visibility = Visibility.Collapsed;
            TxtRegConfirmPassword.Visibility = Visibility.Visible;
        }

        // --- PASSWORD VISIBILITY LOGIC (NEW/TEMP PWD) ---
        private void ChkNewShowPassword_Checked(object sender, RoutedEventArgs e)
        {
            TxtNewPasswordVisible.Text = TxtNewPassword.Password;
            TxtNewPassword.Visibility = Visibility.Collapsed;
            TxtNewPasswordVisible.Visibility = Visibility.Visible;

            TxtConfirmNewPasswordVisible.Text = TxtConfirmNewPassword.Password;
            TxtConfirmNewPassword.Visibility = Visibility.Collapsed;
            TxtConfirmNewPasswordVisible.Visibility = Visibility.Visible;
        }

        private void ChkNewShowPassword_Unchecked(object sender, RoutedEventArgs e)
        {
            TxtNewPassword.Password = TxtNewPasswordVisible.Text;
            TxtNewPasswordVisible.Visibility = Visibility.Collapsed;
            TxtNewPassword.Visibility = Visibility.Visible;

            TxtConfirmNewPassword.Password = TxtConfirmNewPasswordVisible.Text;
            TxtConfirmNewPasswordVisible.Visibility = Visibility.Collapsed;
            TxtConfirmNewPassword.Visibility = Visibility.Visible;
        }

        private async void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            ClearMessages();
            string username = TxtEmail.Text.Trim();
            
            // Sync password based on checkbox state
            string password = ChkShowPassword.IsChecked == true ? TxtPasswordVisible.Text : TxtPassword.Password;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ShowError("Tên đăng nhập và Mật khẩu là bắt buộc.");
                return;
            }

            using (var db = new ApplicationDbContext())
            {
                // Login via Username instead of Email
                var user = await db.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());
                if (user == null || user.IsDeactivated)
                {
                    ShowError("Thông tin đăng nhập không đúng hoặc tài khoản bị khóa.");
                    return;
                }

                if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                {
                    ShowError("Thông tin đăng nhập không đúng.");
                    return;
                }

                if (user.IsTemporaryPassword)
                {
                    _tempUser = user;
                    PanelLogin.Visibility = Visibility.Collapsed;
                    PanelChangePassword.Visibility = Visibility.Visible;
                    ShowSuccess("Bạn cần cập nhật lại mật khẩu.");
                    return;
                }

                LoginSuccess?.Invoke(this, user);
            }
        }

        private async void BtnRegister_Click(object sender, RoutedEventArgs e)
        {
            ClearMessages();
            string name = TxtRegName.Text.Trim();
            string username = TxtRegEmail.Text.Trim();

            // Sync passwords
            string password = ChkRegShowPassword.IsChecked == true ? TxtRegPasswordVisible.Text : TxtRegPassword.Password;
            string confirm = ChkRegShowPassword.IsChecked == true ? TxtRegConfirmPasswordVisible.Text : TxtRegConfirmPassword.Password;

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ShowError("Tất cả các trường là bắt buộc.");
                return;
            }

            if (password != confirm)
            {
                ShowError("Mật khẩu xác nhận không khớp.");
                return;
            }

            if (password.Length < 8 || !password.Any(char.IsLetter) || !password.Any(char.IsDigit))
            {
                ShowError("Mật khẩu phải tối thiểu 8 ký tự, gồm ít nhất 1 chữ cái và 1 chữ số.");
                return;
            }

            using (var db = new ApplicationDbContext())
            {
                var usernameExists = await db.Users.AnyAsync(u => u.Username.ToLower() == username.ToLower());
                if (usernameExists)
                {
                    ShowError("Tên đăng nhập này đã được đăng ký.");
                    return;
                }

                var visitor = new User
                {
                    Name = name,
                    Username = username.ToLower(),
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                    Role = "Visitor",
                    IsTemporaryPassword = false,
                    IsDeactivated = false
                };

                db.Users.Add(visitor);
                await db.SaveChangesAsync();

                LoginSuccess?.Invoke(this, visitor);
            }
        }

        private async void BtnChangePassword_Click(object sender, RoutedEventArgs e)
        {
            ClearMessages();
            if (_tempUser == null) return;

            // Sync passwords
            string newPassword = ChkNewShowPassword.IsChecked == true ? TxtNewPasswordVisible.Text : TxtNewPassword.Password;
            string confirm = ChkNewShowPassword.IsChecked == true ? TxtConfirmNewPasswordVisible.Text : TxtConfirmNewPassword.Password;

            if (newPassword != confirm)
            {
                ShowError("Mật khẩu xác nhận không khớp.");
                return;
            }

            if (newPassword.Length < 8 || !newPassword.Any(char.IsLetter) || !newPassword.Any(char.IsDigit))
            {
                ShowError("Mật khẩu mới phải tối thiểu 8 ký tự, gồm ít nhất 1 chữ cái và 1 chữ số.");
                return;
            }

            using (var db = new ApplicationDbContext())
            {
                var user = await db.Users.FindAsync(_tempUser.Id);
                if (user != null)
                {
                    user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
                    user.IsTemporaryPassword = false;
                    await db.SaveChangesAsync();

                    ShowSuccess("Mật khẩu thay đổi thành công. Hãy đăng nhập lại.");
                    PanelChangePassword.Visibility = Visibility.Collapsed;
                    PanelLogin.Visibility = Visibility.Visible;
                    
                    // Reset fields
                    TxtPassword.Password = "";
                    TxtPasswordVisible.Text = "";
                    _tempUser = null;
                }
            }
        }
    }
}
