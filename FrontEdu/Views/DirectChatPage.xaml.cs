using FrontEdu.Models.Chat;
using FrontEdu.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Web;

namespace FrontEdu.Views
{
    [QueryProperty(nameof(UserId), "userId")]
    public partial class DirectChatPage : ContentPage
    {
        private HttpClient _httpClient;
        private ObservableCollection<MessageDto> Messages { get; set; }
        private int _currentPage = 1;
        private const int PageSize = 20;
        private bool _isLoading;
        private int _userId;
        private MessageDto _messageBeingEdited;
        public int UserId
        {
            get => _userId;
            set
            {
                if (_userId != value)
                {
                    _userId = value;
                    Debug.WriteLine($"UserId set to: {value}");
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        try
                        {
                            await LoadInitialMessages();
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error loading messages: {ex}");
                            await DisplayAlert("Ошибка", "Не удалось загрузить сообщения", "OK");
                        }
                    });
                }
            }
        }

        public DirectChatPage()
        {
            InitializeComponent();
            Messages = new ObservableCollection<MessageDto>();
            MessagesCollection.ItemsSource = Messages;
            InitializeAsync();
        }

        private async void InitializeAsync()
        {
            try
            {
                _httpClient = await AppConfig.CreateHttpClientAsync();
                Debug.WriteLine("HttpClient initialized in DirectChatPage");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Connection initialization error: {ex}");
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await DisplayAlert("Ошибка", "Не удалось инициализировать подключение", "OK");
                });
            }
        }

        private async Task LoadInitialMessages()
        {
            try
            {
                _currentPage = 1;
                Messages.Clear();
                await LoadMessages();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Ошибка", "Не удалось загрузить сообщения", "OK");
            }
        }

        private async void OnLoadMoreMessages(object sender, EventArgs e)
        {
            if (!_isLoading)
            {
                _currentPage++;
                await LoadMessages();
            }
        }
        private async Task LoadMessages()
        {
            if (_httpClient == null)
            {
                _httpClient = await AppConfig.CreateHttpClientAsync();
            }

            try
            {
                _isLoading = true;
                var response = await _httpClient.GetAsync(
                    $"api/Message/direct/{UserId}?page={_currentPage}&pageSize={PageSize}");

                Debug.WriteLine($"Загрузка сообщений: {response.StatusCode}");
                var responseContent = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"Содержимое ответа: {responseContent}");

                if (response.IsSuccessStatusCode)
                {
                    var messages = await response.Content.ReadFromJsonAsync<List<MessageDto>>();
                    if (messages != null && messages.Any())
                    {
                        await MainThread.InvokeOnMainThreadAsync(async () =>
                        {
                            foreach (var message in messages)
                            {
                                var token = await SecureStorage.GetAsync("auth_token");
                                if (!string.IsNullOrEmpty(token))
                                {
                                    var handler = new JwtSecurityTokenHandler();
                                    var jsonToken = handler.ReadToken(token) as JwtSecurityToken;
                                    var currentUserId = int.Parse(jsonToken?.Claims.FirstOrDefault(c => 
                                        c.Type.Contains("nameidentifier"))?.Value ?? "0");
                                    
                                    message.IsFromCurrentUser = message.Sender.Id == currentUserId;
                                }
                                Messages.Add(message);
                            }
                        });
                    }
                }
                else
                {
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        await DisplayAlert("Ошибка", responseContent, "OK");
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка загрузки сообщений: {ex}");
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await DisplayAlert("Ошибка", 
                        $"Не удалось загрузить сообщения: {ex.Message}", "OK");
                });
            }
            finally
            {
                _isLoading = false;
            }
        }

        private async void OnSendClicked(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(MessageEntry.Text))
                return;

            try
            {
                // Создаем MultipartFormDataContent для отправки сообщения
                var content = new MultipartFormDataContent();
                content.Add(new StringContent(UserId.ToString()), "ReceiverId");
                content.Add(new StringContent(MessageEntry.Text), "Content");

                var response = await _httpClient.PostAsync("api/Message/send", content);
                if (response.IsSuccessStatusCode)
                {
                    MessageEntry.Text = string.Empty;
                    await LoadInitialMessages(); // Перезагружаем сообщения
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    await DisplayAlert("Ошибка", error, "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Ошибка", "Не удалось отправить сообщение", "OK");
                Debug.WriteLine($"Send message error: {ex}");
            }
        }

        private async void OnAttachFileClicked(object sender, EventArgs e)
        {
            try
            {
                var file = await FilePicker.PickAsync();
                if (file != null)
                {
                    // Создаем MultipartFormDataContent для отправки файла
                    var content = new MultipartFormDataContent();
                    var streamContent = new StreamContent(await file.OpenReadAsync());
                    content.Add(streamContent, "Attachment", file.FileName);
                    content.Add(new StringContent(UserId.ToString()), "ReceiverId");

                    if (!string.IsNullOrWhiteSpace(MessageEntry.Text))
                    {
                        content.Add(new StringContent(MessageEntry.Text), "Content");
                    }

                    var response = await _httpClient.PostAsync("api/Message/send", content);
                    if (response.IsSuccessStatusCode)
                    {
                        MessageEntry.Text = string.Empty;
                        await LoadInitialMessages();
                    }
                    else
                    {
                        var error = await response.Content.ReadAsStringAsync();
                        await DisplayAlert("Ошибка", error, "OK");
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Ошибка", "Не удалось отправить файл", "OK");
            }
        }
        private async void OnAttachmentClicked(object sender, EventArgs e)
        {
            if (sender is Button button && button.CommandParameter is int messageId)
            {
                try
                {
                    var response = await _httpClient.GetAsync($"api/Message/file/{messageId}");
                    if (response.IsSuccessStatusCode)
                    {
                        var fileName = response.Content.Headers.ContentDisposition?.FileName
                            ?? "attachment";
                        var bytes = await response.Content.ReadAsByteArrayAsync();
                        var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);

                        await File.WriteAllBytesAsync(filePath, bytes);
                        await Launcher.OpenAsync(new OpenFileRequest
                        {
                            File = new ReadOnlyFile(filePath)
                        });
                    }
                    else
                    {
                        await DisplayAlert("Ошибка", "Не удалось загрузить файл", "OK");
                    }
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Ошибка", "Не удалось открыть файл", "OK");
                }
            }
        }

        private void OnEditMessageClicked(object sender, EventArgs e)
        {
            if (sender is Button button && button.CommandParameter is MessageDto message)
            {
                _messageBeingEdited = message;
                EditMessageEntry.Text = message.Content;
                EditMessagePanel.IsVisible = true;
            }
        }

        private void OnCancelEditClicked(object sender, EventArgs e)
        {
            _messageBeingEdited = null;
            EditMessageEntry.Text = string.Empty;
            EditMessagePanel.IsVisible = false;
        }

        private async void OnSaveEditClicked(object sender, EventArgs e)
        {
            if (_messageBeingEdited == null || string.IsNullOrWhiteSpace(EditMessageEntry.Text))
                return;

            try
            {
                var editDto = new EditMessageDto
                {
                    Content = EditMessageEntry.Text
                };

                var response = await _httpClient.PutAsJsonAsync(
                    $"api/Message/{_messageBeingEdited.Id}", editDto);

                if (response.IsSuccessStatusCode)
                {
                    // Обновляем сообщение локально
                    _messageBeingEdited.Content = EditMessageEntry.Text;

                    // Обновляем UI
                    var index = Messages.IndexOf(_messageBeingEdited);
                    if (index != -1)
                    {
                        Messages[index] = _messageBeingEdited;
                    }

                    // Закрываем панель редактирования
                    OnCancelEditClicked(null, null);
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    await DisplayAlert("Ошибка", error, "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Ошибка", "Не удалось отредактировать сообщение", "OK");
                Debug.WriteLine($"Edit message error: {ex}");
            }
        }
    }
}