using FrontEdu.Models.Auth;
using FrontEdu.Models.Chat;
using FrontEdu.Models.Course;
using FrontEdu.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.Http.Json;

namespace FrontEdu.Views;

// Views/CoursesPage.xaml.cs
public partial class CoursesPage : ContentPage
{
    private HttpClient _httpClient;
    private ObservableCollection<CourseResponse> _courses;
    private ObservableCollection<CourseResponse> _allCourses;
    private string _searchQuery = string.Empty;
    private bool _isLoading;
    private bool _canManageCourses;

    public CoursesPage()
    {
        InitializeComponent();
        _courses = new ObservableCollection<CourseResponse>();
        _allCourses = new ObservableCollection<CourseResponse>();
        CoursesCollection.ItemsSource = _courses;
        CoursesRefreshView.Command = new Command(async () => await RefreshCourses());
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
            LoadingIndicator.IsVisible = true;

            // ����������� HttpClient ��� ������ �������������
            _httpClient = await AppConfig.CreateHttpClientAsync();

            // ��������� ����� �� ���������� �������
            var permissionsResponse = await _httpClient.GetAsync("api/Profile/me/permissions");
            if (permissionsResponse.IsSuccessStatusCode)
            {
                var permissions = await permissionsResponse.Content.ReadFromJsonAsync<UserPermissionsResponse>();
                _canManageCourses = permissions?.Permissions.ManageCourses ?? false;
                AddCourseButton.IsVisible = _canManageCourses;
            }

            await LoadCourses();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Initialize error: {ex}");
            await DisplayAlert("������", "�� ������� ��������� �����", "OK");
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
        _courses?.Clear();
        _allCourses?.Clear();
        _httpClient = null;
        _isLoading = false;
    }

    private async Task LoadCourses()
    {
        if (_isLoading || _httpClient == null) return;

        try
        {
            _isLoading = true;
            LoadingIndicator.IsVisible = true;

            var response = await _httpClient.GetAsync("api/Course");
            if (response.IsSuccessStatusCode)
            {
                var courses = await response.Content.ReadFromJsonAsync<List<CourseResponse>>();

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    _allCourses.Clear();
                    foreach (var course in courses ?? Enumerable.Empty<CourseResponse>())
                    {
                        _allCourses.Add(course);
                    }
                    ApplyFilter();
                });
            }
            else
            {
                await DisplayAlert("������", "�� ������� ��������� �����", "OK");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading courses: {ex}");
            await DisplayAlert("������", "�� ������� ��������� �����", "OK");
        }
        finally
        {
            _isLoading = false;
            LoadingIndicator.IsVisible = false;
            CoursesRefreshView.IsRefreshing = false;
        }
    }
    private void ApplyFilter()
    {
        _courses.Clear();
        var filteredCourses = string.IsNullOrWhiteSpace(_searchQuery)
            ? _allCourses
            : _allCourses.Where(c =>
                c.Name.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase) ||
                c.Description.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase));

        foreach (var course in filteredCourses)
        {
            _courses.Add(course);
        }
    }

    private async void OnAddCourseClicked(object sender, EventArgs e)
    {
        if (!_canManageCourses) return;

        try
        {
            LoadingIndicator.IsVisible = true;

            // �������� �������� �����
            string name = await DisplayPromptAsync(
                "����� ����",
                "�������� �����",
                maxLength: 100);

            if (string.IsNullOrWhiteSpace(name)) return;

            // �������� �������� �����
            string description = await DisplayPromptAsync(
                "����� ����",
                "�������� �����",
                maxLength: 500);

            if (string.IsNullOrWhiteSpace(description)) return;

            // �������� ������ ���� �������������
            var usersResponse = await _httpClient.GetAsync("api/Message/users");
            if (!usersResponse.IsSuccessStatusCode)
            {
                await DisplayAlert("������", "�� ������� ��������� ������ �������������", "OK");
                return;
            }

            var users = await usersResponse.Content.ReadFromJsonAsync<List<ChatUserDto>>();
            if (users == null)
            {
                await DisplayAlert("������", "�� ������� �������� ������ �������������", "OK");
                return;
            }

            // �������� ������ ��������������
            var teachers = users.Where(u => u.Role == "Teacher").ToList();
            if (!teachers.Any())
            {
                await DisplayAlert("������", "��� ��������� ��������������", "OK");
                return;
            }

            // �������� ��������������
            var teacherNames = teachers.Select(t => t.FullName).ToArray();
            var selectedTeacher = await DisplayActionSheet(
                "�������� �������������",
                "������",
                null,
                teacherNames);

            if (selectedTeacher == "������" || string.IsNullOrEmpty(selectedTeacher)) return;

            var teacherId = teachers.First(t => t.FullName == selectedTeacher).Id;

            // �������� ������ ���������
            var students = users.Where(u => u.Role == "Student").ToList();
            if (!students.Any())
            {
                await DisplayAlert("������", "��� ��������� ���������", "OK");
                return;
            }

            // �������� ��������
            var studentNames = students.Select(s => s.FullName).ToArray();
            var selectedStudent = await DisplayActionSheet(
                "�������� ��������",
                "������",
                null,
                studentNames);

            if (selectedStudent == "������" || string.IsNullOrEmpty(selectedStudent)) return;

            var studentId = students.First(s => s.FullName == selectedStudent).Id;

            // ������� ������ �� �������� �����
            var createCourseRequest = new CreateCourseRequest
            {
                Name = name,
                Description = description,
                SubjectId = 1, // ����� �������� ����� �������� �����
                TeacherIds = new List<int> { teacherId },
                StudentIds = new List<int> { studentId }
            };

            // ���������� ������
            var response = await _httpClient.PostAsJsonAsync("api/Course", createCourseRequest);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<CreateCourseResponse>();
                if (result != null)
                {
                    await DisplayAlert("�����", $"���� ������� ������ (ID: {result.CourseId})", "OK");
                    await LoadCourses(); // ������������� ������ ������
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
            Debug.WriteLine($"Error creating course: {ex}");
            await DisplayAlert("������", "�� ������� ������� ����", "OK");
        }
        finally
        {
            LoadingIndicator.IsVisible = false;
        }
    }
    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        _searchQuery = e.NewTextValue?.Trim().ToLower() ?? string.Empty;
        ApplyFilter();
    }

    private async void OnCourseSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is CourseResponse selectedCourse)
        {
            try
            {
                CoursesCollection.SelectedItem = null;
                var navigationParameter = new Dictionary<string, object>
                {
                    { "courseId", selectedCourse.Id }
                };

                // ���������� ��������� �������� ���������
                try
                {
                    await Shell.Current.GoToAsync($"AssignmentsPage", navigationParameter);
                }
                catch (Exception navEx)
                {
                    Debug.WriteLine($"First navigation attempt failed: {navEx}");
                    try
                    {
                        await Shell.Current.GoToAsync($"///AssignmentsPage", navigationParameter);
                    }
                    catch (Exception altNavEx)
                    {
                        Debug.WriteLine($"Alternative navigation error: {altNavEx}");
                        await DisplayAlert("������", "�� ������� ������� ������� �����", "OK");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Course selection error: {ex}");
                await DisplayAlert("������", "�� ������� ������� ������� �����", "OK");
            }
        }
    }
    private async Task RefreshCourses()
    {
        await LoadCourses();
    }
}