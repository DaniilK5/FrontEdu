using FrontEdu.Models.Assignments;
using FrontEdu.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;

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
    private bool _isStudent;
    private bool _isLoading;
    private FileResult _selectedFile;
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
                // �������� ����� ����������� � OnAppearing
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

            // ��������� ���������� ���������� ������ "�����"
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
        Debug.WriteLine("Back button pressed in AssignmentsPage");

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                Debug.WriteLine("Attempting to navigate to MainPage");
                await Shell.Current.GoToAsync("///MainPage");
                Debug.WriteLine("Successfully navigated to MainPage");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error navigating to MainPage: {ex.Message}");
            }
        });

        return true; // ������������� ����������� ��������� ������ �����
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await InitializeAsync();
        if (_httpClient != null) // ��������� ��������
        {
            await LoadAssignmentDetails();
        }
    }


    private async Task InitializeAsync()
    {
        try
        {
            // ����������� HttpClient ��� ������ �������������
            _httpClient = await AppConfig.CreateHttpClientAsync();

            // ��������� ���� ������������
            var token = await SecureStorage.GetAsync("auth_token");
            if (!string.IsNullOrEmpty(token))
            {
                var handler = new JwtSecurityTokenHandler();
                var jsonToken = handler.ReadToken(token) as JwtSecurityToken;
                var role = jsonToken?.Claims.FirstOrDefault(c => c.Type.Contains("role"))?.Value;
                _isTeacher = role == "Teacher";
                _isStudent = role == "Student";

                if (_isTeacher)
                {
                    await LoadSubmissions();
                }
                
                // ���������� ������ �������� ������� ������ ���������
                SubmitPanel.IsVisible = _isStudent;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Initialize error: {ex}");
            await DisplayAlert("������", "�� ������� ���������������� ��������", "OK");
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
        _selectedFile = null;
        if (SelectedFileLabel != null)
        {
            SelectedFileLabel.Text = "���� �� ������";
        }
        if (ContentEntry != null)
        {
            ContentEntry.Text = string.Empty;
        }
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
                        Title = $"�������: {assignment.Title}";
                    });
                }
            }
            else
            {
                await DisplayAlert("������", "�� ������� ��������� ������ �������", "OK");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading assignment details: {ex}");
            await DisplayAlert("������", "�� ������� ��������� ������ �������", "OK");
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

                // ���������� ������ ���������� ����� �� DirectChatPage
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
            await DisplayAlert("������", "�� ������� ������� ����", "OK");
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
            Debug.WriteLine($"Starting download of submission file. SubmissionId: {submissionId}");
            try
            {
                LoadingIndicator.IsVisible = true;
                Debug.WriteLine($"Making HTTP request to get submission file...");

                var response = await _httpClient.GetAsync($"api/Assignment/submissions/{submissionId}/file");
                Debug.WriteLine($"Server response status code: {response.StatusCode}");

                // �������� ��� ��������� ������
                foreach (var header in response.Headers)
                {
                    Debug.WriteLine($"Header: {header.Key} = {string.Join(", ", header.Value)}");
                }
                foreach (var header in response.Content.Headers)
                {
                    Debug.WriteLine($"Content Header: {header.Key} = {string.Join(", ", header.Value)}");
                }

                if (response.IsSuccessStatusCode)
                {
                    Debug.WriteLine("File download successful, processing response...");

                    // ��������� ������� � ������������ ����������
                    var contentDisposition = response.Content.Headers.ContentDisposition;
                    var contentType = response.Content.Headers.ContentType;

                    Debug.WriteLine($"Content-Disposition: {contentDisposition}");
                    Debug.WriteLine($"Content-Type: {contentType}");

                    var fileName = contentDisposition?.FileName?.Trim('"')
                        ?? $"submission_{submissionId}{GetDefaultExtension(contentType?.MediaType)}";
                    Debug.WriteLine($"Detected filename: {fileName}");

                    var mediaType = contentType?.MediaType ?? "application/octet-stream";
                    Debug.WriteLine($"Content type: {mediaType}");

                    Debug.WriteLine("Reading file bytes...");
                    var bytes = await response.Content.ReadAsByteArrayAsync();
                    Debug.WriteLine($"Read {bytes.Length} bytes from response");

                    if (bytes.Length == 0)
                    {
                        Debug.WriteLine("Warning: Received empty file");
                        await DisplayAlert("������", "���� ���� ��� ���������", "OK");
                        return;
                    }

                    Debug.WriteLine($"Current platform: {DeviceInfo.Platform}");

                    try
                    {
                        if (DeviceInfo.Platform == DevicePlatform.WinUI)
                        {
                            Debug.WriteLine("Using Windows file save logic");
                            await SaveFileWindows(fileName, bytes);
                        }
                        else if (DeviceInfo.Platform == DevicePlatform.Android)
                        {
                            Debug.WriteLine("Using Android file save logic");
                            await SaveFileAndroid(fileName, bytes, mediaType);
                        }
                        else
                        {
                            Debug.WriteLine("Using default file save logic");
                            var tempPath = Path.Combine(FileSystem.CacheDirectory, fileName);
                            Debug.WriteLine($"Saving file to temp path: {tempPath}");

                            await File.WriteAllBytesAsync(tempPath, bytes);
                            Debug.WriteLine("File saved to temp location, opening...");

                            await Launcher.OpenAsync(new OpenFileRequest
                            {
                                File = new ReadOnlyFile(tempPath)
                            });
                            Debug.WriteLine("File opened successfully");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Platform-specific save error: {ex.Message}");
                        Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                        throw; // �������� ���������� ���� ��� ����� ���������
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"Server error response: {errorContent}");
                    await DisplayAlert("������",
                        $"�� ������� ������� ���� �������. ���: {response.StatusCode}", "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Download submission error type: {ex.GetType().Name}");
                Debug.WriteLine($"Error message: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");

                if (ex.InnerException != null)
                {
                    Debug.WriteLine($"Inner exception: {ex.InnerException.Message}");
                    Debug.WriteLine($"Inner exception stack trace: {ex.InnerException.StackTrace}");
                }

                await DisplayAlert("������", "�� ������� ������� ���� �������", "OK");
            }
            finally
            {
                Debug.WriteLine("Download operation completed");
                LoadingIndicator.IsVisible = false;
            }
        }
        else
        {
            Debug.WriteLine("Invalid sender or submission ID");
        }
    }

    // ��������������� ����� ��� ����������� ���������� �����
    private string GetDefaultExtension(string contentType)
    {
        return contentType switch
        {
            "application/pdf" => ".pdf",
            "application/msword" => ".doc",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => ".docx",
            "application/vnd.ms-excel" => ".xls",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => ".xlsx",
            "text/plain" => ".txt",
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            _ => ""
        };
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

    private async void OnGradeSubmissionClicked(object sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is int submissionId)
        {
            try
            {
                // ����������� ������
                string gradeStr = await DisplayPromptAsync(
                    "������ �������",
                    "������� ������ (0-100)",
                    maxLength: 3,
                    keyboard: Keyboard.Numeric);

                if (string.IsNullOrEmpty(gradeStr)) return;

                if (!int.TryParse(gradeStr, out int grade) || grade < 0 || grade > 100)
                {
                    await DisplayAlert("������", "������ ������ ���� ������ �� 0 �� 100", "OK");
                    return;
                }

                // ����������� �����������
                string comment = await DisplayPromptAsync(
                    "������ �������",
                    "�������� ����������� (�������������)",
                    maxLength: 500);

                // ������� ������
                var gradeRequest = new GradeSubmissionRequest
                {
                    Value = grade,
                    Comment = comment ?? string.Empty
                };

                // ���������� ������
                var response = await _httpClient.PostAsJsonAsync(
                    $"api/Assignment/submissions/{submissionId}/grade",
                    gradeRequest);

                if (response.IsSuccessStatusCode)
                {
                    await DisplayAlert("�����", "������� �������", "OK");
                    await LoadSubmissions(); // ������������� ������ �������
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    await DisplayAlert("������", error, "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Grading error: {ex}");
                await DisplayAlert("������", "�� ������� ������� �������", "OK");
            }
        }
    }

    private async void OnSelectFileClicked(object sender, EventArgs e)
    {
        try
        {
            LoadingIndicator.IsVisible = true;

            if (DeviceInfo.Platform == DevicePlatform.WinUI)
            {
#if WINDOWS
                var filePicker = new FileOpenPicker();
                filePicker.FileTypeFilter.Add("*"); // ��������� ��� ���� ������

                var window = App.Current.Windows[0].Handler.PlatformView as Microsoft.UI.Xaml.Window;
                var hwnd = WindowNative.GetWindowHandle(window);
                InitializeWithWindow.Initialize(filePicker, hwnd);

                var file = await filePicker.PickSingleFileAsync();
                if (file != null)
                {
                    var storageFile = await StorageFile.GetFileFromPathAsync(file.Path);
                    var stream = await storageFile.OpenAsync(FileAccessMode.Read);
                    _selectedFile = new FileResult(file.Path)
                    {
                        FileName = file.Name,
                        ContentType = file.ContentType
                    };
                    SelectedFileLabel.Text = file.Name;
                }
#endif
            }
            else
            {
                // ��� Android � ������ �������� ��������� ������������ ������
                var customFileType = new FilePickerFileType(
                    new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.Android, new[] { "*/*" } },
                        { DevicePlatform.iOS, new[] { "*/*" } },
                        { DevicePlatform.MacCatalyst, new[] { "*/*" } }
                    });

                var options = new PickOptions
                {
                    PickerTitle = "�������� ���� �������",
                    FileTypes = customFileType
                };

                var result = await FilePicker.Default.PickAsync(options);
                if (result != null)
                {
                    _selectedFile = result;
                    SelectedFileLabel.Text = result.FileName;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"File picking error: {ex}");
            await DisplayAlert("������", "�� ������� ������� ����", "OK");
        }
        finally
        {
            LoadingIndicator.IsVisible = false;
        }
    }

    private async void OnSubmitSolutionClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ContentEntry.Text) && _selectedFile == null)
        {
            await DisplayAlert("������", "�������� ����������� ��� ���������� ����", "OK");
            return;
        }

        try
        {
            LoadingIndicator.IsVisible = true;

            var content = new MultipartFormDataContent();

            // ��������� �����������, ���� �� ����
            if (!string.IsNullOrWhiteSpace(ContentEntry.Text))
            {
                content.Add(new StringContent(ContentEntry.Text), "Content");
            }

            // ��������� ����, ���� �� ������
            if (_selectedFile != null)
            {
                byte[] fileBytes;
                if (DeviceInfo.Platform == DevicePlatform.WinUI)
                {
#if WINDOWS
                    var storageFile = await StorageFile.GetFileFromPathAsync(_selectedFile.FullPath);
                    using var stream = await storageFile.OpenStreamForReadAsync();
                    fileBytes = new byte[stream.Length];
                    await stream.ReadAsync(fileBytes, 0, (int)stream.Length);
#else
                    using var stream = await _selectedFile.OpenReadAsync();
                    using var memoryStream = new MemoryStream();
                    await stream.CopyToAsync(memoryStream);
                    fileBytes = memoryStream.ToArray();
#endif
                }
                else
                {
                    using var stream = await _selectedFile.OpenReadAsync();
                    using var memoryStream = new MemoryStream();
                    await stream.CopyToAsync(memoryStream);
                    fileBytes = memoryStream.ToArray();
                }

                var fileContent = new ByteArrayContent(fileBytes);
                content.Add(fileContent, "Attachment", _selectedFile.FileName);
            }

            // ���������� �������
            var response = await _httpClient.PostAsync($"api/Assignment/{AssignmentId}/submit", content);
            
            if (response.IsSuccessStatusCode)
            {
                await DisplayAlert("�����", "������� ����������", "OK");
                
                // ������� �����
                ContentEntry.Text = string.Empty;
                _selectedFile = null;
                SelectedFileLabel.Text = "���� �� ������";
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                await DisplayAlert("������", error, "OK");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Submit solution error: {ex}");
            await DisplayAlert("������", "�� ������� ��������� �������", "OK");
        }
        finally
        {
            LoadingIndicator.IsVisible = false;
        }
    }
}