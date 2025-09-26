using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Json;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using FrontEdu.Models.User;
using FrontEdu.Services;

namespace FrontEdu.Views
{

    [QueryProperty(nameof(UserId), "userId")]
    public partial class ProfileViewPage : ContentPage
    {
        private HttpClient _httpClient;
        private int _userId;
        private bool _isAdmin;
        private UserProfileDto _currentUser;

        public int UserId
        {
            get => _userId;
            set
            {
                if (_userId != value)
                {
                    _userId = value;
                    // Используем Task.Run для асинхронного вызова
                    MainThread.BeginInvokeOnMainThread(async () => await LoadUserProfile());
                }
            }
        }

        public ProfileViewPage()
        {
            InitializeComponent();
            InitializeAsync();
        }

        private async void InitializeAsync()
        {
            try
            {
                _httpClient = await AppConfig.CreateHttpClientAsync();
                
                // Проверяем, является ли текущий пользователь администратором
                var token = await SecureStorage.GetAsync("auth_token");
                if (!string.IsNullOrEmpty(token))
                {
                    var handler = new JwtSecurityTokenHandler();
                    var jsonToken = handler.ReadToken(token) as JwtSecurityToken;
                    var role = jsonToken?.Claims.FirstOrDefault(c => c.Type.Contains("role"))?.Value;
                    _isAdmin = role == "Administrator";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Connection initialization error: {ex}");
                await DisplayAlert("Ошибка", "Не удалось инициализировать подключение", "OK");
            }
        }

        private async Task LoadUserProfile()
        {
            try
            {
                LoadingIndicator.IsVisible = true;

                if (_httpClient == null)
                {
                    _httpClient = await AppConfig.CreateHttpClientAsync();
                }

                var response = await _httpClient.GetAsync($"api/Profile/users/{UserId}");
                if (response.IsSuccessStatusCode)
                {
                    _currentUser = await response.Content.ReadFromJsonAsync<UserProfileDto>();
                    if (_currentUser != null)
                    {
                        // Синхронное обновление UI
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            UpdateUI(_currentUser);
                        });
                    }
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    await DisplayAlert("Ошибка", error, "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading user profile: {ex}");
                await DisplayAlert("Ошибка", "Не удалось загрузить профиль пользователя", "OK");
            }
            finally
            {
                LoadingIndicator.IsVisible = false;
            }
        }

        // Отдельный метод для обновления UI
        private void UpdateUI(UserProfileDto user)
        {
            // Основная информация
            FullNameLabel.Text = user.FullName ?? "-";
            EmailLabel.Text = user.Email ?? "-";
            RoleLabel.Text = user.Role ?? "-";

            // Показываем секцию редактирования роли только администратору
            RoleEditSection.IsVisible = _isAdmin;
            if (_isAdmin)
            {
                // Устанавливаем текущую роль в Picker
                var roleIndex = Array.IndexOf(RolePicker.Items.ToArray(), user.Role);
                if (roleIndex >= 0)
                {
                    RolePicker.SelectedIndex = roleIndex;
                }
            }

            // Контактная информация
            AddressLabel.Text = user.Address ?? "-";
            PhoneLabel.Text = user.PhoneNumber ?? "-";
            SocialStatusLabel.Text = user.SocialStatus ?? "-";

            // Студенческая информация
            bool isStudent = user.Role?.Equals("Student", StringComparison.OrdinalIgnoreCase) ?? false;
            StudentInfoPanel.IsVisible = isStudent;
            if (isStudent)
            {
                StudentGroupLabel.Text = user.StudentGroup ?? "-";
                StudentIdLabel.Text = user.StudentId ?? "-";
            }

            // Информация о группе
            if (user.GroupInfo != null)
            {
                GroupInfoPanel.IsVisible = true;
                GroupNameLabel.Text = user.GroupInfo.Name ?? "-";
                GroupDescriptionLabel.Text = user.GroupInfo.Description ?? "-";
                CuratorNameLabel.Text = user.GroupInfo.CuratorName ?? "-";
            }
            else
            {
                GroupInfoPanel.IsVisible = false;
            }
        }

        private async void OnSaveRoleClicked(object sender, EventArgs e)
        {
            if (!_isAdmin || RolePicker.SelectedItem == null)
                return;

            try
            {
                LoadingIndicator.IsVisible = true;

                var newRole = new { role = RolePicker.SelectedItem.ToString() };
                var response = await _httpClient.PutAsJsonAsync(
                    $"api/Admin/users/{UserId}/role", newRole);

                if (response.IsSuccessStatusCode)
                {
                    await DisplayAlert("Успех", "Роль пользователя успешно обновлена", "OK");
                    await LoadUserProfile(); // Перезагружаем профиль
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    await DisplayAlert("Ошибка", error, "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating user role: {ex}");
                await DisplayAlert("Ошибка", "Не удалось обновить роль пользователя", "OK");
            }
            finally
            {
                LoadingIndicator.IsVisible = false;
            }
        }
    }
}
