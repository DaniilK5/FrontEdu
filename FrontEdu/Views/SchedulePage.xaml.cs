using CommunityToolkit.Maui.Storage;
using FrontEdu.Models.Schedule;
using System.Collections.ObjectModel;
using System.Net.Http.Json;
using System.Windows.Input;
using Debug = System.Diagnostics.Debug;
using FrontEdu.Services;
using FrontEdu.Models.Auth;
using FrontEdu.Models.Constants;


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
namespace FrontEdu.Views;

public partial class SchedulePage : ContentPage
{

    private HttpClient _httpClient;
    private bool _isLoading;
    private FileResult _selectedFile;
    private readonly ObservableCollection<ImageDetails> _images;
    private IFileSaver FileSaver { get; }
    public bool CanManageImages { get; private set; }
    public ICommand RefreshCommand { get; }
    public ICommand DeleteCommand { get; }

    private ImageType _currentType = ImageType.Schedule;
    private int? _selectedGroupId;
    private int? _selectedSubjectId;
    private DateTime? _startDate;
    private DateTime? _endDate;

    private string _userRole;
    public bool IsAdministrator
    {
        get => _userRole == UserRoles.Administrator;
        private set
        {
            if (_userRole != (value ? UserRoles.Administrator : _userRole))
            {
                _userRole = value ? UserRoles.Administrator : _userRole;
                OnPropertyChanged();
            }
        }
    }

    public SchedulePage(IFileSaver fileSaver)
    {
        InitializeComponent();
        FileSaver = fileSaver;
        _images = new ObservableCollection<ImageDetails>();
        ImagesCollection.ItemsSource = _images;

        // Установка начальных дат в UTC
        var today = DateTime.UtcNow.Date;
        StartDatePicker.Date = today.AddMonths(-1);
        EndDatePicker.Date = today;
        _startDate = StartDatePicker.Date;
        _endDate = EndDatePicker.Date;

        RefreshCommand = new Command(async () => await LoadImages());
        DeleteCommand = new Command<int>(async (id) => await DeleteImage(id));
        BindingContext = this;

        // Выбираем расписание по умолчанию
        TypePicker.SelectedIndex = 0;
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
            _httpClient = await AppConfig.CreateHttpClientAsync();

            // Получаем разрешения пользователя
            var response = await _httpClient.GetAsync("api/Profile/me/permissions");
            if (response.IsSuccessStatusCode)
            {
                var permissions = await response.Content.ReadFromJsonAsync<UserPermissionsResponse>();
                _userRole = permissions?.Role;
                OnPropertyChanged(nameof(IsAdministrator));

                // Показываем панель загрузки только администраторам
                UploadPanel.IsVisible = IsAdministrator;
            }

            await LoadImages();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Initialize error: {ex}");
            await DisplayAlert("Ошибка", "Не удалось загрузить расписание", "OK");
        }
    }
    private async Task LoadImages()
    {
        if (_isLoading)
        {
            Debug.WriteLine("LoadImages: Already loading images, returning");
            return;
        }

        try
        {
            Debug.WriteLine($"LoadImages: Starting load with parameters:");
            Debug.WriteLine($"- Current type: {_currentType}");
            Debug.WriteLine($"- Selected group ID: {_selectedGroupId}");
            Debug.WriteLine($"- Selected subject ID: {_selectedSubjectId}");
            Debug.WriteLine($"- Start date: {_startDate}");
            Debug.WriteLine($"- End date: {_endDate}");

            _isLoading = true;
            LoadingIndicator.IsVisible = true;

            // Формируем URL в зависимости от типа и фильтров
            var queryParams = new List<string>();

            if (_selectedGroupId.HasValue)
            {
                Debug.WriteLine($"Adding group filter: {_selectedGroupId.Value}");
                queryParams.Add($"groupId={_selectedGroupId}");
            }

            if (_currentType == ImageType.Grades && _selectedSubjectId.HasValue)
            {
                Debug.WriteLine($"Adding subject filter: {_selectedSubjectId.Value}");
                queryParams.Add($"subjectId={_selectedSubjectId}");
            }

            // Преобразуем даты в UTC формат
            if (_startDate.HasValue)
            {
                var utcStartDate = DateTime.SpecifyKind(_startDate.Value.Date, DateTimeKind.Utc);
                Debug.WriteLine($"Adding start date filter: {utcStartDate:o}");
                queryParams.Add($"startDate={utcStartDate:yyyy-MM-ddTHH:mm:ss.fffZ}");
            }

            if (_endDate.HasValue)
            {
                var utcEndDate = DateTime.SpecifyKind(_endDate.Value.Date.AddDays(1).AddSeconds(-1), DateTimeKind.Utc);
                Debug.WriteLine($"Adding end date filter: {utcEndDate:o}");
                queryParams.Add($"endDate={utcEndDate:yyyy-MM-ddTHH:mm:ss.fffZ}");
            }

            var queryString = queryParams.Any() ? $"?{string.Join("&", queryParams)}" : string.Empty;

            var baseUrl = _currentType == ImageType.Schedule
                ? "api/GradeImages/schedule"
                : "api/GradeImages";

            var fullUrl = $"{baseUrl}{queryString}";
            Debug.WriteLine($"LoadImages: Requesting URL: {fullUrl}");

            // Проверяем состояние HTTP-клиента
            if (_httpClient == null)
            {
                Debug.WriteLine("LoadImages: HTTP client is null, creating new instance");
                _httpClient = await AppConfig.CreateHttpClientAsync();
            }

            Debug.WriteLine("LoadImages: Sending GET request");
            var response = await _httpClient.GetAsync(fullUrl);
            Debug.WriteLine($"LoadImages: Received response with status code: {response.StatusCode}");

            var content = await response.Content.ReadAsStringAsync();
            Debug.WriteLine($"LoadImages: Response content: {content}");

            if (response.IsSuccessStatusCode)
            {
                Debug.WriteLine("LoadImages: Successfully received response, processing data");
                _images.Clear();

                if (_currentType == ImageType.Schedule)
                {
                    Debug.WriteLine("LoadImages: Processing schedule images");
                    var result = await response.Content.ReadFromJsonAsync<ScheduleImagesResponse>();
                    Debug.WriteLine($"LoadImages: Deserialized schedule response, found {result?.Schedules?.Count ?? 0} images");

                    if (result?.Schedules != null)
                    {
                        foreach (var image in result.Schedules.OrderByDescending(i => i.UploadedAt))
                        {
                            _images.Add(image);
                            Debug.WriteLine($"LoadImages: Added schedule image: {image.FileName} (ID: {image.Id})");
                        }
                    }
                }
                else // для ImageType.Grades
                {
                    Debug.WriteLine("LoadImages: Processing grade images");
                    var gradesResponse = await response.Content.ReadFromJsonAsync<GradesImagesResponse>();
                    Debug.WriteLine($"LoadImages: Deserialized grades response, found {gradesResponse?.Images?.Count ?? 0} images");

                    if (gradesResponse?.Images != null)
                    {
                        foreach (var image in gradesResponse.Images.OrderByDescending(i => i.UploadedAt))
                        {
                            _images.Add(new ImageDetails
                            {
                                Id = image.Id,
                                FileName = image.FileName,
                                FileType = image.FileType,
                                UploadedAt = image.UploadedAt,
                                Group = image.Group,
                                Uploader = image.Uploader
                            });
                            Debug.WriteLine($"LoadImages: Added grade image: {image.FileName} (ID: {image.Id})");
                        }
                    }
                }

                Debug.WriteLine($"LoadImages: Completed successfully, total images loaded: {_images.Count}");
            }
            else
            {
                Debug.WriteLine($"LoadImages: Request failed with status code {response.StatusCode}");
                Debug.WriteLine($"LoadImages: Error response content: {content}");
                await DisplayAlert("Ошибка", content, "OK");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"LoadImages: Exception occurred: {ex.GetType().Name}");
            Debug.WriteLine($"LoadImages: Exception message: {ex.Message}");
            Debug.WriteLine($"LoadImages: Stack trace: {ex.StackTrace}");

            if (ex.InnerException != null)
            {
                Debug.WriteLine($"LoadImages: Inner exception: {ex.InnerException.Message}");
                Debug.WriteLine($"LoadImages: Inner exception stack trace: {ex.InnerException.StackTrace}");
            }

            await DisplayAlert("Ошибка", "Не удалось загрузить список изображений", "OK");
        }
        finally
        {
            _isLoading = false;
            LoadingIndicator.IsVisible = false;
            Debug.WriteLine("LoadImages: Completed execution");
        }
    }
    private async void OnSelectFileClicked(object sender, EventArgs e)
    {
        try
        {
            var customFileType = new FilePickerFileType(
                new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, new[] {
                        ".jpg", ".jpeg", ".png", // Добавляем форматы изображений
                        ".pdf", ".doc", ".docx", ".xls", ".xlsx"
                    }},
                    { DevicePlatform.Android, new[] {
                        "image/jpeg", "image/png", // Добавляем MIME-типы для изображений
                        "application/pdf",
                        "application/msword",
                        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                        "application/vnd.ms-excel",
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                    }}
                });

            var options = new PickOptions
            {
                PickerTitle = _currentType == ImageType.Schedule
                    ? "Выберите файл расписания"
                    : "Выберите файл с оценками",
                FileTypes = customFileType
            };

            _selectedFile = await FilePicker.PickAsync(options);
            if (_selectedFile != null)
            {
                // Проверяем расширение файла
                var extension = Path.GetExtension(_selectedFile.FileName).ToLowerInvariant();
                var allowedImageExtensions = new[] { ".jpg", ".jpeg", ".png" };

                if (_currentType == ImageType.Grades && !allowedImageExtensions.Contains(extension))
                {
                    await DisplayAlert("Ошибка", "Для оценок можно загружать только изображения (jpg, jpeg, png)", "OK");
                    _selectedFile = null;
                    SelectedFileLabel.Text = "Файл не выбран";
                    return;
                }

                SelectedFileLabel.Text = _selectedFile.FileName;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"File picking error: {ex}");
            await DisplayAlert("Ошибка", "Не удалось выбрать файл", "OK");
        }
    }

    private async void OnUploadScheduleClicked(object sender, EventArgs e)
    {
        if (_selectedFile == null)
        {
            await DisplayAlert("Внимание", "Выберите файл", "OK");
            return;
        }

        try
        {
            LoadingIndicator.IsVisible = true;

            var content = new MultipartFormDataContent();
            var stream = await _selectedFile.OpenReadAsync();
            var streamContent = new StreamContent(stream);
            content.Add(streamContent, "file", _selectedFile.FileName);

            var response = await _httpClient.PostAsync("api/Schedule/upload", content);
            if (response.IsSuccessStatusCode)
            {
                await DisplayAlert("Успех", "Файл расписания загружен", "OK");
                _selectedFile = null;
                SelectedFileLabel.Text = "Файл не выбран";
                await LoadImages();
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                await DisplayAlert("Ошибка", error, "OK");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Upload error: {ex}");
            await DisplayAlert("Ошибка", "Не удалось загрузить файл", "OK");
        }
        finally
        {
            LoadingIndicator.IsVisible = false;
        }
    }

    private async void OnDownloadClicked(object sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is int fileId)
        {
            try
            {
                LoadingIndicator.IsVisible = true;

                // Используем общий эндпоинт для скачивания изображений
                var response = await _httpClient.GetAsync($"api/GradeImages/{fileId}/download");

                if (response.IsSuccessStatusCode)
                {
                    var fileName = response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                        ?? $"{(_currentType == ImageType.Schedule ? "schedule" : "grades")}_{fileId}{GetFileExtension(response.Content.Headers.ContentType?.MediaType)}";
                    var contentType = response.Content.Headers.ContentType?.MediaType;
                    var bytes = await response.Content.ReadAsByteArrayAsync();

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
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"Download error response: {error}");
                    await DisplayAlert("Ошибка", error, "OK");
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
    }

    // Вспомогательный метод для определения расширения файла
    private string GetFileExtension(string contentType)
    {
        return contentType?.ToLower() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "application/pdf" => ".pdf",
            "application/msword" => ".doc",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => ".docx",
            "application/vnd.ms-excel" => ".xls",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => ".xlsx",
            _ => ""
        };
    }

    private async Task DeleteImage(int fileId)
    {
        try
        {
            var confirm = await DisplayAlert("Подтверждение",
                "Вы действительно хотите удалить этот файл?",
                "Да", "Нет");

            if (!confirm) return;

            var response = await _httpClient.DeleteAsync($"api/Schedule/files/{fileId}");
            if (response.IsSuccessStatusCode)
            {
                await DisplayAlert("Успех", "Файл удален", "OK");
                await LoadImages();
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                await DisplayAlert("Ошибка", error, "OK");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Delete error: {ex}");
            await DisplayAlert("Ошибка", "Не удалось удалить файл", "OK");
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

                string extension = Path.GetExtension(fileName);
                if (string.IsNullOrEmpty(extension))
                {
                    extension = ".bin";
                }
                filePicker.FileTypeChoices.Add($"Файл {extension}", new List<string> { extension });

                var window = App.Current.Windows[0].Handler.PlatformView as Microsoft.UI.Xaml.Window;
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                WinRT.Interop.InitializeWithWindow.Initialize(filePicker, hwnd);

                var file = await filePicker.PickSaveFileAsync();
                if (file != null)
                {
                    await File.WriteAllBytesAsync(file.Path, fileData);
                    await DisplayAlert("Успех", "Файл сохранен", "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Windows save error: {ex}");
                throw;
            }
#endif
    }

    private async Task SaveFileAndroid(string fileName, byte[] fileData, string contentType)
    {
#if ANDROID
        try
        {
            using var stream = new MemoryStream(fileData);
            var result = await FileSaver.SaveAsync(fileName, stream, new CancellationToken());

            if (result.IsSuccessful)
            {
                await DisplayAlert("Успех", "Файл сохранен", "OK");
            }
            else
            {
                throw new Exception("Не удалось сохранить файл");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Android save error: {ex}");
            throw;
        }
#endif
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _httpClient = null;
        _isLoading = false;
        _selectedFile = null;
        _images.Clear();
    }

    private async void OnTypeSelected(object sender, EventArgs e)
    {
        try
        {
            _currentType = TypePicker.SelectedIndex == 0 ? ImageType.Schedule : ImageType.Grades;
            Debug.WriteLine($"Selected type: {_currentType}"); // Добавляем логирование типа

            // Показываем/скрываем picker предметов в зависимости от типа
            SubjectPicker.IsVisible = _currentType == ImageType.Grades;

            // Сбрасываем выбранный предмет при переключении на расписание
            if (_currentType == ImageType.Schedule)
            {
                _selectedSubjectId = null;
                SubjectPicker.SelectedItem = null;
            }

            // Обновляем заголовок для выбора файла
            var filePickerTitle = _currentType == ImageType.Schedule ? "Выберите файл расписания" : "Выберите файл оценок";
            SelectedFileLabel.Text = "Файл не выбран";

            await LoadGroups(); // Перезагружаем список групп
            if (_currentType == ImageType.Grades)
            {
                await LoadSubjects(); // Загружаем предметы для оценок
            }

            await LoadImages(); // Обновляем список изображений
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in OnTypeSelected: {ex}");
            await DisplayAlert("Ошибка", "Не удалось обновить данные", "OK");
        }
    }

    private async void OnGroupSelected(object sender, EventArgs e)
    {
        if (GroupPicker.SelectedItem is GroupInfo group)
        {
            _selectedGroupId = group.Id;
            await LoadImages();
        }
    }

    private async void OnSubjectSelected(object sender, EventArgs e)
    {
        if (SubjectPicker.SelectedItem is SubjectInfo subject)
        {
            _selectedSubjectId = subject.Id;
            await LoadImages();
        }
    }

    private async void OnDateSelected(object sender, DateChangedEventArgs e)
    {
        if (sender == StartDatePicker)
            _startDate = e.NewDate;
        else if (sender == EndDatePicker)
            _endDate = e.NewDate;
    }

    private async void OnApplyFiltersClicked(object sender, EventArgs e)
    {
        await LoadImages();
    }

    private async Task LoadGroups()
    {
        try
        {
            // Проверяем и инициализируем httpClient если он null
            if (_httpClient == null)
            {
                _httpClient = await AppConfig.CreateHttpClientAsync();
            }

            var response = await _httpClient.GetAsync("api/StudentGroup/list");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<StudentGroupListResponse>();
                if (result?.Groups != null)
                {
                    GroupPicker.ItemsSource = result.Groups.Select(g => new GroupInfo { Id = g.Id, Name = g.Name }).ToList();
                    GroupPicker.ItemDisplayBinding = new Binding("Name");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading groups: {ex}");
            await DisplayAlert("Ошибка", "Не удалось загрузить список групп", "OK");
        }
    }

    private async Task LoadSubjects()
    {
        try
        {
            var response = await _httpClient.GetAsync("api/Subject/list");
            if (response.IsSuccessStatusCode)
            {
                var subjects = await response.Content.ReadFromJsonAsync<List<SubjectInfo>>();
                SubjectPicker.ItemsSource = subjects;
                SubjectPicker.ItemDisplayBinding = new Binding("Name");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading subjects: {ex}");
        }
    }

    private async void OnUploadClicked(object sender, EventArgs e)
    {
        Debug.WriteLine("OnUploadClicked started");

        if (_selectedFile == null)
        {
            Debug.WriteLine("No file selected");
            await DisplayAlert("Внимание", "Выберите файл", "OK");
            return;
        }

        if (!_selectedGroupId.HasValue)
        {
            Debug.WriteLine("No group selected");
            await DisplayAlert("Внимание", "Выберите группу", "OK");
            return;
        }

        try
        {
            Debug.WriteLine($"Starting file upload process - File: {_selectedFile.FileName}, Size: {_selectedFile.FullPath}, Type: {_currentType}");
            LoadingIndicator.IsVisible = true;

            var content = new MultipartFormDataContent();

            // Логируем информацию о файле
            Debug.WriteLine($"Opening file stream for {_selectedFile.FileName}");
            var stream = await _selectedFile.OpenReadAsync();
            Debug.WriteLine($"Stream opened successfully, length: {stream.Length} bytes");

            var fileContent = new StreamContent(stream);
            Debug.WriteLine("Adding file to MultipartFormDataContent");
            content.Add(fileContent, "file", _selectedFile.FileName);

            // Логируем параметры запроса
            Debug.WriteLine($"Current type: {_currentType}");
            Debug.WriteLine($"Selected group ID: {_selectedGroupId}");

            if (_currentType == ImageType.Grades)
            {
                Debug.WriteLine($"Adding grades parameters - SubjectId: {_selectedSubjectId}");
                content.Add(new StringContent("Grades"), "type");
                content.Add(new StringContent(_selectedSubjectId.ToString()), "subjectId");
            }

            content.Add(new StringContent(_selectedGroupId.ToString()), "studentGroupId");

            // Логируем URL
            var uploadUrl = _currentType == ImageType.Schedule
                ? "api/GradeImages/schedule/upload"
                : "api/GradeImages/upload";
            Debug.WriteLine($"Upload URL: {uploadUrl}");

            // Логируем отправку запроса
            Debug.WriteLine("Sending POST request");
            var response = await _httpClient.PostAsync(uploadUrl, content);
            Debug.WriteLine($"Response status code: {response.StatusCode}");

            var responseContent = await response.Content.ReadAsStringAsync();
            Debug.WriteLine($"Response content: {responseContent}");

            if (response.IsSuccessStatusCode)
            {
                Debug.WriteLine("Upload successful, parsing response");
                var result = await response.Content.ReadFromJsonAsync<ImageUploadResponse>();
                Debug.WriteLine($"Upload completed - Image ID: {result?.Id}, File name: {result?.FileName}");

                await DisplayAlert("Успех", "Файл успешно загружен", "OK");

                _selectedFile = null;
                SelectedFileLabel.Text = "Файл не выбран";

                await LoadImages();
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                Debug.WriteLine("Upload forbidden - access denied");
                await DisplayAlert("Ошибка", "Извините, нельзя загрузить файл.", "OK");
            }
            else
            {
                Debug.WriteLine($"Upload failed with status code: {response.StatusCode}");
                await DisplayAlert("Ошибка", responseContent, "OK");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Upload error: {ex.GetType().Name}");
            Debug.WriteLine($"Error message: {ex.Message}");
            Debug.WriteLine($"Stack trace: {ex.StackTrace}");

            if (ex.InnerException != null)
            {
                Debug.WriteLine($"Inner exception: {ex.InnerException.Message}");
                Debug.WriteLine($"Inner exception stack trace: {ex.InnerException.StackTrace}");
            }

            await DisplayAlert("Ошибка", "Не удалось загрузить файл", "OK");
        }
        finally
        {
            Debug.WriteLine("Upload process completed");
            LoadingIndicator.IsVisible = false;
        }
    }
    public enum ImageType
    {
        Schedule,
        Grades
    }

    public class ImageUploadResponse
    {
        public int Id { get; set; }
        public string FileName { get; set; }
        public string FileType { get; set; }
        public ImageType Type { get; set; }
        public DateTime UploadedAt { get; set; }
        public GroupInfo Group { get; set; }
    }

    public class ImagesListResponse
    {
        public int TotalCount { get; set; }
        public FilterInfo FilterInfo { get; set; }
        public DateRange DateRange { get; set; }
        public List<ImageDetails> Schedules { get; set; }
        public List<ImageDetails> Images { get; set; } // ← добавьте это
    }

    public class FilterInfo
    {
        public int? GroupId { get; set; }
        public int? SubjectId { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }

    public class DateRange
    {
        public DateTime? Earliest { get; set; }
        public DateTime? Latest { get; set; }
    }

    public class ImageDetails
    {
        public int Id { get; set; }
        public string FileName { get; set; }
        public string FileType { get; set; }
        public DateTime UploadedAt { get; set; }
        public GroupInfo Group { get; set; }
        public UploaderInfo Uploader { get; set; }
        public DateTime UploadDate { get; set; }
        public string UploadTime { get; set; }
    }

    public class GroupInfo
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class UploaderInfo
    {
        public int Id { get; set; }
        public string FullName { get; set; }
    }

    public class SubjectInfo
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class GradesImagesResponse
    {
        public int TotalCount { get; set; }
        public FilterInfo FilterInfo { get; set; }
        public List<ImageResponseItem> Images { get; set; }
    }

    public class ImageResponseItem
    {
        public int Id { get; set; }
        public string FileName { get; set; }
        public string FileType { get; set; }
        public DateTime UploadedAt { get; set; }
        public int Type { get; set; }
        public SubjectInfo Subject { get; set; }
        public GroupInfo Group { get; set; }
        public UploaderInfo Uploader { get; set; }
    }

    public class ScheduleImagesResponse
    {
        public int TotalCount { get; set; }
        public FilterInfo FilterInfo { get; set; }
        public DateRange DateRange { get; set; }
        public List<ImageDetails> Schedules { get; set; }
    }
}