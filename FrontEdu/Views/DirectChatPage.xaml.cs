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
                _userId = value;
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await LoadInitialMessages();
                });
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
            }
            catch (Exception ex)
            {
                await DisplayAlert("������", "�� ������� ���������������� �����������", "OK");
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
                await DisplayAlert("������", "�� ������� ��������� ���������", "OK");
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
            try
            {
                _isLoading = true;
                var response = await _httpClient.GetAsync(
                    $"api/Message/direct/{UserId}?page={_currentPage}&pageSize={PageSize}");

                if (response.IsSuccessStatusCode)
                {
                    var messages = await response.Content.ReadFromJsonAsync<List<MessageDto>>();
                    if (messages != null && messages.Any())
                    {
                        foreach (var message in messages)
                        {
                            // �������� ID �������� ������������ �� ������
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
                await DisplayAlert("������", "�� ������� ��������� ���������", "OK");
                Debug.WriteLine($"Load messages error: {ex}");
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
                // ������� MultipartFormDataContent ��� �������� ���������
                var content = new MultipartFormDataContent();
                content.Add(new StringContent(UserId.ToString()), "ReceiverId");
                content.Add(new StringContent(MessageEntry.Text), "Content");

                var response = await _httpClient.PostAsync("api/Message/send", content);
                if (response.IsSuccessStatusCode)
                {
                    MessageEntry.Text = string.Empty;
                    await LoadInitialMessages(); // ������������� ���������
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    await DisplayAlert("������", error, "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("������", "�� ������� ��������� ���������", "OK");
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
                    // ������� MultipartFormDataContent ��� �������� �����
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
                        await DisplayAlert("������", error, "OK");
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("������", "�� ������� ��������� ����", "OK");
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
                        await DisplayAlert("������", "�� ������� ��������� ����", "OK");
                    }
                }
                catch (Exception ex)
                {
                    await DisplayAlert("������", "�� ������� ������� ����", "OK");
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
                    // ��������� ��������� ��������
                    _messageBeingEdited.Content = EditMessageEntry.Text;

                    // ��������� UI
                    var index = Messages.IndexOf(_messageBeingEdited);
                    if (index != -1)
                    {
                        Messages[index] = _messageBeingEdited;
                    }

                    // ��������� ������ ��������������
                    OnCancelEditClicked(null, null);
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    await DisplayAlert("������", error, "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("������", "�� ������� ��������������� ���������", "OK");
                Debug.WriteLine($"Edit message error: {ex}");
            }
        }
    }
}