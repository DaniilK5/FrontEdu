using FrontEdu.Models.Assignments;
using FrontEdu.Models.Auth;
using FrontEdu.Models.Course;
using FrontEdu.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;

namespace FrontEdu.Views;

[QueryProperty(nameof(CourseId), "courseId")]
public partial class AssignmentsPage : ContentPage
{
    private bool _canCreateAssignments;
    private HttpClient _httpClient;
    private ObservableCollection<AssignmentResponse> _assignments;
    private bool _isLoading;
    private int _courseId;

    public int CourseId
    {
        get => _courseId;
        set
        {
            if (_courseId != value)
            {
                _courseId = value;
                // �� ��������� ������ �����, ��� ����� �������� � OnAppearing
            }
        }
    }
    public AssignmentsPage()
    {
        try
        {
            InitializeComponent();
            _assignments = new ObservableCollection<AssignmentResponse>();
            AssignmentsCollection.ItemsSource = _assignments;
            AssignmentsRefreshView.Command = new Command(async () => await LoadAssignments());
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"AssignmentsPage constructor error: {ex}");
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            LoadingIndicator.IsVisible = true;

            // ����������� HttpClient ��� ������ �������������
            _httpClient = await AppConfig.CreateHttpClientAsync();

            // ��������� ����� ������������
            var permissionsResponse = await _httpClient.GetAsync("api/Profile/me/permissions");
            if (permissionsResponse.IsSuccessStatusCode)
            {
                var permissions = await permissionsResponse.Content.ReadFromJsonAsync<UserPermissionsResponse>();
                _canCreateAssignments = permissions?.Categories.Assignments.CanManage ?? false;

                // ���������, �������� �� ������������ �������������� �����
                if (_canCreateAssignments)
                {
                    var courseResponse = await _httpClient.GetAsync($"api/Course/{CourseId}");
                    if (courseResponse.IsSuccessStatusCode)
                    {
                        var course = await courseResponse.Content.ReadFromJsonAsync<CourseResponse>();
                        var token = await SecureStorage.GetAsync("auth_token");
                        if (!string.IsNullOrEmpty(token))
                        {
                            var handler = new JwtSecurityTokenHandler();
                            var jsonToken = handler.ReadToken(token) as JwtSecurityToken;
                            var userId = int.Parse(jsonToken?.Claims.FirstOrDefault(c =>
                                c.Type.Contains("nameidentifier"))?.Value ?? "0");

                            _canCreateAssignments = course?.Teachers.Any(t => t.Id == userId) ?? false;
                        }
                    }
                }

                AddAssignmentButton.IsVisible = _canCreateAssignments;
            }

            // ��������� �������
            await LoadAssignments();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Initialize error: {ex}");
            await DisplayAlert("������", "�� ������� ���������������� ��������", "OK");
        }
        finally
        {
            LoadingIndicator.IsVisible = false;
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        ResetState();
    }

    private void ResetState()
    {
        _assignments?.Clear();
        _httpClient = null;
        _isLoading = false;
    }
    private async Task LoadAssignments()
    {
        if (_isLoading || _httpClient == null) return;

        try
        {
            _isLoading = true;
            LoadingIndicator.IsVisible = true;

            // ��������� � ����������� HttpClient ���� �����
            if (_httpClient == null)
            {
                _httpClient = await AppConfig.CreateHttpClientAsync();
            }

            var response = await _httpClient.GetAsync($"api/Course/{CourseId}/assignments");
            if (response.IsSuccessStatusCode)
            {
                var assignments = await response.Content.ReadFromJsonAsync<List<AssignmentResponse>>();

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    _assignments.Clear();
                    if (assignments != null)
                    {
                        foreach (var assignment in assignments)
                        {
                            _assignments.Add(assignment);
                        }
                    }
                });
            }
            else
            {
                await DisplayAlert("������", "�� ������� ��������� �������", "OK");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading assignments: {ex}");
            await DisplayAlert("������", "�� ������� ��������� �������", "OK");
        }
        finally
        {
            _isLoading = false;
            LoadingIndicator.IsVisible = false;
            AssignmentsRefreshView.IsRefreshing = false;
        }
    }
    private async void OnAssignmentSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is AssignmentResponse selectedAssignment)
        {
            try
            {
                AssignmentsCollection.SelectedItem = null;
                var navigationParameter = new Dictionary<string, object>
            {
                { "assignmentId", selectedAssignment.Id }
            };

                Debug.WriteLine($"Navigating to assignment details with ID: {selectedAssignment.Id}");
                await Shell.Current.GoToAsync($"//AssignmentDetailsPage", navigationParameter);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Navigation error: {ex}");
                try
                {
                    // �������������� ������� ���������
                    await Shell.Current.GoToAsync($"AssignmentDetailsPage?assignmentId={selectedAssignment.Id}");
                }
                catch (Exception altEx)
                {
                    Debug.WriteLine($"Alternative navigation error: {altEx}");
                    await DisplayAlert("������", "�� ������� ������� ������ �������", "OK");
                }
            }
        }
    }
    private async void OnAddAssignmentClicked(object sender, EventArgs e)
    {
        if (!_canCreateAssignments) return;

        try
        {
            LoadingIndicator.IsVisible = true;

            // �������� ������ ��� �������
            string title = await DisplayPromptAsync(
                "����� �������",
                "�������� �������",
                maxLength: 100);

            if (string.IsNullOrWhiteSpace(title)) return;

            string description = await DisplayPromptAsync(
                "����� �������",
                "�������� �������",
                maxLength: 500);

            if (string.IsNullOrWhiteSpace(description)) return;

            // ���������� ��� �������
            var deadline = DateTime.Now.AddDays(7);
            bool hasDeadline = await DisplayAlert("�������", "������ ���������� ���� �����?", "��", "���");
            if (hasDeadline)
            {
                // � �������� ���������� ����� ����� ������������ DatePicker
                // ��� ������� ������������� ������� ����� 7 ����
                deadline = DateTime.Now.AddDays(7);
            }

            // ���������� ��� ����
            bool attachFile = await DisplayAlert("����", "������ ���������� ���� � �������?", "��", "���");

            // ������� ����� ��� ��������
            var content = new MultipartFormDataContent();
            content.Add(new StringContent(title), "Title");
            content.Add(new StringContent(description), "Description");
            content.Add(new StringContent(deadline.ToString("o")), "Deadline");
            content.Add(new StringContent(CourseId.ToString()), "CourseId");

            if (attachFile)
            {
                // �������� ����
                var customFileType = new FilePickerFileType(
                    new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                    { DevicePlatform.Android, new[] { "image/*", "application/*" } },
                    { DevicePlatform.WinUI, new[] { ".jpg", ".jpeg", ".png", ".doc", ".docx", ".pdf" } }
                    });

                var options = new PickOptions
                {
                    PickerTitle = "�������� ���� �������",
                    FileTypes = customFileType,
                };

                var file = await FilePicker.PickAsync(options);
                if (file != null)
                {
                    var stream = await file.OpenReadAsync();
                    var fileContent = new StreamContent(stream);
                    content.Add(fileContent, "Attachment", file.FileName);
                }
                else
                {
                    // ���� ������������ ������� ����� �����, ����������, ����� �� �� ���������� ��� �����
                    bool continueWithoutFile = await DisplayAlert(
                        "���� �� ������",
                        "������ ������� ������� ��� �����?",
                        "��", "���");

                    if (!continueWithoutFile) return;
                }
            }

            // ���������� ������
            var response = await _httpClient.PostAsync("api/Assignment/create", content);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<CreateAssignmentResponse>();
                if (result != null)
                {
                    await DisplayAlert("�����", "������� ������� �������", "OK");
                    await LoadAssignments();
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
            Debug.WriteLine($"Error creating assignment: {ex}");
            await DisplayAlert("������", "�� ������� ������� �������", "OK");
        }
        finally
        {
            LoadingIndicator.IsVisible = false;
        }
    }
}