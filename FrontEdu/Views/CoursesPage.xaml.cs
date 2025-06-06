using FrontEdu.Models.Auth;
using FrontEdu.Models.Chat;
using FrontEdu.Models.Course;
using FrontEdu.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.Http.Json;

namespace FrontEdu.Views;

// Views/CoursesPage.xaml.cs

[QueryProperty(nameof(SubjectId), "subjectId")]
[QueryProperty(nameof(SubjectName), "subjectName")]
[QueryProperty(nameof(SubjectCode), "subjectCode")]
[QueryProperty(nameof(SubjectCourses), "courses")]
public partial class CoursesPage : ContentPage
{
    private HttpClient _httpClient;
    private ObservableCollection<CourseResponse> _courses;
    private ObservableCollection<CourseResponse> _allCourses;
    private string _searchQuery = string.Empty;
    private bool _isLoading;
    private bool _canManageCourses;

    public int SubjectId { get; set; }
    public string SubjectName { get; set; }
    public string SubjectCode { get; set; }
    public List<SubjectCourseInfo> SubjectCourses { get; set; }
    private bool _isNavigating;
    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        if (!_isNavigating)
        {
            _isNavigating = true;
            await InitializePage();
            _isNavigating = false;
        }
    }
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

    private List<CourseTeacher> ConvertTeachers(List<SubjectTeacherInfo> teachers)
    {
        return teachers?.Select(t => new CourseTeacher
        {
            Id = t.Id,
            FullName = t.FullName,
            Email = t.Email,
            JoinedAt = DateTime.UtcNow  // ��� ��� � SubjectTeacherInfo ��� JoinedAt, ���������� ������� �����
        }).ToList() ?? new List<CourseTeacher>();
    }

    private async Task InitializePage()
    {
        try
        {
            LoadingIndicator.IsVisible = true;

            _httpClient = await AppConfig.CreateHttpClientAsync();

            // ��������� ����� �� ���������� �������
            var permissionsResponse = await _httpClient.GetAsync("api/Profile/me/permissions");
            if (permissionsResponse.IsSuccessStatusCode)
            {
                var permissions = await permissionsResponse.Content.ReadFromJsonAsync<UserPermissionsResponse>();
                _canManageCourses = permissions?.Permissions.ManageCourses ?? false;
                AddCourseButton.IsVisible = _canManageCourses;
            }

            // ���� ���� ���������� �����, ���������� ��
            if (SubjectCourses != null)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    Title = $"����� �������� {SubjectName}";
                    _allCourses.Clear();
                    foreach (var course in SubjectCourses)
                    {
                        _allCourses.Add(new CourseResponse
                        {
                            Id = course.Id,
                            Name = course.Name,
                            Description = course.Description,
                            CreatedAt = course.CreatedAt,
                            Teachers = ConvertTeachers(course.Teachers), // ���������� ����� �����������
                            StudentsCount = course.StudentsCount
                        });
                    }
                    ApplyFilter();
                });
            }
            else
            {
                await LoadCourses();
            }
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

            // ���������� SubjectId ��� ��������� ������ ����������� ��������
            var response = await _httpClient.GetAsync($"api/Subject/{SubjectId}/courses");
            if (response.IsSuccessStatusCode)
            {
                var subjectCourses = await response.Content.ReadFromJsonAsync<SubjectCoursesResponse>();
                if (subjectCourses != null)
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        // ��������� ��������� ��������
                        Title = $"����� �������� {subjectCourses.Name}";

                        _allCourses.Clear();
                        foreach (var course in subjectCourses.Courses ?? Enumerable.Empty<SubjectCourseInfo>())
                        {
                            _allCourses.Add(new CourseResponse
                            {
                                Id = course.Id,
                                Name = course.Name,
                                Description = course.Description,
                                CreatedAt = course.CreatedAt,
                                Teachers = ConvertTeachers(course.Teachers),
                                StudentsCount = course.StudentsCount
                            });
                        }
                        ApplyFilter();
                    });
                }
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