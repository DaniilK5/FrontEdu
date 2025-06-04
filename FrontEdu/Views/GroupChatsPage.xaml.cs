using FrontEdu.Models.Chat;
using FrontEdu.Models.GroupChat;
using FrontEdu.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.Http.Json;

namespace FrontEdu.Views
{
    public partial class GroupChatsPage : ContentPage
    {
        private HttpClient _httpClient;
        private ObservableCollection<GroupChatDto> _groupChats;
        private ObservableCollection<GroupChatDto> _allGroupChats; // Добавьте это поле для хранения полного списка
        private string _searchQuery = string.Empty;
        private bool _isLoading;

        public GroupChatsPage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await InitializePage();
        }

        private async Task InitializePage()
        {
            try
            {
                LoadingIndicator.IsVisible = true;

                var token = await SecureStorage.GetAsync("auth_token");
                if (string.IsNullOrEmpty(token))
                {
                    Debug.WriteLine("Token not found, redirecting to login");
                    await Shell.Current.GoToAsync("//Login");
                    return;
                }

                _httpClient = await AppConfig.CreateHttpClientAsync();
                
                if (_groupChats == null)
                {
                    _groupChats = new ObservableCollection<GroupChatDto>();
                    GroupChatsCollection.ItemsSource = _groupChats;
                    GroupChatsRefreshView.Command = new Command(async () => await RefreshGroupChats());
                }

                await LoadGroupChats();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Initialize error: {ex}");
                await DisplayAlert("Ошибка", "Не удалось загрузить список групповых чатов", "OK");
            }
            finally
            {
                LoadingIndicator.IsVisible = false;
            }
        }

        private async Task LoadGroupChats()
        {
            if (_isLoading) return;

            try
            {
                _isLoading = true;
                LoadingIndicator.IsVisible = true;

                _httpClient = await AppConfig.CreateHttpClientAsync();

                var url = "api/GroupChat/my";
                var response = await _httpClient.GetAsync(url);
                
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        await Shell.Current.GoToAsync("//Login");
                    });
                    return;
                }

                if (response.IsSuccessStatusCode)
                {
                    var groupChatsResponse = await response.Content.ReadFromJsonAsync<GroupChatResponse>();
                    
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        // Сохраняем полный список чатов
                        _allGroupChats = new ObservableCollection<GroupChatDto>();
                        if (groupChatsResponse?.Chats != null)
                        {
                            foreach (var chat in groupChatsResponse.Chats)
                            {
                                var groupChatDto = new GroupChatDto
                                {
                                    Id = chat.Id,
                                    Name = chat.Name,
                                    CreatedAt = chat.CreatedAt,
                                    Members = chat.Members.Select(m => new ChatUserDto
                                    {
                                        Id = m.Id,
                                        FullName = m.FullName,
                                        Role = m.IsAdmin ? "Administrator" : "Member"
                                    }).ToList(),
                                    Creator = new ChatUserDto
                                    {
                                        Id = chat.Members.FirstOrDefault(m => m.IsAdmin)?.Id ?? 0,
                                        FullName = chat.Members.FirstOrDefault(m => m.IsAdmin)?.FullName ?? "Unknown"
                                    }
                                };
                                _allGroupChats.Add(groupChatDto);
                            }
                        }

                        // Применяем текущий фильтр поиска
                        ApplySearchFilter(_searchQuery);
                        return Task.CompletedTask;
                    });
                }
                else
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        await DisplayAlert("Ошибка", responseContent, "OK");
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading group chats: {ex}");
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await DisplayAlert("Ошибка", 
                        $"Не удалось загрузить групповые чаты: {ex.Message}", "OK");
                });
            }
            finally
            {
                _isLoading = false;
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    LoadingIndicator.IsVisible = false;
                    GroupChatsRefreshView.IsRefreshing = false;
                    return Task.CompletedTask;
                });
            }
        }

        private async void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            _searchQuery = e.NewTextValue ?? string.Empty;
            ApplySearchFilter(_searchQuery);
        }

        private void ApplySearchFilter(string searchQuery)
        {
            if (_allGroupChats == null) return;

            // Очищаем текущий список
            _groupChats.Clear();

            // Если поисковый запрос пустой, показываем все чаты
            if (string.IsNullOrWhiteSpace(searchQuery))
            {
                foreach (var chat in _allGroupChats)
                {
                    _groupChats.Add(chat);
                }
                return;
            }

            // Применяем фильтр
            var searchTerms = searchQuery.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var filteredChats = _allGroupChats.Where(chat =>
            {
                // Проверяем название чата
                if (chat.Name.ToLower().Contains(searchQuery.ToLower()))
                    return true;

                // Проверяем участников чата
                if (chat.Members.Any(m => searchTerms.All(term => 
                    m.FullName.ToLower().Contains(term))))
                    return true;

                return false;
            });

            // Добавляем отфильтрованные чаты в коллекцию
            foreach (var chat in filteredChats)
            {
                _groupChats.Add(chat);
            }
        }

        private async void OnGroupChatSelected(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.FirstOrDefault() is GroupChatDto selectedChat)
            {
                try
                {
                    var chatId = selectedChat.Id;
                    GroupChatsCollection.SelectedItem = null;

                    var navigationParameter = new Dictionary<string, object>
                    {
                        { "groupChatId", chatId }
                    };

                    await Shell.Current.GoToAsync("GroupChatPage", navigationParameter);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Navigation error: {ex}");
                    await DisplayAlert("Ошибка", "Не удалось открыть групповой чат", "OK");
                }
            }
        }

        private async Task RefreshGroupChats()
        {
            await LoadGroupChats();
        }

        private async void OnCreateGroupChatClicked(object sender, EventArgs e)
        {
            string chatName = await DisplayPromptAsync("Новый групповой чат", 
                "Введите название чата", "Создать", "Отмена");

            if (!string.IsNullOrWhiteSpace(chatName))
            {
                try
                {
                    var createRequest = new CreateGroupChatRequest
                    {
                        Name = chatName
                    };

                    var response = await _httpClient.PostAsJsonAsync("api/GroupChat/create", createRequest);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        var result = await response.Content.ReadFromJsonAsync<CreateGroupChatResponse>();
                        if (result?.GroupChatId > 0)
                        {
                            await LoadGroupChats(); // Обновляем список чатов

                            // Опционально: можно сразу перейти в созданный чат
                            var navigationParameter = new Dictionary<string, object>
                            {
                                { "groupChatId", result.GroupChatId }
                            };
                            await Shell.Current.GoToAsync("GroupChatPage", navigationParameter);
                        }
                        else
                        {
                            await DisplayAlert("Ошибка", "Не удалось получить идентификатор созданного чата", "OK");
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
                    Debug.WriteLine($"Create group chat error: {ex}");
                    await DisplayAlert("Ошибка", "Не удалось создать групповой чат", "OK");
                }
            }
        }

        private async void OnBackToMainClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("//MainPage");
        }
    }
}