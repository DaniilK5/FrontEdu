using FrontEdu.Models.Auth;
using FrontEdu.Services;
using System.Net.Http.Json;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;

namespace FrontEdu.Views
{
    public partial class MainPage : ContentPage
    {
        private UserPermissionsResponse _userPermissions;
        private HttpClient _httpClient;
        private bool _isInitialized;

        public MainPage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (!_isInitialized)
            {
                await InitializePage();
                _isInitialized = true;
            }
        }

        private async Task InitializePage()
        {
            try
            {
                if (_httpClient == null)
                {
                    _httpClient = await AppConfig.CreateHttpClientAsync();
                }

                // �������� ���������� ������������
                var response = await _httpClient.GetAsync("api/Profile/me/permissions");
                if (response.IsSuccessStatusCode)
                {
                    _userPermissions = await response.Content.ReadFromJsonAsync<UserPermissionsResponse>();

                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        // ������ ��������
                        var hasEducationAccess = _userPermissions.Categories.Courses.CanView || 
                                               _userPermissions.Categories.Assignments.CanView || 
                                               _userPermissions.Categories.Assignments.CanSubmit;

                        // ��������� �������� ��� ������ ���������
                        SubjectsButton.IsVisible = _userPermissions.Categories.Courses.CanView;
                        CoursesButton.IsVisible = _userPermissions.Categories.Courses.CanView;
                        AssignmentsButton.IsVisible = _userPermissions.Categories.Assignments.CanView || 
                                                    _userPermissions.Categories.Assignments.CanSubmit;

                        // ������ �����������������
                        AdminSection.IsVisible = _userPermissions.Permissions.ManageUsers;

                        // ������ �����
                        var hasMessageAccess = _userPermissions.Permissions.SendMessages;
                        ChatButton.IsVisible = hasMessageAccess;
                        GroupChatsButton.IsVisible = hasMessageAccess;

                        // ���������
                        SettingsButton.IsVisible = _userPermissions.Permissions.ManageSettings;

                        // ��������� �������� ���������� ��� ���������
                        AbsencesButton.IsVisible = _userPermissions.Categories.Schedule.CanView ||
                                                 _userPermissions.Categories.Schedule.CanManage;

                    });
                }
                else
                {
                    throw new Exception($"Failed to get permissions: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing main page: {ex}");
                await DisplayAlert("������", "�� ������� ��������� ���������", "OK");
            }
        }

        private async void OnCoursesClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("//CoursesPage");
        }

        private async void OnAbsencesClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("//AbsencesPage");
        }
        private async void OnAssignmentsClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("//AssignmentsPage");
        }

        private async void OnProfileClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("//ProfilePage");
        }

        private async void OnSettingsClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("//SettingsPage");
        }

        private async void OnUsersClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("//UsersPage");
        }

        private async void OnChatClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("//ChatPage");
        }

        private async void OnGroupChatsClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("//GroupChatsPage");
        }

        private async void OnLogoutClicked(object sender, EventArgs e)
        {
            try
            {
                // ������� �����
                await SecureStorage.Default.SetAsync("auth_token", string.Empty);
                
                // �������������� �� �������� �����
                await Shell.Current.GoToAsync("//Login");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Logout error: {ex}");
                await DisplayAlert("������", "�� ������� ����� �� �������", "OK");
            }
        }

        private async void OnSubjectsClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("//SubjectsPage");
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            // �� ����������� HttpClient ��� ������ ������������ ��������
            _userPermissions = null;
        }

        // ��������� ����� ��� ������� �������� ��� ������ �� ����������
        public void Cleanup()
        {
            _httpClient?.Dispose();
            _httpClient = null;
            _userPermissions = null;
            _isInitialized = false;
        }
    }
}