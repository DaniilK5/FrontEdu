using FrontEdu.Models.Assignments;
using FrontEdu.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
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
namespace FrontEdu.Views;

[QueryProperty(nameof(AssignmentId), "assignmentId")]
public partial class AssignmentDetailsPage : ContentPage
{
    private HttpClient _httpClient;
    private int _assignmentId;
    private bool _isTeacher;
    private bool _isLoading;
    private ObservableCollection<SubmissionInfo> _submissions;
    private IServiceProvider ServiceProvider => IPlatformApplication.Current?.Services;

    public int AssignmentId
    {
        get => _assignmentId;
        set
        {
            if (_assignmentId != value)
            {
                _assignmentId = value;
                // Загрузка будет происходить в OnAppearing
            }
        }
    }
    public AssignmentDetailsPage()
    {
        try
        {
            InitializeComponent();
            _submissions = new ObservableCollection<SubmissionInfo>();
            SubmissionsCollection.ItemsSource = _submissions;

            // Добавляем обработчик аппаратной кнопки "Назад"
            Shell.Current.Navigating += Current_Navigating;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Constructor error: {ex}");
        }
    }


    private void Current_Navigating(object sender, ShellNavigatingEventArgs e)
    {
        if (e.Current?.Location?.ToString().Contains("AssignmentDetailsPage") == true)
        {
            ResetState();
        }
    }
    protected override bool OnBackButtonPressed()
    {
        try
        {
            Shell.Current.Navigating -= Current_Navigating;
            Shell.Current.GoToAsync("/MainPage").ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Back navigation error: {ex}");
            return base.OnBackButtonPressed();
        }
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await InitializeAsync();
        if (_httpClient != null) // Добавляем проверку
        {
            await LoadAssignmentDetails();
        }
    }


    private async Task InitializeAsync()
    {
        try
        {
            // Пересоздаем HttpClient при каждой инициализации
            _httpClient = await AppConfig.CreateHttpClientAsync();

            // Проверяем роль пользователя
            var token = await SecureStorage.GetAsync("auth_token");
            if (!string.IsNullOrEmpty(token))
            {
                var handler = new JwtSecurityTokenHandler();
                var jsonToken = handler.ReadToken(token) as JwtSecurityToken;
                var role = jsonToken?.Claims.FirstOrDefault(c => c.Type.Contains("role"))?.Value;
                _isTeacher = role == "Teacher";

                if (_isTeacher)
                {
                    await LoadSubmissions();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Initialize error: {ex}");
            await DisplayAlert("Ошибка", "Не удалось инициализировать страницу", "OK");
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        ResetState();
    }

    private void ResetState()
    {
        _submissions?.Clear();
        _httpClient = null;
        _isLoading = false;
    }

    private async Task LoadAssignmentDetails()
    {
        if (_isLoading) return;

        try
        {
            _isLoading = true;
            LoadingIndicator.IsVisible = true;

            var response = await _httpClient.GetAsync($"api/Assignment/{AssignmentId}");
            if (response.IsSuccessStatusCode)
            {
                var assignment = await response.Content.ReadFromJsonAsync<AssignmentDetailsResponse>();
                if (assignment != null)
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        TitleLabel.Text = assignment.Title;
                        DescriptionLabel.Text = assignment.Description;
                        DeadlineLabel.Text = assignment.Deadline.ToString("dd.MM.yyyy HH:mm");
                        InstructorLabel.Text = assignment.Instructor?.FullName ?? "-";

                        AttachmentPanel.IsVisible = assignment.HasAttachment;
                        Title = $"Задание: {assignment.Title}";
                    });
                }
            }
            else
            {
                await DisplayAlert("Ошибка", "Не удалось загрузить детали задания", "OK");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading assignment details: {ex}");
            await DisplayAlert("Ошибка", "Не удалось загрузить детали задания", "OK");
        }
        finally
        {
            _isLoading = false;
            LoadingIndicator.IsVisible = false;
        }
    }

    private async Task LoadSubmissions()
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/Assignment/{AssignmentId}/submissions");
            if (response.IsSuccessStatusCode)
            {
                var submissions = await response.Content.ReadFromJsonAsync<List<SubmissionInfo>>();

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    _submissions.Clear();
                    SubmissionsPanel.IsVisible = true;

                    if (submissions != null)
                    {
                        foreach (var submission in submissions)
                        {
                            _submissions.Add(submission);
                        }
                    }
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading submissions: {ex}");
        }
    }

    private async void OnDownloadClicked(object sender, EventArgs e)
    {
        try
        {
            LoadingIndicator.IsVisible = true;

            var response = await _httpClient.GetAsync($"api/Assignment/{AssignmentId}/file");
            if (response.IsSuccessStatusCode)
            {
                var fileName = response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                    ?? $"assignment_{AssignmentId}";
                var bytes = await response.Content.ReadAsByteArrayAsync();

                // Используем методы сохранения файла из DirectChatPage
                if (DeviceInfo.Platform == DevicePlatform.WinUI)
                {
                    await SaveFileWindows(fileName, bytes);
                }
                else if (DeviceInfo.Platform == DevicePlatform.Android)
                {
                    var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
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
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Download error: {ex}");
            await DisplayAlert("Ошибка", "Не удалось скачать файл", "OK");
        }
        finally
        {
            LoadingIndicator.IsVisible = false;
        }
    }


    private async void OnSubmissionFileClicked(object sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is int submissionId)
        {
            try
            {
                LoadingIndicator.IsVisible = true;

                var response = await _httpClient.GetAsync($"api/Assignment/submission/{submissionId}/file");
                if (response.IsSuccessStatusCode)
                {
                    var fileName = response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                        ?? $"submission_{submissionId}";
                    var bytes = await response.Content.ReadAsByteArrayAsync();

                    if (DeviceInfo.Platform == DevicePlatform.WinUI)
                    {
                        await SaveFileWindows(fileName, bytes);
                    }
                    else if (DeviceInfo.Platform == DevicePlatform.Android)
                    {
                        var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
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
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Download submission error: {ex}");
                await DisplayAlert("Ошибка", "Не удалось скачать файл решения", "OK");
            }
            finally
            {
                LoadingIndicator.IsVisible = false;
            }
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

    private async void OnGradeSubmissionClicked(object sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is int submissionId)
        {
            try
            {
                // Запрашиваем оценку
                string gradeStr = await DisplayPromptAsync(
                    "Оценка решения",
                    "Введите оценку (0-100)",
                    maxLength: 3,
                    keyboard: Keyboard.Numeric);

                if (string.IsNullOrEmpty(gradeStr)) return;

                if (!int.TryParse(gradeStr, out int grade) || grade < 0 || grade > 100)
                {
                    await DisplayAlert("Ошибка", "Оценка должна быть числом от 0 до 100", "OK");
                    return;
                }

                // Запрашиваем комментарий
                string comment = await DisplayPromptAsync(
                    "Оценка решения",
                    "Добавьте комментарий (необязательно)",
                    maxLength: 500);

                // Создаем запрос
                var gradeRequest = new GradeSubmissionRequest
                {
                    Value = grade,
                    Comment = comment ?? string.Empty
                };

                // Отправляем оценку
                var response = await _httpClient.PutAsJsonAsync(
                    $"api/Assignment/submissions/{submissionId}/grade",
                    gradeRequest);

                if (response.IsSuccessStatusCode)
                {
                    await DisplayAlert("Успех", "Решение оценено", "OK");
                    await LoadSubmissions(); // Перезагружаем список решений
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    await DisplayAlert("Ошибка", error, "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Grading error: {ex}");
                await DisplayAlert("Ошибка", "Не удалось оценить решение", "OK");
            }
        }
    }
}