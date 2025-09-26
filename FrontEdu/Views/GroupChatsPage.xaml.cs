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
        private ObservableCollection<GroupChatDto> _allGroupChats; // �������� ��� ���� ��� �������� ������� ������
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
                await DisplayAlert("������", "�� ������� ��������� ������ ��������� �����", "OK");
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
                        // ��������� ������ ������ �����
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

                        // ��������� ������� ������ ������
                        ApplySearchFilter(_searchQuery);
                        return Task.CompletedTask;
                    });
                }
                else
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        await DisplayAlert("������", responseContent, "OK");
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading group chats: {ex}");
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await DisplayAlert("������", 
                        $"�� ������� ��������� ��������� ����: {ex.Message}", "OK");
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

            // ������� ������� ������
            _groupChats.Clear();

            // ���� ��������� ������ ������, ���������� ��� ����
            if (string.IsNullOrWhiteSpace(searchQuery))
            {
                foreach (var chat in _allGroupChats)
                {
                    _groupChats.Add(chat);
                }
                return;
            }

            // ��������� ������
            var searchTerms = searchQuery.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var filteredChats = _allGroupChats.Where(chat =>
            {
                // ��������� �������� ����
                if (chat.Name.ToLower().Contains(searchQuery.ToLower()))
                    return true;

                // ��������� ���������� ����
                if (chat.Members.Any(m => searchTerms.All(term => 
                    m.FullName.ToLower().Contains(term))))
                    return true;

                return false;
            });

            // ��������� ��������������� ���� � ���������
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
                    await DisplayAlert("������", "�� ������� ������� ��������� ���", "OK");
                }
            }
        }

        private async Task RefreshGroupChats()
        {
            await LoadGroupChats();
        }

        private async void OnCreateGroupChatClicked(object sender, EventArgs e)
        {
            string chatName = await DisplayPromptAsync("����� ��������� ���", 
                "������� �������� ����", "�������", "������");

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
                            await LoadGroupChats(); // ��������� ������ �����

                            // �����������: ����� ����� ������� � ��������� ���
                            var navigationParameter = new Dictionary<string, object>
                            {
                                { "groupChatId", result.GroupChatId }
                            };
                            await Shell.Current.GoToAsync("GroupChatPage", navigationParameter);
                        }
                        else
                        {
                            await DisplayAlert("������", "�� ������� �������� ������������� ���������� ����", "OK");
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
                    Debug.WriteLine($"Create group chat error: {ex}");
                    await DisplayAlert("������", "�� ������� ������� ��������� ���", "OK");
                }
            }
        }

        private async void OnBackToMainClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("//MainPage");
        }
    }
}