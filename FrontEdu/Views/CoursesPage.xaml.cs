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
            JoinedAt = DateTime.UtcNow  // Так как в SubjectTeacherInfo нет JoinedAt, используем текущее время
        }).ToList() ?? new List<CourseTeacher>();
    }

    private async Task InitializePage()
    {
        try
        {
            LoadingIndicator.IsVisible = true;

            _httpClient = await AppConfig.CreateHttpClientAsync();

            // Проверяем права на управление курсами
            var permissionsResponse = await _httpClient.GetAsync("api/Profile/me/permissions");
            if (permissionsResponse.IsSuccessStatusCode)
            {
                var permissions = await permissionsResponse.Content.ReadFromJsonAsync<UserPermissionsResponse>();
                _canManageCourses = permissions?.Permissions.ManageCourses ?? false;
                AddCourseButton.IsVisible = _canManageCourses;
            }

            // Если есть переданные курсы, отображаем их
            if (SubjectCourses != null)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    Title = $"Курсы предмета {SubjectName}";
                    _allCourses.Clear();
                    foreach (var course in SubjectCourses)
                    {
                        _allCourses.Add(new CourseResponse
                        {
                            Id = course.Id,
                            Name = course.Name,
                            Description = course.Description,
                            CreatedAt = course.CreatedAt,
                            Teachers = ConvertTeachers(course.Teachers), // Используем метод конвертации
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
            await DisplayAlert("Ошибка", "Не удалось загрузить курсы", "OK");
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

            // Используем SubjectId для получения курсов конкретного предмета
            var response = await _httpClient.GetAsync($"api/Subject/{SubjectId}/courses");
            if (response.IsSuccessStatusCode)
            {
                var subjectCourses = await response.Content.ReadFromJsonAsync<SubjectCoursesResponse>();
                if (subjectCourses != null)
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        // Обновляем заголовок страницы
                        Title = $"Курсы предмета {subjectCourses.Name}";

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
                await DisplayAlert("Ошибка", "Не удалось загрузить курсы", "OK");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading courses: {ex}");
            await DisplayAlert("Ошибка", "Не удалось загрузить курсы", "OK");
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

            // Получаем название курса
            string name = await DisplayPromptAsync(
                "Новый курс",
                "Название курса",
                maxLength: 100);

            if (string.IsNullOrWhiteSpace(name)) return;

            // Получаем описание курса
            string description = await DisplayPromptAsync(
                "Новый курс",
                "Описание курса",
                maxLength: 500);

            if (string.IsNullOrWhiteSpace(description)) return;

            // Получаем список всех пользователей
            var usersResponse = await _httpClient.GetAsync("api/Message/users");
            if (!usersResponse.IsSuccessStatusCode)
            {
                await DisplayAlert("Ошибка", "Не удалось загрузить список пользователей", "OK");
                return;
            }

            var users = await usersResponse.Content.ReadFromJsonAsync<List<ChatUserDto>>();
            if (users == null)
            {
                await DisplayAlert("Ошибка", "Не удалось получить список пользователей", "OK");
                return;
            }

            // Получаем список преподавателей
            var teachers = users.Where(u => u.Role == "Teacher").ToList();
            if (!teachers.Any())
            {
                await DisplayAlert("Ошибка", "Нет доступных преподавателей", "OK");
                return;
            }

            // Выбираем преподавателей
            var teacherNames = teachers.Select(t => t.FullName).ToArray();
            var selectedTeacher = await DisplayActionSheet(
                "Выберите преподавателя",
                "Отмена",
                null,
                teacherNames);

            if (selectedTeacher == "Отмена" || string.IsNullOrEmpty(selectedTeacher)) return;

            var teacherId = teachers.First(t => t.FullName == selectedTeacher).Id;

            // Получаем список студентов
            var students = users.Where(u => u.Role == "Student").ToList();
            if (!students.Any())
            {
                await DisplayAlert("Ошибка", "Нет доступных студентов", "OK");
                return;
            }

            // Выбираем студента
            var studentNames = students.Select(s => s.FullName).ToArray();
            var selectedStudent = await DisplayActionSheet(
                "Выберите студента",
                "Отмена",
                null,
                studentNames);

            if (selectedStudent == "Отмена" || string.IsNullOrEmpty(selectedStudent)) return;

            var studentId = students.First(s => s.FullName == selectedStudent).Id;

            // Создаем запрос на создание курса
            var createCourseRequest = new CreateCourseRequest
            {
                Name = name,
                Description = description,
                SubjectId = 1, // Можно добавить выбор предмета позже
                TeacherIds = new List<int> { teacherId },
                StudentIds = new List<int> { studentId }
            };

            // Отправляем запрос
            var response = await _httpClient.PostAsJsonAsync("api/Course", createCourseRequest);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<CreateCourseResponse>();
                if (result != null)
                {
                    await DisplayAlert("Успех", $"Курс успешно создан (ID: {result.CourseId})", "OK");
                    await LoadCourses(); // Перезагружаем список курсов
                }
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                await DisplayAlert("Ошибка", error, "OK");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error creating course: {ex}");
            await DisplayAlert("Ошибка", "Не удалось создать курс", "OK");
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

                // Используем различные варианты навигации
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
                        await DisplayAlert("Ошибка", "Не удалось открыть задания курса", "OK");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Course selection error: {ex}");
                await DisplayAlert("Ошибка", "Не удалось открыть задания курса", "OK");
            }
        }
    }
    private async Task RefreshCourses()
    {
        await LoadCourses();
    }
}