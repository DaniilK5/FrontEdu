using FrontEdu.Models.Auth;
using FrontEdu.Models.Chat;
using FrontEdu.Models.Course;
using FrontEdu.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.Http.Json;
using System.ComponentModel;
namespace FrontEdu.Views;

// Views/CoursesPage.xaml.cs

[QueryProperty(nameof(SubjectId), "subjectId")]
[QueryProperty(nameof(SubjectName), "subjectName")]
[QueryProperty(nameof(SubjectCode), "subjectCode")]
[QueryProperty(nameof(SubjectCourses), "courses")]
public partial class CoursesPage : ContentPage, INotifyPropertyChanged
{
    private HttpClient _httpClient;
    private ObservableCollection<CourseResponse> _courses;
    private ObservableCollection<CourseResponse> _allCourses;
    private string _searchQuery = string.Empty;
    private bool _isLoading;
    private bool _canManageCourses;
    private List<CourseStudent> _currentStudents;
    private CourseResponse _currentCourse;

    public int SubjectId { get; set; }
    public string SubjectName { get; set; }
    public string SubjectCode { get; set; }
    public List<SubjectCourseInfo> SubjectCourses { get; set; }
    private bool _isNavigating;

    private int _selectedSubjectId = -1;
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
        
        BindingContext = this; // Добавьте эту строку
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
                CanManageCourses = permissions?.Permissions.ManageCourses ?? false; // Используем свойство вместо поля
                AddCourseButton.IsVisible = CanManageCourses;
            }

            // Если есть переданные курсы, отображаем их
            if (SubjectCourses != null)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    Title = $"Темы предмета {SubjectName}";
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
            await DisplayAlert("Ошибка", "Не удалось загрузить темы", "OK");
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
                        Title = $"Темы предмета {subjectCourses.Name}";

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
                await DisplayAlert("Ошибка", "Не удалось загрузить темы", "OK");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading courses: {ex}");
            await DisplayAlert("Ошибка", "Не удалось загрузить темы", "OK");
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
                "Новая тема",
                "Описание темы",
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
                SubjectId = this.SubjectId,
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


    private async void OnCourseInfoClicked(object sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is CourseResponse course)
        {
            try
            {
                _currentCourse = course; // Сохраняем текущий курс
                LoadingIndicator.IsVisible = true;
                Debug.WriteLine($"Loading info for course: {course.Id}");

                // Получаем список студентов курса
                var response = await _httpClient.GetAsync($"api/Course/{course.Id}/students");
                var content = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"API Response: {content}");

                if (!response.IsSuccessStatusCode)
                {
                    await DisplayAlert("Ошибка", "Не удалось загрузить список студентов", "OK");
                    return;
                }

                var students = await response.Content.ReadFromJsonAsync<List<CourseStudent>>();
                _currentStudents = students ?? new List<CourseStudent>();

                // Обновляем информацию в панели
                CourseNameLabel.Text = course.Name;
                CourseDescriptionLabel.Text = course.Description;
                CourseCreatedLabel.Text = $"Создан: {course.CreatedAt:dd.MM.yyyy}";
                CourseStudentsCollection.ItemsSource = _currentStudents;

                // Настраиваем обработчик для кнопки добавления студента
                AddStudentButton.Clicked -= OnAddStudentButtonClicked;
                AddStudentButton.Clicked += OnAddStudentButtonClicked;

                // Показываем панель
                CourseInfoPanel.IsVisible = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error showing course info: {ex}");
                await DisplayAlert("Ошибка", "Не удалось загрузить информацию о курсе", "OK");
            }
            finally
            {
                LoadingIndicator.IsVisible = false;
            }
        }
    }

    private async void OnAddStudentButtonClicked(object sender, EventArgs e)
    {
        if (_currentCourse != null)
        {
            await OnAddStudentClicked(_currentCourse);
        }
    }

    private void OnCloseCourseInfoClicked(object sender, EventArgs e)
    {
        CourseInfoPanel.IsVisible = false;
        AddStudentButton.Clicked -= OnAddStudentButtonClicked;
        _currentCourse = null;
    }

    private async void OnRemoveStudentClicked(object sender, EventArgs e)
    {
        if (_currentCourse == null)
        {
            Debug.WriteLine("Current course is null");
            return;
        }

        if (sender is Button button && button.CommandParameter is CourseStudent student)
        {
            try
            {
                bool confirm = await DisplayAlert("Подтверждение",
                    $"Удалить студента {student.FullName} из темы?",
                    "Да", "Нет");

                if (!confirm) return;

                var deleteResponse = await _httpClient.DeleteAsync(
                    $"api/Course/{_currentCourse.Id}/students/{student.UserId}");

                if (deleteResponse.IsSuccessStatusCode)
                {
                    _currentStudents.Remove(student);
                    CourseStudentsCollection.ItemsSource = null;
                    CourseStudentsCollection.ItemsSource = _currentStudents;
                    await DisplayAlert("Успех", "Студент удален из темы", "OK");
                }
                else
                {
                    var error = await deleteResponse.Content.ReadAsStringAsync();
                    await DisplayAlert("Ошибка", $"Не удалось удалить студента: {error}", "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error removing student: {ex}");
                await DisplayAlert("Ошибка", "Не удалось удалить студента", "OK");
            }
        }
    }

    private async Task OnAddStudentClicked(CourseResponse course)
    {
        try
        {
            if (_httpClient == null)
            {
                _httpClient = await AppConfig.CreateHttpClientAsync();
            }

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

            var currentStudentIds = _currentStudents?.Select(s => s.UserId) ?? Enumerable.Empty<int>();
            var availableStudents = users
                .Where(u => u.Role == "Student" && !currentStudentIds.Contains(u.Id))
                .ToList();

            if (!availableStudents.Any())
            {
                await DisplayAlert("Информация", "Нет доступных студентов для добавления", "OK");
                return;
            }

            var studentNames = availableStudents.Select(s => s.FullName).ToArray();
            var selectedStudent = await DisplayActionSheet(
                "Выберите студента",
                "Отмена",
                null,
                studentNames);

            if (selectedStudent == "Отмена" || string.IsNullOrEmpty(selectedStudent))
                return;

            var studentToAdd = availableStudents.First(s => s.FullName == selectedStudent);
            var addResponse = await _httpClient.PostAsync(
                $"api/Course/{course.Id}/students/{studentToAdd.Id}",
                null);

            if (addResponse.IsSuccessStatusCode)
            {
                var addedStudent = await addResponse.Content.ReadFromJsonAsync<CourseStudent>();
                if (addedStudent != null)
                {
                    _currentStudents.Add(addedStudent);
                    CourseStudentsCollection.ItemsSource = null;
                    CourseStudentsCollection.ItemsSource = _currentStudents;
                    await DisplayAlert("Успех", "Студент добавлен в тему", "OK");
                }
            }
            else
            {
                var error = await addResponse.Content.ReadAsStringAsync();
                await DisplayAlert("Ошибка", $"Не удалось добавить студента: {error}", "OK");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error adding student: {ex}");
            await DisplayAlert("Ошибка", "Не удалось добавить студента", "OK");
        }
    }

    // Добавьте публичное свойство для привязки в XAML
    public bool CanManageCourses
    {
        get => _canManageCourses;
        set
        {
            if (_canManageCourses != value)
            {
                _canManageCourses = value;
                OnPropertyChanged(nameof(CanManageCourses));
            }
        }
    }

    private Frame CreateStudentFrame(CourseStudent student, int courseId, VerticalStackLayout parentLayout, VerticalStackLayout courseInfo)
    {
        var studentFrame = new Frame
        {
            Padding = new Thickness(10),
            Content = new Grid
            {
                ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }
            }
        };

        var studentInfo = new VerticalStackLayout
        {
            Children =
        {
            new Label { Text = student.FullName, FontAttributes = FontAttributes.Bold },
            new Label { Text = student.Email, TextColor = Colors.Grey, FontSize = 14 }
        }
        };

        ((Grid)studentFrame.Content).Add(studentInfo, 0, 0);

        if (_canManageCourses)
        {
            var deleteButton = new Button
            {
                Text = "✕",
                TextColor = Colors.Red,
                BackgroundColor = Colors.Transparent,
                WidthRequest = 40,
                HeightRequest = 40
            };

            deleteButton.Clicked += async (s, args) =>
            {
                try 
                {
                    bool confirm = await DisplayAlert("Подтверждение",
                        $"Удалить студента {student.FullName} из курса?",
                        "Да", "Нет");

                    if (confirm)
                    {
                        Debug.WriteLine($"Attempting to delete student {student.UserId} from course {courseId}");
                        
                        var deleteResponse = await _httpClient.DeleteAsync(
                            $"api/Course/{courseId}/students/{student.UserId}");
                        
                        Debug.WriteLine($"Delete response status: {deleteResponse.StatusCode}");

                        if (deleteResponse.IsSuccessStatusCode)
                        {
                            // Безопасно получаем Label со счетчиком студентов
                            var studentCountLabel = courseInfo.Children
                                .FirstOrDefault(x => x is Label label && 
                                    label.Text?.StartsWith("Количество студентов:") == true) as Label;

                            if (studentCountLabel != null)
                            {
                                parentLayout.Remove(studentFrame);
                                var currentCount = int.Parse(((Label)courseInfo.Children[2]).Text
                                    .Replace("Количество студентов: ", "")) - 1;
                                ((Label)courseInfo.Children[2]).Text = $"Количество студентов: {currentCount}";
                                
                                Debug.WriteLine($"Successfully removed student. New count: {currentCount}");
                                await DisplayAlert("Успех", "Студент удален из курса", "OK");
                            }
                            else
                            {
                                Debug.WriteLine("Could not find student count label");
                                // Всё равно удаляем фрейм и показываем успех, даже если не смогли обновить счетчик
                                parentLayout.Remove(studentFrame);
                                await DisplayAlert("Успех", "Студент удален из курса", "OK");
                            }
                        }
                        else
                        {
                            var error = await deleteResponse.Content.ReadAsStringAsync();
                            Debug.WriteLine($"Server returned error: {error}");
                            await DisplayAlert("Ошибка", $"Не удалось удалить студента: {error}", "OK");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error deleting student: {ex}");
                    await DisplayAlert("Ошибка", "Не удалось удалить студента", "OK");
                }
            };

            ((Grid)studentFrame.Content).Add(deleteButton, 1, 0);
        }

        return studentFrame;
    }
    public class CourseStudent
    {
        public int UserId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string StudentId { get; set; }
        public string Group { get; set; }
        public DateTime EnrolledAt { get; set; }
    }
}