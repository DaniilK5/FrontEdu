using CommunityToolkit.Maui.Storage;
using FrontEdu.Models.Chat;
using FrontEdu.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
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
using Microsoft.Extensions.DependencyInjection;

namespace FrontEdu.Views
{
    [QueryProperty(nameof(GroupChatId), "groupChatId")]
    public partial class GroupChatPage : ContentPage
    {
        private HttpClient _httpClient;
        private ObservableCollection<MessageDto> Messages { get; set; }
        private int _currentPage = 1;
        private const int PageSize = 20;
        private bool _isLoading;
        private int _groupChatId;
        private MessageDto _messageBeingEdited;
        private FileResult _selectedFile;
        private GroupChatDto _currentChat;
        private ObservableCollection<ChatUserDto> _members;
        private bool _isCreator;

        private IServiceProvider ServiceProvider => IPlatformApplication.Current?.Services;

        public int GroupChatId
        {
            get => _groupChatId;
            set
            {
                if (_groupChatId != value)
                {
                    _groupChatId = value;
                    LoadGroupChatInfo();
                    UpdateGroupInfo();
                }
            }
        }

        public GroupChatPage()
        {
            InitializeComponent();
            Messages = new ObservableCollection<MessageDto>();
            MessagesCollection.ItemsSource = Messages;
        }
        private async void OnLoadMoreMessages(object sender, EventArgs e)
        {
            if (!_isLoading)
            {
                _currentPage++;
                await LoadMessages();
            }
        }
        private async Task LoadGroupChatInfo()
        {
            try
            {
                if (_httpClient == null)
                {
                    _httpClient = await AppConfig.CreateHttpClientAsync();
                }

                var response = await _httpClient.GetAsync($"api/Message/groups/{GroupChatId}");
                if (response.IsSuccessStatusCode)
                {
                    _currentChat = await response.Content.ReadFromJsonAsync<GroupChatDto>();
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        Title = _currentChat.Name;
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading group chat info: {ex}");
            }
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            try
            {
                if (_httpClient == null)
                {
                    _httpClient = await AppConfig.CreateHttpClientAsync();
                }

                await LoadInitialMessages();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in OnAppearing: {ex}");
                await DisplayAlert("Ошибка", "Не удалось загрузить сообщения", "OK");
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

        private async Task LoadMessages()
        {
            if (_httpClient == null || _isLoading)
                return;

            try
            {
                _isLoading = true;
                var response = await _httpClient.GetAsync(
                    $"api/Message/group/{GroupChatId}?page={_currentPage}&pageSize={PageSize}");

                if (response.IsSuccessStatusCode)
                {
                    var messages = await response.Content.ReadFromJsonAsync<List<MessageDto>>();
                    if (messages != null && messages.Any())
                    {
                        await MainThread.InvokeOnMainThreadAsync(async () =>
                        {
                            var currentIndex = Messages.Count > 0 ? Messages.Count - 1 : 0;

                            foreach (var message in messages.OrderBy(m => m.Timestamp))
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
                                    Messages.Add(message);
                                else
                                    Messages.Insert(0, message);
                            }

                            if (_currentPage == 1)
                                MessagesCollection.ScrollTo(Messages.Count - 1);
                            else
                                MessagesCollection.ScrollTo(currentIndex + PageSize);
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading messages: {ex}");
                await DisplayAlert("Ошибка", "Не удалось загрузить сообщения", "OK");
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
                await DisplayAlert("Ошибка", "Введите текст сообщения", "OK");
                return;
            }

            try
            {
                var content = new MultipartFormDataContent();
                content.Add(new StringContent(GroupChatId.ToString()), "GroupChatId");
                content.Add(new StringContent(MessageEntry.Text), "Content");

                if (_selectedFile != null)
                {
                    var stream = await _selectedFile.OpenReadAsync();
                    var streamContent = new StreamContent(stream);
                    content.Add(streamContent, "Attachment", _selectedFile.FileName);
                }

                var response = await _httpClient.PostAsync("api/Message/send", content);
                if (response.IsSuccessStatusCode)
                {
                    MessageEntry.Text = string.Empty;
                    ClearSelectedFile();
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
                Debug.WriteLine($"Send message error: {ex}");
                await DisplayAlert("Ошибка", "Не удалось отправить сообщение", "OK");
            }
        }


        private void UpdateSelectedFileUI()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                SelectedFileLabel.IsVisible = _selectedFile != null;
                if (_selectedFile != null)
                {
                    SelectedFileLabel.Text = $"Прикреплённый файл: {_selectedFile.FileName}";
                }
            });
        }

        private async void OnAttachFileClicked(object sender, EventArgs e)
        {
            try
            {
                var options = new PickOptions
                {
                    PickerTitle = "Выберите файл"
                };

                var file = await FilePicker.PickAsync(options);
                if (file != null)
                {
                    _selectedFile = file;
                    UpdateSelectedFileUI();
                    // Уведомляем пользователя
                    await DisplayAlert("Файл выбран",
                        "Введите текст сообщения и нажмите 'Отправить'", "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка при выборе файла: {ex}");
                await DisplayAlert("Ошибка", "Не удалось выбрать файл", "OK");
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
                    var fileSaver = ServiceProvider?.GetService<IFileSaver>();

                    if (fileSaver == null)
                    {
                        // Если сервис не найден, используем стандартный FileSaver
                        fileSaver = FileSaver.Default;
                    }

                    var result = await fileSaver.SaveAsync(fileName, stream,
                        new CancellationTokenSource().Token);

                    if (result.IsSuccessful)
                    {
                        await DisplayAlert("Успех", "Файл успешно сохранен", "OK");
                    }
                    else
                    {
                        throw new Exception("Не удалось сохранить файл");
                    }
                }
                else // Android 9 и ниже
                {
                    // Запрашиваем разрешение на запись
                    var status = await Permissions.RequestAsync<Permissions.StorageWrite>();
                    if (status != PermissionStatus.Granted)
                    {
                        throw new Exception("Нет разрешения на сохранение файлов");
                    }

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
                await DisplayAlert("Ошибка", $"Не удалось сохранить файл: {ex.Message}", "OK");
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
        // Метод для очистки выбранного файла
        private void ClearSelectedFile()
        {
            _selectedFile = null;
            UpdateSelectedFileUI();
        }



        // Обработчик нажатия кнопки очистки файла
        private void OnClearFileClicked(object sender, EventArgs e)
        {
            ClearSelectedFile();
        }


        private async void OnUserNameTapped(object sender, TappedEventArgs e)
        {
            if (e.Parameter is int userId)
            {
                try
                {
                    var navigationParameter = new Dictionary<string, object>
            {
                { "userId", userId }
            };
                    await Shell.Current.GoToAsync($"/ProfileViewPage", navigationParameter);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Navigation error: {ex}");
                    await DisplayAlert("Ошибка", "Не удалось открыть профиль пользователя", "OK");
                }
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

        private void ResetState()
        {
            Messages.Clear();
            _messageBeingEdited = null;
            _selectedFile = null;
            _currentPage = 1;
            _isLoading = false;
            ClearSelectedFile();
            EditMessagePanel.IsVisible = false;
            MessageEntry.Text = string.Empty;
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            ResetState();
        }

        private async Task UpdateGroupInfo()
        {
            try
            {
                if (_httpClient == null)
                {
                    _httpClient = await AppConfig.CreateHttpClientAsync();
                }

                var response = await _httpClient.GetAsync($"api/Message/groups/{GroupChatId}");
                if (response.IsSuccessStatusCode)
                {
                    _currentChat = await response.Content.ReadFromJsonAsync<GroupChatDto>();

                    var token = await SecureStorage.GetAsync("auth_token");
                    if (!string.IsNullOrEmpty(token))
                    {
                        var handler = new JwtSecurityTokenHandler();
                        var jsonToken = handler.ReadToken(token) as JwtSecurityToken;
                        var currentUserId = int.Parse(jsonToken?.Claims.FirstOrDefault(c =>
                            c.Type.Contains("nameidentifier"))?.Value ?? "0");

                        _isCreator = _currentChat.Creator.Id == currentUserId;
                    }

                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        Title = _currentChat.Name;
                        GroupNameLabel.Text = _currentChat.Name;
                        MembersCountLabel.Text = $"{_currentChat.Members.Count} участников";

                        _members = new ObservableCollection<ChatUserDto>(_currentChat.Members);
                        foreach (var member in _members)
                        {
                            member.CanBeRemoved = _isCreator && member.Id != _currentChat.Creator.Id;
                        }
                        MembersCollection.ItemsSource = _members;
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating group info: {ex}");
                await DisplayAlert("Ошибка", "Не удалось обновить информацию о чате", "OK");
            }
        }

        private async void OnManageMembersClicked(object sender, EventArgs e)
        {
            await UpdateGroupInfo();
            ManageMembersPanel.IsVisible = true;
        }

        private void OnCloseMembersPanelClicked(object sender, EventArgs e)
        {
            ManageMembersPanel.IsVisible = false;
        }

        private async void OnRemoveMemberClicked(object sender, EventArgs e)
        {
            if (sender is Button button && button.CommandParameter is int memberId)
            {
                bool confirm = await DisplayAlert("Подтверждение",
                    "Вы уверены, что хотите удалить участника?", "Да", "Нет");

                if (!confirm) return;

                try
                {
                    var response = await _httpClient.DeleteAsync(
                        $"api/Message/groups/{GroupChatId}/members/{memberId}");

                    if (response.IsSuccessStatusCode)
                    {
                        var memberToRemove = _members.FirstOrDefault(m => m.Id == memberId);
                        if (memberToRemove != null)
                        {
                            _members.Remove(memberToRemove);
                        }
                        await UpdateGroupInfo();
                    }
                    else
                    {
                        var error = await response.Content.ReadAsStringAsync();
                        await DisplayAlert("Ошибка", error, "OK");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Remove member error: {ex}");
                    await DisplayAlert("Ошибка", "Не удалось удалить участника", "OK");
                }
            }
        }

        private async void OnAddMembersClicked(object sender, EventArgs e)
        {
            try
            {
                // Получаем список всех пользователей
                var response = await _httpClient.GetAsync("api/Message/users");
                if (response.IsSuccessStatusCode)
                {
                    var allUsers = await response.Content.ReadFromJsonAsync<List<ChatUserDto>>();

                    // Исключаем текущих участников
                    var availableUsers = allUsers.Where(u =>
                        !_members.Any(m => m.Id == u.Id)).ToList();

                    if (!availableUsers.Any())
                    {
                        await DisplayAlert("Информация",
                            "Нет доступных пользователей для добавления", "OK");
                        return;
                    }

                    // Создаем список для выбора пользователей
                    var userNames = availableUsers.Select(u => u.FullName).ToArray();
                    string selection = await DisplayActionSheet(
                        "Выберите пользователя", "Отмена", null, userNames);

                    if (!string.IsNullOrEmpty(selection) && selection != "Отмена")
                    {
                        var selectedUser = availableUsers.FirstOrDefault(u => u.FullName == selection);
                        if (selectedUser != null)
                        {
                            // Отправляем запрос на добавление участника
                            var addMemberResponse = await _httpClient.PostAsync(
                                $"api/Message/groups/{GroupChatId}/members/{selectedUser.Id}", null);

                            if (addMemberResponse.IsSuccessStatusCode)
                            {
                                await UpdateGroupInfo();
                            }
                            else
                            {
                                var error = await addMemberResponse.Content.ReadAsStringAsync();
                                await DisplayAlert("Ошибка", error, "OK");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Add member error: {ex}");
                await DisplayAlert("Ошибка", "Не удалось добавить участника", "OK");
            }
        }
    }
}