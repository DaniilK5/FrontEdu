using FrontEdu.Models.Chat;
using FrontEdu.Services;
using System.Collections.ObjectModel;
using System.Net.Http.Json;

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
                _httpClient = await AppConfig.CreateHttpClientAsync();
                _users = new ObservableCollection<ChatUserDto>();
                UsersCollection.ItemsSource = _users;
                
                UsersRefreshView.Command = new Command(async () => await RefreshUsers());
                
                await LoadUsers();
            }
            catch (Exception ex)
            {
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

                var queryParams = new List<string>();
                if (!string.IsNullOrEmpty(_searchQuery))
                    queryParams.Add($"search={Uri.EscapeDataString(_searchQuery)}");
                if (!string.IsNullOrEmpty(_selectedRole))
                    queryParams.Add($"role={Uri.EscapeDataString(_selectedRole)}");

                var url = "api/message/users" + (queryParams.Any() ? "?" + string.Join("&", queryParams) : "");
                var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var users = await response.Content.ReadFromJsonAsync<List<ChatUserDto>>();
                    _users.Clear();
                    if (users != null)
                    {
                        foreach (var user in users)
                        {
                            _users.Add(user);
                        }
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
                await DisplayAlert("Ошибка", "Не удалось загрузить пользователей", "OK");
            }
            finally
            {
                LoadingIndicator.IsVisible = false;
                _isLoading = false;
                UsersRefreshView.IsRefreshing = false;
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
                UsersCollection.SelectedItem = null;
                await Shell.Current.GoToAsync($"DirectChatPage?userId={selectedUser.Id}");
            }
        }

        private async Task RefreshUsers()
        {
            await LoadUsers();
        }
    }
}