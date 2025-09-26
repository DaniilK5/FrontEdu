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

                // Получаем разрешения пользователя
                var response = await _httpClient.GetAsync("api/Profile/me/permissions");
                if (response.IsSuccessStatusCode)
                {
                    _userPermissions = await response.Content.ReadFromJsonAsync<UserPermissionsResponse>();

                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        // Секция обучения
                        var hasEducationAccess = _userPermissions.Categories.Courses.CanView || 
                                               _userPermissions.Categories.Assignments.CanView || 
                                               _userPermissions.Categories.Assignments.CanSubmit;

                        // Добавляем проверку для кнопки предметов
                        SubjectsButton.IsVisible = _userPermissions.Categories.Courses.CanView;
                        // CoursesButton.IsVisible = _userPermissions.Categories.Courses.CanView;
                        // AssignmentsButton.IsVisible = _userPermissions.Categories.Assignments.CanView || 
                        //                            _userPermissions.Categories.Assignments.CanSubmit;
                        AssignmentsButton.IsVisible = false;
                        CoursesButton.IsVisible = false;
                        // Секция администрирования
                        AdminSection.IsVisible = _userPermissions.Permissions.ManageUsers;

                        // Секция чатов
                        var hasMessageAccess = _userPermissions.Permissions.SendMessages;
                        ChatButton.IsVisible = hasMessageAccess;
                        GroupChatsButton.IsVisible = hasMessageAccess;

                        // Настройки
                        SettingsButton.IsVisible = _userPermissions.Permissions.ManageSettings;

                        // Добавляем проверку разрешений для пропусков
                        AbsencesButton.IsVisible = _userPermissions.Categories.Schedule.CanView ||
                                                 _userPermissions.Categories.Schedule.CanManage;

                        // Добавляем проверку для секции куратора
                        CuratorSection.IsVisible = _userPermissions.Categories.Schedule.CanManage && 
                                                 _userPermissions.Role?.ToLower() == "teacher";

                        // Добавляем видимость кнопки расписания
                        ScheduleButton.IsVisible = true; // Расписание доступно всем

                        // Добавляем проверку для секции родителя
                        ParentSection.IsVisible = _userPermissions.Role?.ToLower() == "parent";

                        // Добавляем проверку для кнопки оценок
                        GradesButton.IsVisible = _userPermissions.Role?.ToLower() == "student";
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
                await DisplayAlert("Ошибка", "Не удалось загрузить настройки", "OK");
            }
        }

        private async void OnCoursesClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("/CoursesPage");
        }

        private async void OnAbsencesClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("/AbsencesPage");
        }
        private async void OnAssignmentsClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("/AssignmentsPage");
        }

        private async void OnProfileClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("/ProfilePage");
        }

        private async void OnSettingsClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("/SettingsPage");
        }

        private async void OnUsersClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("//UsersPage");
        }

        private async void OnChatClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("/ChatPage");
        }

        private async void OnGroupChatsClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("/GroupChatsPage");
        }

        private async void OnLogoutClicked(object sender, EventArgs e)
        {
            try
            {
                Debug.WriteLine("Logout process started");

                // Получаем текущий токен для логирования
                var token = await SecureStorage.Default.GetAsync("auth_token");
                if (!string.IsNullOrEmpty(token))
                {
                    try
                    {
                        var handler = new JwtSecurityTokenHandler();
                        var jwtToken = handler.ReadToken(token) as JwtSecurityToken;
                        var userId = jwtToken?.Claims.FirstOrDefault(x => x.Type == "sub")?.Value;
                        var userRole = jwtToken?.Claims.FirstOrDefault(x => x.Type == "role")?.Value;
                        
                        Debug.WriteLine($"Logging out user - ID: {userId ?? "unknown"}, Role: {userRole ?? "unknown"}");
                    }
                    catch (Exception tokenEx)
                    {
                        Debug.WriteLine($"Error reading token: {tokenEx.Message}");
                    }
                }
                
                Debug.WriteLine("Removing authentication token from secure storage");
                try
                {
                    SecureStorage.Default.Remove("auth_token");
                    Debug.WriteLine("Token successfully removed");
                }
                catch (Exception storageEx)
                {
                    Debug.WriteLine($"Error removing token from storage: {storageEx.Message}");
                }

                // Очищаем состояние страницы перед переходом
                _userPermissions = null;
                _isInitialized = false;
                
                // Важно: НЕ освобождаем HttpClient здесь
                _httpClient = null; // Просто обнуляем ссылку
                
                Debug.WriteLine("Redirecting to login page");
                await Shell.Current.GoToAsync("/Login");
                
                Debug.WriteLine("Logout completed successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Logout error: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                await DisplayAlert("Ошибка", "Не удалось выйти из системы", "OK");
            }
        }

        private async void OnSubjectsClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("/SubjectsPage");
        }

        private async void OnCuratorClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("/CuratorPage");
        }

        private async void OnGroupManagementClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("/GroupManagementPage");
        }

        private async void OnScheduleClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("/SchedulePage");
        }

        private async void OnChildrenPerformanceClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("/ParentPerformancePage");
        }

        private async void OnGradesClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("/StudentGradesPage");
        }

        private async void OnHelpClicked(object sender, EventArgs e)
        {
            try
            {
                await Shell.Current.GoToAsync("HelpPage");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Navigation error: {ex}");
                await DisplayAlert("Ошибка", "Не удалось открыть справку", "OK");
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            // Не освобождаем HttpClient при каждом исчезновении страницы
            _userPermissions = null;
        }

        // Добавляем метод для очистки ресурсов при выходе из приложения
        public void Cleanup()
        {
            _userPermissions = null;
            _isInitialized = false;
            _httpClient = null; // Просто обнуляем ссылку, не вызывая Dispose
        }
    }
}