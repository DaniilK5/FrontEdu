using CommunityToolkit.Maui.Storage;
using FrontEdu.Models.Chat;
using FrontEdu.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using FrontEdu.Models.GroupChat;
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
        private bool _isLoading;
        private int _groupChatId;
        private MessageDto _messageBeingEdited;
        private FileResult _selectedFile;
        private GroupChatDto _currentChat;
        private ObservableCollection<ChatUserDto> _members;
        private bool _isCreator;
        private GroupChatDetailsResponse _chatDetails;

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

        private async Task LoadGroupChatInfo()
        {
            try
            {
                if (_httpClient == null)
                {
                    _httpClient = await AppConfig.CreateHttpClientAsync();
                }

                var response = await _httpClient.GetAsync($"api/GroupChat/{GroupChatId}");
                if (response.IsSuccessStatusCode)
                {
                    var chatDetails = await response.Content.ReadFromJsonAsync<GroupChatDetailsResponse>();
                    if (chatDetails != null)
                    {
                        await MainThread.InvokeOnMainThreadAsync(() =>
                        {
                            Title = chatDetails.Name;
                            GroupNameLabel.Text = chatDetails.Name;
                            MembersCountLabel.Text = $"{chatDetails.Statistics.TotalMembers} участников";

                            // Преобразуем информацию о чате в текущий формат
                            _currentChat = new GroupChatDto
                            {
                                Id = chatDetails.Id,
                                Name = chatDetails.Name,
                                CreatedAt = chatDetails.CreatedAt,
                                Members = chatDetails.Members.Select(m => new ChatUserDto
                                {
                                    Id = m.UserId,
                                    FullName = m.FullName,
                                    Email = m.Email,
                                    Role = m.IsAdmin ? "Administrator" : "Member",
                                    CanBeRemoved = chatDetails.CurrentUserInfo.IsAdmin && !m.IsAdmin
                                }).ToList()
                            };

                            // Обновляем информацию о текущем пользователе
                            _isCreator = chatDetails.CurrentUserInfo.IsAdmin;

                            // Обновляем список участников
                            _members = new ObservableCollection<ChatUserDto>(_currentChat.Members);
                            MembersCollection.ItemsSource = _members;

                            // Обновляем видимость кнопок управления для администратора
                            if (ManageMembersButton != null)
                            {
                                ManageMembersButton.IsVisible = chatDetails.CurrentUserInfo.IsAdmin;
                            }
                        });
                    }
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"Error loading group chat info: {error}");
                    await DisplayAlert("Ошибка", "Не удалось загрузить информацию о чате", "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading group chat info: {ex}");
                await DisplayAlert("Ошибка", "Не удалось загрузить информацию о чате", "OK");
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
                var response = await _httpClient.GetAsync($"api/GroupChat/{GroupChatId}/messages");

                if (response.IsSuccessStatusCode)
                {
                    var chatData = await response.Content.ReadFromJsonAsync<GroupChatMessagesResponse>();
                    if (chatData?.Messages != null && chatData.Messages.Any())
                    {
                        await MainThread.InvokeOnMainThreadAsync(async () =>
                        {
                            // Получаем ID текущего пользователя для определения своих сообщений
                            var token = await SecureStorage.GetAsync("auth_token");
                            var currentUserId = 0;
                            if (!string.IsNullOrEmpty(token))
                            {
                                var handler = new JwtSecurityTokenHandler();
                                var jsonToken = handler.ReadToken(token) as JwtSecurityToken;
                                currentUserId = int.Parse(jsonToken?.Claims.FirstOrDefault(c =>
                                    c.Type.Contains("nameidentifier"))?.Value ?? "0");
                            }

                            // Очищаем текущие сообщения
                            Messages.Clear();

                            // Добавляем сообщения
                            foreach (var message in chatData.Messages.OrderBy(m => m.Timestamp))
                            {
                                message.IsFromCurrentUser = message.Sender.Id == currentUserId;
                                Messages.Add(message);
                            }

                            // Прокручиваем к последнему сообщению
                            ScrollToLastMessage();
                        });

                        // Обновляем информацию о чате
                        Title = chatData.Name;
                        GroupNameLabel.Text = chatData.Name;
                        MembersCountLabel.Text = $"{chatData.Members.Count} участников";
                    }
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"Error loading messages: {error}");
                    await DisplayAlert("Ошибка", "Не удалось загрузить сообщения", "OK");
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
                    await LoadMessages(); // Изменено с LoadInitialMessages на LoadMessages
                    ScrollToLastMessage(); // Добавлен вызов прокрутки
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
            _isLoading = false;
            ClearSelectedFile();
            EditMessagePanel.IsVisible = false;
            ChatInfoPanel.IsVisible = false; // Добавим скрытие панели информации
            ManageMembersPanel.IsVisible = false;
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

        private async Task LoadMembersAsync()
        {
            if (_httpClient == null)
            {
                _httpClient = await AppConfig.CreateHttpClientAsync();
            }

            var response = await _httpClient.GetAsync($"api/GroupChat/{GroupChatId}/members");
            if (response.IsSuccessStatusCode)
            {
                var members = await response.Content.ReadFromJsonAsync<List<GroupChatMemberInfo>>();

                // Получаем ID и роль текущего пользователя
                var token = await SecureStorage.GetAsync("auth_token");
                var currentUserId = 0;
                var isCurrentUserAdmin = false; // Добавляем объявление переменной

                if (!string.IsNullOrEmpty(token))
                {
                    var handler = new JwtSecurityTokenHandler();
                    var jsonToken = handler.ReadToken(token) as JwtSecurityToken;
                    currentUserId = int.Parse(jsonToken?.Claims.FirstOrDefault(c =>
                        c.Type.Contains("nameidentifier"))?.Value ?? "0");
                    
                    // Определяем, является ли текущий пользователь администратором
                    isCurrentUserAdmin = members.FirstOrDefault(m => m.UserId == currentUserId)?.IsAdmin ?? false;
                }

                // Обновляем UI синхронно
                _members = new ObservableCollection<ChatUserDto>(
                    members.Select(m => new ChatUserDto
                    {
                        Id = m.UserId,
                        FullName = m.FullName,
                        Email = m.Email,
                        Role = m.IsAdmin ? "Administrator" : "Member",
                        // Пользователь может быть удален, если:
                        // 1. Текущий пользователь админ
                        // 2. Удаляемый пользователь не админ
                        // 3. Удаляемый пользователь не является текущим пользователем
                        CanBeRemoved = isCurrentUserAdmin && !m.IsAdmin && m.UserId != currentUserId
                    }));

                // Обновляем UI
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    MembersCollection.ItemsSource = _members;
                    ManageMembersPanel.IsVisible = true;
                });
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"Error loading members: {error}");
                await DisplayAlert("Ошибка", "Не удалось загрузить список участников", "OK");
            }
        }

        private async void OnManageMembersClicked(object sender, EventArgs e)
        {
            try
            {
                await LoadMembersAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in OnManageMembersClicked: {ex}");
                await DisplayAlert("Ошибка", "Не удалось загрузить список участников", "OK");
            }
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
                        $"api/GroupChat/{GroupChatId}/members/{memberId}");

                    if (response.IsSuccessStatusCode)
                    {
                        var memberToRemove = _members.FirstOrDefault(m => m.Id == memberId);
                        if (memberToRemove != null)
                        {
                            _members.Remove(memberToRemove);
                        }
                        // Обновляем список участников после успешного удаления
                        await LoadMembersAsync();
                    }
                    else
                    {
                        var error = await response.Content.ReadAsStringAsync();
                        Debug.WriteLine($"Error removing member: {error}");
                        await DisplayAlert("Ошибка", "Не удалось удалить участника", "OK");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Remove member error: {ex}");
                    await DisplayAlert("Ошибка", "Не удалось удалить участника", "OK");
                }
            }
        }


        // Добавим метод для отображения информации о чате
        private async void OnChatInfoClicked(object sender, EventArgs e)
        {
            try
            {
                if (_httpClient == null)
                {
                    _httpClient = await AppConfig.CreateHttpClientAsync();
                }

                var response = await _httpClient.GetAsync($"api/GroupChat/{GroupChatId}");
                if (response.IsSuccessStatusCode)
                {
                    _chatDetails = await response.Content.ReadFromJsonAsync<GroupChatDetailsResponse>();
                    
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        // Обновляем информацию в панели
                        CreatedAtLabel.Text = $"Создан: {_chatDetails.CreatedAt:dd.MM.yyyy HH:mm}";
                        
                        DetailedStatisticsLabel.Text = 
                            $"Всего участников: {_chatDetails.Statistics.TotalMembers}\n" +
                            $"Администраторов: {_chatDetails.Statistics.AdminsCount}\n" +
                            $"Всего сообщений: {_chatDetails.Statistics.TotalMessages}\n" +
                            $"Вложений: {_chatDetails.Statistics.TotalAttachments}\n" +
                            $"Непрочитанных: {_chatDetails.Statistics.UnreadMessages}";

                        UserInfoLabel.Text = 
                            $"Роль: {(_chatDetails.CurrentUserInfo.IsAdmin ? "Администратор" : "Участник")}\n" +
                            $"Присоединились: {_chatDetails.CurrentUserInfo.JoinedAt:dd.MM.yyyy HH:mm}\n" +
                            $"Ваших сообщений: {_chatDetails.Statistics.YourMessages}";

                        // Показываем панель
                        ChatInfoPanel.IsVisible = true;
                    });
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    await DisplayAlert("Ошибка", "Не удалось загрузить информацию о чате", "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading chat info: {ex}");
                await DisplayAlert("Ошибка", "Не удалось загрузить информацию о чате", "OK");
            }
        }

        private void OnCloseChatInfoClicked(object sender, EventArgs e)
        {
            ChatInfoPanel.IsVisible = false;
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
                            // Создаем объект запроса
                            var addMemberRequest = new { userId = selectedUser.Id };

                            // Отправляем запрос на добавление участника по новому эндпоинту
                            var addMemberResponse = await _httpClient.PostAsJsonAsync(
                                $"api/GroupChat/{GroupChatId}/AddMember", addMemberRequest);

                            if (addMemberResponse.IsSuccessStatusCode)
                            {
                                // Обновляем информацию о группе после успешного добавления
                                await LoadMembersAsync();
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