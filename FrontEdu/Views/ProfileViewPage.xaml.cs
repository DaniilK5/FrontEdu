using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Json;
using System.Diagnostics;
using FrontEdu.Models.User;
using FrontEdu.Services;

namespace FrontEdu.Views
{

    [QueryProperty(nameof(UserId), "userId")]
    public partial class ProfileViewPage : ContentPage
    {
        private HttpClient _httpClient;
        private int _userId;

        public int UserId
        {
            get => _userId;
            set
            {
                if (_userId != value)
                {
                    _userId = value;
                    LoadUserProfile();
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
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Connection initialization error: {ex}");
                await DisplayAlert("Ошибка", "Не удалось инициализировать подключение", "OK");
            }
        }

        private async void LoadUserProfile()
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
                    var user = await response.Content.ReadFromJsonAsync<UserProfileDto>();
                    if (user != null)
                    {
                        await MainThread.InvokeOnMainThreadAsync(() =>
                        {
                            // Основная информация
                            FullNameLabel.Text = user.FullName ?? "-";
                            EmailLabel.Text = user.Email ?? "-";
                            RoleLabel.Text = user.Role ?? "-";

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
    }


}
