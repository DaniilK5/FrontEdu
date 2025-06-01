using FrontEdu.Models.Chat;
using FrontEdu.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Runtime.Versioning;

namespace FrontEdu.Views
{
    public partial class ChatPage : ContentPage
    {
        private HttpClient _httpClient;
        private ObservableCollection<ChatUserDto> _users;
        private string _searchQuery = string.Empty;
        private string _selectedRole = string.Empty;
        private bool _isLoading;

        public ChatPage()
        {
            InitializeComponent();
            InitializeAsync();
        }

        private async void InitializeAsync()
        {
            try
            {
                LoadingIndicator.IsVisible = true;

                // Проверяем наличие токена
                var token = await SecureStorage.GetAsync("auth_token");
                if (string.IsNullOrEmpty(token))
                {
                    Debug.WriteLine("Token not found, redirecting to login");
                    await Shell.Current.GoToAsync("//Login");
                    return;
                }

                _httpClient = await AppConfig.CreateHttpClientAsync();
                _users = new ObservableCollection<ChatUserDto>();
                UsersCollection.ItemsSource = _users;

                UsersRefreshView.Command = new Command(async () => await RefreshUsers());

                await LoadUsers();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Initialize error: {ex}");
                await DisplayAlert("Ошибка", "Не удалось загрузить список пользователей", "OK");
            }
            finally
            {
                LoadingIndicator.IsVisible = false;
            }
        }

        private async Task LoadUsers()
        {
            if (_isLoading) return;

            try
            {
                _isLoading = true;
                LoadingIndicator.IsVisible = true;

                // Пересоздаем HttpClient для обновления токена
                _httpClient = await AppConfig.CreateHttpClientAsync();

                var queryParams = new List<string>();
                if (!string.IsNullOrEmpty(_searchQuery))
                    queryParams.Add($"search={Uri.EscapeDataString(_searchQuery)}");
                if (!string.IsNullOrEmpty(_selectedRole))
                    queryParams.Add($"role={Uri.EscapeDataString(_selectedRole)}");

                var url = "api/Message/users" + (queryParams.Any() ? "?" + string.Join("&", queryParams) : "");
                
                Debug.WriteLine($"Запрос к API: {url}");
                var response = await _httpClient.GetAsync(url);
                
                Debug.WriteLine($"Код ответа: {response.StatusCode}");
                var responseContent = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"Содержимое ответа: {responseContent}");

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    Debug.WriteLine("Unauthorized, redirecting to login");
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        await Shell.Current.GoToAsync("//Login");
                    });
                    return;
                }

                if (response.IsSuccessStatusCode)
                {
                    var users = await response.Content.ReadFromJsonAsync<List<ChatUserDto>>();
                    
                    [SupportedOSPlatform("Windows"), SupportedOSPlatform("Android"), 
                     SupportedOSPlatform("iOS"), SupportedOSPlatform("MacCatalyst")]
                    async Task UpdateUI()
                    {
                        await MainThread.InvokeOnMainThreadAsync(() =>
                        {
                            _users.Clear();
                            if (users != null)
                            {
                                foreach (var user in users)
                                {
                                    _users.Add(user);
                                }
                            }
                            return Task.CompletedTask;
                        });
                    }

                    await UpdateUI();
                }
                else
                {
                    [SupportedOSPlatform("Windows"), SupportedOSPlatform("Android")]
                    async Task ShowError()
                    {
                        await MainThread.InvokeOnMainThreadAsync(async () =>
                        {
                            await DisplayAlert("Ошибка", responseContent, "OK");
                        });
                    }

                    await ShowError();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка загрузки пользователей: {ex}");
                
                [SupportedOSPlatform("Windows"), SupportedOSPlatform("Android"), 
                 SupportedOSPlatform("iOS"), SupportedOSPlatform("MacCatalyst")]
                async Task ShowError()
                {
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        await DisplayAlert("Ошибка", 
                            $"Не удалось загрузить пользователей: {ex.Message}", "OK");
                    });
                }

                await ShowError();
            }
            finally
            {
                _isLoading = false;
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    LoadingIndicator.IsVisible = false;
                    UsersRefreshView.IsRefreshing = false;
                    return Task.CompletedTask;
                });
            }
        }

        private async void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            _searchQuery = e.NewTextValue ?? string.Empty;
            await LoadUsers();
        }

        private async void OnRoleFilterClicked(object sender, EventArgs e)
        {
            if (sender is Button button)
            {
                _selectedRole = button.CommandParameter?.ToString() ?? string.Empty;

                // Обновляем внешний вид кнопок
                foreach (var child in RoleFilters.Children)
                {
                    if (child is Button roleButton)
                    {
                        roleButton.BackgroundColor =
                            roleButton == button ?
                            Application.Current.Resources["Primary"] as Color ?? Colors.Blue :
                            Application.Current.Resources["Gray500"] as Color ?? Colors.Gray;
                    }
                }

                await LoadUsers();
            }
        }

        private async void OnUserSelected(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.FirstOrDefault() is ChatUserDto selectedUser)
            {
                try
                {
                    UsersCollection.SelectedItem = null;
                    Debug.WriteLine($"Navigating to chat with user ID: {selectedUser.Id}");
                    
                    var navigationParameter = new Dictionary<string, object>
                    {
                        { "userId", selectedUser.Id }
                    };

                    await Shell.Current.GoToAsync("DirectChatPage", navigationParameter);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Navigation error: {ex}");
                    await DisplayAlert("Ошибка", "Не удалось открыть чат", "OK");
                }
            }
        }

        private async Task RefreshUsers()
        {
            await LoadUsers();
        }
    }
}