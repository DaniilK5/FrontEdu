using FrontEdu.Models.Admin;
using FrontEdu.Services;
using System.Collections.ObjectModel;
using System.Net.Http.Json;

namespace FrontEdu.Views
{
    public partial class UsersPage : ContentPage
    {
        private HttpClient _httpClient;
        private ObservableCollection<AdminUserDto> _users;
        private ObservableCollection<AdminUserDto> _allUsers;
        private string _searchQuery = string.Empty;
        private bool _isLoading;

        public UsersPage()
        {
            InitializeComponent();
            InitializeAsync();
        }

        private async void InitializeAsync()
        {
            try
            {
                _httpClient = await AppConfig.CreateHttpClientAsync();

                if (_users == null)
                {
                    _users = new ObservableCollection<AdminUserDto>();
                    _allUsers = new ObservableCollection<AdminUserDto>();
                    UsersCollection.ItemsSource = _users;
                    UsersRefreshView.Command = new Command(async () => await RefreshUsers());
                }

                await LoadUsers();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Ошибка", "Не удалось загрузить список пользователей", "OK");
            }
        }

        private async Task LoadUsers()
        {
            if (_isLoading) return;

            try
            {
                _isLoading = true;
                LoadingIndicator.IsVisible = true;

                var response = await _httpClient.GetAsync("api/Admin/users");

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    await Shell.Current.GoToAsync("//Login");
                    return;
                }

                if (response.IsSuccessStatusCode)
                {
                    var users = await response.Content.ReadFromJsonAsync<List<AdminUserDto>>();

                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        _allUsers.Clear();
                        _users.Clear();

                        if (users != null)
                        {
                            foreach (var user in users)
                            {
                                _allUsers.Add(user);
                                _users.Add(user);
                            }
                        }
                    });
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    await DisplayAlert("Ошибка", error, "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Ошибка",
                    "Не удалось загрузить список пользователей", "OK");
            }
            finally
            {
                _isLoading = false;
                LoadingIndicator.IsVisible = false;
                UsersRefreshView.IsRefreshing = false;
            }
        }

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            _searchQuery = e.NewTextValue?.ToLower() ?? string.Empty;
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            if (string.IsNullOrWhiteSpace(_searchQuery))
            {
                _users.Clear();
                foreach (var user in _allUsers)
                {
                    _users.Add(user);
                }
                return;
            }

            var filteredUsers = _allUsers.Where(u =>
                u.FullName?.ToLower().Contains(_searchQuery) == true ||
                u.Email?.ToLower().Contains(_searchQuery) == true ||
                u.StudentGroup?.ToLower().Contains(_searchQuery) == true);

            _users.Clear();
            foreach (var user in filteredUsers)
            {
                _users.Add(user);
            }
        }

        private async void OnRefreshClicked(object sender, EventArgs e)
        {
            await LoadUsers();
        }

        private async void OnUserSelected(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.FirstOrDefault() is AdminUserDto selectedUser)
            {
                UsersCollection.SelectedItem = null;
                var navigationParameter = new Dictionary<string, object>
                {
                    { "userId", selectedUser.Id }
                };
                // Добавляем префикс /// для абсолютной навигации
                await Shell.Current.GoToAsync("/ProfileViewPage", navigationParameter);
            }
        }

        private async void OnEditUserClicked(object sender, EventArgs e)
        {
            if (sender is Button button && button.CommandParameter is AdminUserDto user)
            {
                var navigationParameter = new Dictionary<string, object>
                {
                    { "userId", user.Id }
                };
                // Добавляем префикс /// для абсолютной навигации
                await Shell.Current.GoToAsync("/ProfileViewPage", navigationParameter);
            }
        }

        private async Task RefreshUsers()
        {
            await LoadUsers();
        }
    }
}