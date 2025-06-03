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
                System.Diagnostics.Debug.WriteLine("HttpClient initialized in DirectChatPage");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Connection initialization error: {ex}");
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

                System.Diagnostics.Debug.WriteLine($"Загрузка сообщений: {response.StatusCode}");
                var responseContent = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"Содержимое ответа: {responseContent}");

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
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки сообщений: {ex}");
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
                var content = new MultipartFormDataContent();
                content.Add(new StringContent(UserId.ToString()), "ReceiverId");
                content.Add(new StringContent(MessageEntry.Text), "Content");
                // GroupChatId не указываем, так как это прямое сообщение
                content.Add(new StringContent(""), "GroupChatId");

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
            catch (Exception ex)
            {
                await DisplayAlert("Ошибка", "Не удалось отправить сообщение", "OK");
                System.Diagnostics.Debug.WriteLine($"Send message error: {ex}");
            }
        }

        private async void OnAttachFileClicked(object sender, EventArgs e)
        {
            try
            {
                var file = await FilePicker.PickAsync();
                if (file != null)
                {
                    var content = new MultipartFormDataContent();
                    
                    // Добавляем файл
                    var streamContent = new StreamContent(await file.OpenReadAsync());
                    content.Add(streamContent, "Attachment", file.FileName);
                    
                    // Добавляем получателя
                    content.Add(new StringContent(UserId.ToString()), "ReceiverId");
                    
                    // Добавляем текст сообщения, если он есть
                    if (!string.IsNullOrWhiteSpace(MessageEntry.Text))
                    {
                        content.Add(new StringContent(MessageEntry.Text), "Content");
                    }
                    else
                    {
                        content.Add(new StringContent(""), "Content");
                    }
                    
                    // GroupChatId не указываем для прямого сообщения
                    content.Add(new StringContent(""), "GroupChatId");

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
                System.Diagnostics.Debug.WriteLine($"Send file error: {ex}");
            }
        }

        private async void OnAttachmentClicked(object sender, EventArgs e)
        {
            if (sender is Button button && button.CommandParameter is int messageId)
            {
                try
                {
                    Debug.WriteLine($"Начало загрузки файла для сообщения ID: {messageId}");
                    
                    if (_httpClient == null)
                    {
                        _httpClient = await AppConfig.CreateHttpClientAsync();
                    }

                    // Добавляем обработку Accept заголовка
                    var request = new HttpRequestMessage(HttpMethod.Get, $"api/Message/file/{messageId}");
                    request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("*/*"));

                    var response = await _httpClient.SendAsync(request);
                    var statusCode = response.StatusCode;
                    Debug.WriteLine($"Статус ответа: {statusCode}");
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        Debug.WriteLine($"Ошибка при загрузке файла: {errorContent}");
                        await DisplayAlert("Ошибка", $"Не удалось загрузить файл: {errorContent}", "OK");
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
                        await DisplayAlert("Ошибка", "Файл пуст или поврежден", "OK");
                        return;
                    }

                    System.Diagnostics.Debug.WriteLine($"Размер файла: {bytes.Length} байт");

                    // Определяем платформу и выбираем соответствующее действие
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
                    System.Diagnostics.Debug.WriteLine($"Ошибка при загрузке файла: {ex}");
                    await DisplayAlert("Ошибка", 
                        $"Не удалось загрузить файл: {ex.Message}", "OK");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Некорректные параметры для загрузки файла");
                await DisplayAlert("Ошибка", "Не удалось определить файл для загрузки", "OK");
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

        // Получаем расширение файла
        string extension = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(extension))
        {
            extension = ".bin"; // Если расширение отсутствует, используем .bin
        }
        else if (!extension.StartsWith("."))
        {
            extension = "." + extension; // Добавляем точку, если её нет
        }

        // Добавляем фильтр файлов с корректным расширением
        var fileTypes = new List<string> { extension };
        filePicker.FileTypeChoices.Add("Файл" + extension, fileTypes);
        // Добавляем опцию "Все файлы" как запасной вариант
        filePicker.FileTypeChoices.Add("Все файлы", new List<string> { "." });

        var window = App.Current.Windows[0].Handler.PlatformView as Microsoft.UI.Xaml.Window;
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        WinRT.Interop.InitializeWithWindow.Initialize(filePicker, hwnd);

        var file = await filePicker.PickSaveFileAsync();
        if (file != null)
        {
            await File.WriteAllBytesAsync(file.Path, fileData);
            await DisplayAlert("Успех", "Файл успешно сохранен", "OK");
        }
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"Windows save error: {ex}");
        await DisplayAlert("Ошибка", "Не удалось сохранить файл", "OK");
    }
#else
    await DisplayAlert("Ошибка", "Сохранение файлов не поддерживается на этой платформе", "OK");
#endif
        }

        private async Task SaveFileAndroid(string fileName, byte[] fileData, string contentType)
        {
#if ANDROID
    try
    {
        contentType ??= "application/octet-stream";

        if (OperatingSystem.IsAndroidVersionAtLeast(29)) // Android 10 и выше
        {
            using var stream = new MemoryStream(fileData);
            var fileSaver = ServiceProvider.GetService<IFileSaver>();
            var result = await fileSaver.SaveAsync(fileName, stream, 
                new CancellationTokenSource().Token);
            
            if (result.IsSuccessful)
            {
                await DisplayAlert("Успех", "Файл успешно сохранен", "OK");
            }
        }
        else // Android 9 и ниже
        {
            var downloadPath = Android.OS.Environment.GetExternalStoragePublicDirectory(
                Android.OS.Environment.DirectoryDownloads)?.AbsolutePath;
            if (string.IsNullOrEmpty(downloadPath))
            {
                throw new DirectoryNotFoundException("Папка загрузок не найдена");
            }

            var filePath = Path.Combine(downloadPath, fileName);
            await File.WriteAllBytesAsync(filePath, fileData);
            await DisplayAlert("Успех", "Файл сохранен в папку Загрузки", "OK");
        }
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"Android save error: {ex}");
        throw;
    }
#else
    await DisplayAlert("Ошибка", "Сохранение файлов не поддерживается на этой платформе", "OK");
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
                System.Diagnostics.Debug.WriteLine($"Edit message error: {ex}");
            }
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
                // В случае ошибки пробуем альтернативный путь
                try
                {
                    await Shell.Current.GoToAsync("..");
                }
                catch (Exception navEx)
                {
                    Debug.WriteLine($"Alternative navigation error: {navEx}");
                    // Если и это не работает, просто перезагружаем чат
                    await Shell.Current.GoToAsync("//chat");
                }
            }
        }
        */
    }
}