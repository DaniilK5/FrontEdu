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
                    // ���������� Task.Run ��� ������������ ������
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
                
                // ���������, �������� �� ������� ������������ ���������������
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
                await DisplayAlert("������", "�� ������� ���������������� �����������", "OK");
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
                        // ���������� ���������� UI
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            UpdateUI(_currentUser);
                        });
                    }
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    await DisplayAlert("������", error, "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading user profile: {ex}");
                await DisplayAlert("������", "�� ������� ��������� ������� ������������", "OK");
            }
            finally
            {
                LoadingIndicator.IsVisible = false;
            }
        }

        // ��������� ����� ��� ���������� UI
        private void UpdateUI(UserProfileDto user)
        {
            // �������� ����������
            FullNameLabel.Text = user.FullName ?? "-";
            EmailLabel.Text = user.Email ?? "-";
            RoleLabel.Text = user.Role ?? "-";

            // ���������� ������ �������������� ���� ������ ��������������
            RoleEditSection.IsVisible = _isAdmin;
            if (_isAdmin)
            {
                // ������������� ������� ���� � Picker
                var roleIndex = Array.IndexOf(RolePicker.Items.ToArray(), user.Role);
                if (roleIndex >= 0)
                {
                    RolePicker.SelectedIndex = roleIndex;
                }
            }

            // ���������� ����������
            AddressLabel.Text = user.Address ?? "-";
            PhoneLabel.Text = user.PhoneNumber ?? "-";
            SocialStatusLabel.Text = user.SocialStatus ?? "-";

            // ������������ ����������
            bool isStudent = user.Role?.Equals("Student", StringComparison.OrdinalIgnoreCase) ?? false;
            StudentInfoPanel.IsVisible = isStudent;
            if (isStudent)
            {
                StudentGroupLabel.Text = user.StudentGroup ?? "-";
                StudentIdLabel.Text = user.StudentId ?? "-";
            }

            // ���������� � ������
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
                    await DisplayAlert("�����", "���� ������������ ������� ���������", "OK");
                    await LoadUserProfile(); // ������������� �������
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    await DisplayAlert("������", error, "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating user role: {ex}");
                await DisplayAlert("������", "�� ������� �������� ���� ������������", "OK");
            }
            finally
            {
                LoadingIndicator.IsVisible = false;
            }
        }
    }
}
