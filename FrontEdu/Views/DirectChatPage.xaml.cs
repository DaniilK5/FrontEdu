using FrontEdu.Models.Chat;
using FrontEdu.Services;
using Microsoft.Maui.Controls.PlatformConfiguration;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Web;
using Microsoft.Maui.Storage;
using CommunityToolkit.Maui.Storage;
using Debug = System.Diagnostics.Debug;

#if WINDOWS
using Windows.Storage;
using Windows.Storage.Pickers;
using Microsoft.UI.Xaml;
using WinRT.Interop;
#endif
#if ANDROID
using Android.OS;
#endif
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
        private FileResult _selectedFile;
        private IServiceProvider ServiceProvider => IPlatformApplication.Current?.Services;
        public int UserId
        {
            get => _userId;
            set
            {
                if (_userId != value)
                {
                    _userId = value;
                    System.Diagnostics.Debug.WriteLine($"UserId set to: {value}");
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        try
                        {
                            await LoadInitialMessages();
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error loading messages: {ex}");
                            await DisplayAlert("������", "�� ������� ��������� ���������", "OK");
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
                System.Diagnostics.Debug.WriteLine("HttpClient initialized in DirectChatPage");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Connection initialization error: {ex}");
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await DisplayAlert("������", "�� ������� ���������������� �����������", "OK");
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
            if (_httpClient == null)
            {
                _httpClient = await AppConfig.CreateHttpClientAsync();
            }

            try
            {
                _isLoading = true;
                var response = await _httpClient.GetAsync(
                    $"api/Message/direct/{UserId}?page={_currentPage}&pageSize={PageSize}");

                System.Diagnostics.Debug.WriteLine($"�������� ���������: {response.StatusCode}");
                var responseContent = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"���������� ������: {responseContent}");

                if (response.IsSuccessStatusCode)
                {
                    var messages = await response.Content.ReadFromJsonAsync<List<MessageDto>>();
                    if (messages != null && messages.Any())
                    {
                        await MainThread.InvokeOnMainThreadAsync(async () =>
                        {
                            // ������� �������� ������� ������ ���������
                            var currentIndex = Messages.Count > 0 ? Messages.Count - 1 : 0;

                            foreach (var message in messages.OrderBy(m => m.Timestamp)) // ��������� �� �������
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

                                if (_currentPage == 1)
                                {
                                    // ��� ������ �������� ��������� � �����
                                    Messages.Add(message);
                                }
                                else
                                {
                                    // ��� ����������� ������� ��������� � ������
                                    Messages.Insert(0, message);
                                }
                            }

                            // ������������ � ������ �������
                            if (_currentPage == 1)
                            {
                                // ��� ������ �������� ������������ � ����� ���
                                MessagesCollection.ScrollTo(Messages.Count - 1);
                            }
                            else
                            {
                                // ��� ��������� ������ ��������� ��������� �������
                                MessagesCollection.ScrollTo(currentIndex + PageSize);
                            }
                        });
                    }
                }
                else
                {
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        await DisplayAlert("������", responseContent, "OK");
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"������ �������� ���������: {ex}");
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await DisplayAlert("������", 
                        $"�� ������� ��������� ���������: {ex.Message}", "OK");
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
            {
                await DisplayAlert("������", "������� ����� ���������", "OK");
                return;
            }

            try
            {
                var content = new MultipartFormDataContent();
                content.Add(new StringContent(UserId.ToString()), "ReceiverId");
                content.Add(new StringContent(MessageEntry.Text), "Content");
                content.Add(new StringContent(""), "GroupChatId");

                // ���� ���� ��������� ����, ��������� ���
                if (_selectedFile != null)
                {
                    var stream = await _selectedFile.OpenReadAsync();
                    var streamContent = new StreamContent(stream);
                    content.Add(streamContent, "Attachment", _selectedFile.FileName);
                }

                var response = await _httpClient.PostAsync("api/Message/send", content);
                if (response.IsSuccessStatusCode)
                {
                    // ������� ����� � ��������� ����
                    MessageEntry.Text = string.Empty;
                    _selectedFile = null;
                    
                    await LoadInitialMessages();
                    ScrollToLastMessage();
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

        private void UpdateSelectedFileUI()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                SelectedFileLabel.IsVisible = _selectedFile != null;
                SelectedFileLabel.Text = _selectedFile != null 
                    ? $"��������� ����: {_selectedFile.FileName}"
                    : string.Empty;
            });
        }

        private async void OnAttachFileClicked(object sender, EventArgs e)
        {
            try
            {
                var options = new PickOptions
                {
                    PickerTitle = "�������� ����"
                };

                var file = await FilePicker.PickAsync(options);
                if (file != null)
                {
                    _selectedFile = file;
                    UpdateSelectedFileUI();
                    // ���������� ������������
                    await DisplayAlert("���� ������", 
                        "������� ����� ��������� � ������� '���������'", "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"������ ��� ������ �����: {ex}");
                await DisplayAlert("������", "�� ������� ������� ����", "OK");
            }
        }

        private async void OnAttachmentClicked(object sender, EventArgs e)
        {
            if (sender is Button button && button.CommandParameter is int messageId)
            {
                try
                {
                    Debug.WriteLine($"������ �������� ����� ��� ��������� ID: {messageId}");
                    
                    if (_httpClient == null)
                    {
                        _httpClient = await AppConfig.CreateHttpClientAsync();
                    }

                    // ��������� ��������� Accept ���������
                    var request = new HttpRequestMessage(HttpMethod.Get, $"api/Message/file/{messageId}");
                    request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("*/*"));

                    var response = await _httpClient.SendAsync(request);
                    var statusCode = response.StatusCode;
                    Debug.WriteLine($"������ ������: {statusCode}");
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        Debug.WriteLine($"������ ��� �������� �����: {errorContent}");
                        await DisplayAlert("������", $"�� ������� ��������� ����: {errorContent}", "OK");
                        return;
                    }

                    var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
                    var fileName = response.Content.Headers.ContentDisposition?.FileName?.Trim('"') 
                        ?? $"file_{messageId}";
                    
                    Debug.WriteLine($"Content-Type: {contentType}");
                    Debug.WriteLine($"FileName: {fileName}");

                    var bytes = await response.Content.ReadAsByteArrayAsync();
                    if (bytes == null || bytes.Length == 0)
                    {
                        await DisplayAlert("������", "���� ���� ��� ���������", "OK");
                        return;
                    }

                    System.Diagnostics.Debug.WriteLine($"������ �����: {bytes.Length} ����");

                    // ���������� ��������� � �������� ��������������� ��������
                    if (DeviceInfo.Platform == DevicePlatform.WinUI)
                    {
                        await SaveFileWindows(fileName, bytes);
                    }
                    else if (DeviceInfo.Platform == DevicePlatform.Android)
                    {
                        await SaveFileAndroid(fileName, bytes, contentType);
                    }
                    else
                    {
                        var tempPath = Path.Combine(FileSystem.CacheDirectory, fileName);
                        await File.WriteAllBytesAsync(tempPath, bytes);
                        await Launcher.OpenAsync(new OpenFileRequest
                        {
                            File = new ReadOnlyFile(tempPath)
                        });
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"������ ��� �������� �����: {ex}");
                    await DisplayAlert("������", 
                        $"�� ������� ��������� ����: {ex.Message}", "OK");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("������������ ��������� ��� �������� �����");
                await DisplayAlert("������", "�� ������� ���������� ���� ��� ��������", "OK");
            }
        }

        private async Task SaveFileWindows(string fileName, byte[] fileData)
        {
#if WINDOWS
    try
    {
        var filePicker = new FileSavePicker
        {
            SuggestedFileName = fileName
        };

        // �������� ���������� �����
        string extension = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(extension))
        {
            extension = ".bin"; // ���� ���������� �����������, ���������� .bin
        }
        else if (!extension.StartsWith("."))
        {
            extension = "." + extension; // ��������� �����, ���� � ���
        }

        // ��������� ������ ������ � ���������� �����������
        var fileTypes = new List<string> { extension };
        filePicker.FileTypeChoices.Add("����" + extension, fileTypes);
        // ��������� ����� "��� �����" ��� �������� �������
        filePicker.FileTypeChoices.Add("��� �����", new List<string> { "." });

        var window = App.Current.Windows[0].Handler.PlatformView as Microsoft.UI.Xaml.Window;
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        WinRT.Interop.InitializeWithWindow.Initialize(filePicker, hwnd);

        var file = await filePicker.PickSaveFileAsync();
        if (file != null)
        {
            await File.WriteAllBytesAsync(file.Path, fileData);
            await DisplayAlert("�����", "���� ������� ��������", "OK");
        }
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"Windows save error: {ex}");
        await DisplayAlert("������", "�� ������� ��������� ����", "OK");
    }
#else
    await DisplayAlert("������", "���������� ������ �� �������������� �� ���� ���������", "OK");
#endif
        }

        private async Task SaveFileAndroid(string fileName, byte[] fileData, string contentType)
        {
#if ANDROID
    try
    {
        contentType ??= "application/octet-stream";

        if (OperatingSystem.IsAndroidVersionAtLeast(29)) // Android 10 � ����
        {
            using var stream = new MemoryStream(fileData);
            var fileSaver = ServiceProvider?.GetService<IFileSaver>();
            
            if (fileSaver == null)
            {
                // ���� ������ �� ������, ���������� ����������� FileSaver
                fileSaver = FileSaver.Default;
            }

            var result = await fileSaver.SaveAsync(fileName, stream, 
                new CancellationTokenSource().Token);
            
            if (result.IsSuccessful)
            {
                await DisplayAlert("�����", "���� ������� ��������", "OK");
            }
            else
            {
                throw new Exception("�� ������� ��������� ����");
            }
        }
        else // Android 9 � ����
        {
            // ����������� ���������� �� ������
            var status = await Permissions.RequestAsync<Permissions.StorageWrite>();
            if (status != PermissionStatus.Granted)
            {
                throw new Exception("��� ���������� �� ���������� ������");
            }

            var downloadPath = Android.OS.Environment.GetExternalStoragePublicDirectory(
                Android.OS.Environment.DirectoryDownloads)?.AbsolutePath;
            if (string.IsNullOrEmpty(downloadPath))
            {
                throw new DirectoryNotFoundException("����� �������� �� �������");
            }

            var filePath = Path.Combine(downloadPath, fileName);
            await File.WriteAllBytesAsync(filePath, fileData);
            await DisplayAlert("�����", "���� �������� � ����� ��������", "OK");
        }
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"Android save error: {ex}");
        await DisplayAlert("������", $"�� ������� ��������� ����: {ex.Message}", "OK");
    }
#else
    await DisplayAlert("������", "���������� ������ �� �������������� �� ���� ���������", "OK");
#endif
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
                System.Diagnostics.Debug.WriteLine($"Edit message error: {ex}");
            }
        }

        private void ScrollToLastMessage()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (Messages.Any())
                {
                    MessagesCollection.ScrollTo(Messages.Count - 1, animate: true);
                }
            });
        }
        /*
        private async void OnBackClicked(object sender, EventArgs e)
        {
            try
            {
                await Shell.Current.GoToAsync("//ChatPage");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Navigation error: {ex}");
                // � ������ ������ ������� �������������� ����
                try
                {
                    await Shell.Current.GoToAsync("..");
                }
                catch (Exception navEx)
                {
                    Debug.WriteLine($"Alternative navigation error: {navEx}");
                    // ���� � ��� �� ��������, ������ ������������� ���
                    await Shell.Current.GoToAsync("//chat");
                }
            }
        }
        */
    }
}