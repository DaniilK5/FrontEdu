using FrontEdu.Models.Absence;
using FrontEdu.Models.Auth;
using FrontEdu.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.Http.Json;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FrontEdu.Views;

public partial class AbsencesPage : ContentPage
{
    private HttpClient _httpClient;
    private ObservableCollection<StudentAbsenceStatistics> _students;
    private ObservableCollection<StudentAbsenceStatistics> _allStudents;
    private string _searchQuery = string.Empty;
    private bool _isLoading;
    private int _currentGroupId;
    private DateTime? _startDate;
    private DateTime? _endDate;
    private bool _showExcused = true;
    private bool _showUnexcused = true;
    private bool _canManageStudents;
    private string _userRole;
    private readonly AbsenceStatisticsViewModel _viewModel;

    public bool CanManageStudents => _canManageStudents;

    public AbsencesPage()
    {
        InitializeComponent();
        _viewModel = new AbsenceStatisticsViewModel();
        BindingContext = _viewModel;
        
        // Инициализация коллекций
        _students = _viewModel.Students;
        _allStudents = new ObservableCollection<StudentAbsenceStatistics>();
        
        // Установка начальных дат
        StartDatePicker.Date = DateTime.Today.AddMonths(-1);
        EndDatePicker.Date = DateTime.Today;
        _startDate = StartDatePicker.Date;
        _endDate = EndDatePicker.Date;

        // Установка начального значения
        _currentGroupId = 1;
    }
    // Добавьте новый метод для обновления UI при выборе конкретной группы

    private void UpdateUIForSingleGroup(GroupAbsenceResponse groupStatistics)
    {
        _viewModel.GroupName = groupStatistics.GroupName;
        _viewModel.TotalStudents = groupStatistics.TotalStudents;
        _viewModel.TotalAbsenceHours = groupStatistics.TotalAbsenceHours;
        _viewModel.ExcusedHours = groupStatistics.ExcusedHours;
        _viewModel.UnexcusedHours = groupStatistics.UnexcusedHours;
        _viewModel.AverageAbsenceHours = groupStatistics.AverageAbsenceHours;

        _allStudents.Clear();
        if (groupStatistics.StudentStatistics != null)
        {
            foreach (var student in groupStatistics.StudentStatistics)
            {
                var studentStats = new StudentAbsenceStatistics
                {
                    StudentId = student.StudentId,
                    Student = student.Student,
                    TotalHours = student.TotalHours,
                    ExcusedHours = student.ExcusedHours,
                    UnexcusedHours = student.UnexcusedHours
                };
                _allStudents.Add(studentStats);
            }
        }

        // Принудительно вызываем обновление фильтра
        ApplyFilter();

        // Для отладки
        Debug.WriteLine($"Updated UI for group {groupStatistics.GroupName}");
        Debug.WriteLine($"Total students with absences: {_allStudents.Count}");
        Debug.WriteLine($"Filtered students: {_viewModel.Students.Count}");
    }

    private void UpdateUIForAllGroups(GroupsStatisticsResponse statistics)
    {
        if (statistics == null)
        {
            ShowEmptyGroupStatistics();
            return;
        }

        _viewModel.GroupName = "Все группы";

        if (statistics.TotalStatistics != null)
        {
            _viewModel.TotalStudents = statistics.TotalStatistics.TotalStudents;
            _viewModel.TotalAbsenceHours = statistics.TotalStatistics.TotalAbsenceHours;
            _viewModel.AverageAbsenceHours = statistics.TotalStatistics.AverageAbsenceHoursPerGroup;
            _viewModel.ExcusedHours = statistics.TotalStatistics.ExcusedHours;
            _viewModel.UnexcusedHours = statistics.TotalStatistics.UnexcusedHours;
        }
        else
        {
            _viewModel.TotalStudents = 0;
            _viewModel.TotalAbsenceHours = 0;
            _viewModel.AverageAbsenceHours = 0;
            _viewModel.ExcusedHours = 0;
            _viewModel.UnexcusedHours = 0;
        }

        _allStudents.Clear();
        if (statistics.GroupsDetails != null)
        {
            foreach (var groupDetail in statistics.GroupsDetails)
            {
                if (groupDetail?.Statistics?.TopAbsentStudents != null)
                {
                    foreach (var student in groupDetail.Statistics.TopAbsentStudents)
                    {
                        if (student != null && groupDetail.GroupInfo != null)
                        {
                            var studentStats = new StudentAbsenceStatistics
                            {
                                StudentId = student.StudentId,
                                Student = $"{student.StudentName} ({groupDetail.GroupInfo.Name})",
                                TotalHours = student.TotalHours,
                                ExcusedHours = student.ExcusedHours,
                                UnexcusedHours = student.UnexcusedHours
                            };
                            _allStudents.Add(studentStats);
                        }
                    }
                }
            }
        }

        // Принудительно вызываем обновление фильтра
        ApplyFilter();

        // Для отладки
        Debug.WriteLine($"Updated UI for all groups");
        Debug.WriteLine($"Total students with absences: {_allStudents.Count}");
        Debug.WriteLine($"Filtered students: {_viewModel.Students.Count}");
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
            _httpClient = await AppConfig.CreateHttpClientAsync();

            // Получаем права пользователя
            var permissionsResponse = await _httpClient.GetAsync("api/Profile/me/permissions");
            if (permissionsResponse.IsSuccessStatusCode)
            {
                var permissions = await permissionsResponse.Content.ReadFromJsonAsync<UserPermissionsResponse>();
                _canManageStudents = permissions?.Permissions.ManageStudents ?? false;
                _userRole = permissions?.Role;
                _viewModel.CanManageStudents = _canManageStudents;

                // Загружаем список групп для администраторов и преподавателей
                if (_userRole?.ToLower() is "administrator" or "teacher")
                {
                    await LoadGroups();
                }
            }

            // Загружаем данные в зависимости от роли
            switch (_userRole?.ToLower())
            {
                case "administrator":
                case "teacher":
                    await LoadGroupStatistics();
                    break;
                case "parent":
                    await LoadChildrenAbsences();
                    break;
                case "student":
                    await LoadStudentAbsences();
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Initialize error: {ex}");
            await DisplayAlert("Ошибка", "Не удалось загрузить информацию о пропусках", "OK");
        }
        finally
        {
            LoadingIndicator.IsVisible = false;
        }
    }

    private async Task LoadGroupStatistics()
    {
        if (_isLoading) return;

        try
        {
            _isLoading = true;
            LoadingIndicator.IsVisible = true;

            // Преобразуем даты в UTC и устанавливаем время начала/конца дня
            var startDate = _startDate?.ToUniversalTime().Date;
            var endDate = _endDate?.ToUniversalTime().Date.AddDays(1).AddTicks(-1);

            // Формируем строку запроса с UTC датами в ISO формате
            var queryParams = new List<string>();
            if (startDate.HasValue)
                queryParams.Add($"startDate={startDate.Value:o}");
            if (endDate.HasValue)
                queryParams.Add($"endDate={endDate.Value:o}");

            var queryString = queryParams.Any() ? $"?{string.Join("&", queryParams)}" : string.Empty;

            if (_httpClient == null)
            {
                _httpClient = await AppConfig.CreateHttpClientAsync();
            }

            // Выбираем endpoint в зависимости от выбранной группы
            string endpoint = _currentGroupId == 0 
                ? $"api/Absence/groups/statistics{queryString}"
                : $"api/Absence/group/{_currentGroupId}/statistics{queryString}";

            Debug.WriteLine($"Requesting: {endpoint}"); // Для отладки

            var response = await _httpClient.GetAsync(endpoint);
            var content = await response.Content.ReadAsStringAsync();
            Debug.WriteLine($"Response: {content}"); // Для отладки

            if (response.IsSuccessStatusCode)
            {
                if (_currentGroupId == 0)
                {
                    var statistics = await response.Content.ReadFromJsonAsync<GroupsStatisticsResponse>();
                    if (statistics != null)
                    {
                        await MainThread.InvokeOnMainThreadAsync(() => UpdateUIForAllGroups(statistics));
                    }
                }
                else
                {
                    var groupStatistics = await response.Content.ReadFromJsonAsync<GroupAbsenceResponse>();
                    if (groupStatistics != null)
                    {
                        await MainThread.InvokeOnMainThreadAsync(() => UpdateUIForSingleGroup(groupStatistics));
                    }
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"Server error: {errorContent}");
                await DisplayAlert("Ошибка", "Не удалось загрузить статистику пропусков", "OK");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading statistics: {ex}");
            await DisplayAlert("Ошибка", "Не удалось загрузить статистику пропусков", "OK");
        }
        finally
        {
            _isLoading = false;
            LoadingIndicator.IsVisible = false;
        }
    }

    private void ShowEmptyGroupStatistics()
    {
        var selectedGroup = _viewModel.SelectedGroup;
        if (selectedGroup != null)
        {
            _viewModel.GroupName = selectedGroup.Name;
            _viewModel.TotalStudents = selectedGroup.StudentsCount;
            _viewModel.TotalAbsenceHours = 0;
            _viewModel.ExcusedHours = 0;
            _viewModel.UnexcusedHours = 0;
            _viewModel.AverageAbsenceHours = 0;
        }
        _allStudents.Clear();
        ApplyFilter();
    }

    private async Task LoadChildrenAbsences()
    {
        try
        {
            LoadingIndicator.IsVisible = true;
            var queryString = BuildQueryString();
            var response = await _httpClient.GetAsync($"api/Absence/parent/children{queryString}");

            if (response.IsSuccessStatusCode)
            {
                // Логируем ответ для отладки
                var rawContent = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"API Response: {rawContent}");

                var absences = await response.Content.ReadFromJsonAsync<List<ParentChildAbsenceDetails>>();
                if (absences != null)
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        _allStudents.Clear();

                        // Обновляем общую статистику на основе данных детей
                        int totalExcused = 0;
                        int totalUnexcused = 0;

                        foreach (var childAbsence in absences)
                        {
                            Debug.WriteLine($"Processing child absence: {System.Text.Json.JsonSerializer.Serialize(childAbsence)}");

                            // Проверяем, что Student не null
                            if (childAbsence?.Student == null)
                            {
                                Debug.WriteLine("Warning: Student is null for a child absence record");
                                continue;
                            }

                            var studentStats = new StudentAbsenceStatistics
                            {
                                StudentId = childAbsence.Student.Id,
                                Student = childAbsence.Student.FullName,
                                TotalHours = childAbsence.TotalHours,
                                ExcusedHours = childAbsence.ExcusedHours,
                                UnexcusedHours = childAbsence.UnexcusedHours
                            };

                            Debug.WriteLine($"Created student stats: {System.Text.Json.JsonSerializer.Serialize(studentStats)}");

                            _allStudents.Add(studentStats);

                            totalExcused += childAbsence.ExcusedHours;
                            totalUnexcused += childAbsence.UnexcusedHours;
                        }

                        // Обновляем статистику в ViewModel
                        _viewModel.GroupName = "Мои дети";
                        _viewModel.TotalStudents = _allStudents.Count;
                        _viewModel.ExcusedHours = totalExcused;
                        _viewModel.UnexcusedHours = totalUnexcused;
                        _viewModel.TotalAbsenceHours = totalExcused + totalUnexcused;
                        _viewModel.AverageAbsenceHours = _allStudents.Count > 0
                            ? (double)_viewModel.TotalAbsenceHours / _allStudents.Count
                            : 0;

                        ApplyFilter();

                        Debug.WriteLine($"Updated ViewModel - Total Students: {_viewModel.TotalStudents}, " +
                                      $"Total Hours: {_viewModel.TotalAbsenceHours}");
                    });
                }
                else
                {
                    Debug.WriteLine("Deserialized response is null");
                }
            }
            else
            {
                Debug.WriteLine($"Error loading children absences: {await response.Content.ReadAsStringAsync()}");
                await DisplayAlert("Ошибка", "Не удалось загрузить информацию о пропусках", "OK");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading children absences: {ex}");
            await DisplayAlert("Ошибка", "Не удалось загрузить информацию о пропусках", "OK");
        }
        finally
        {
            LoadingIndicator.IsVisible = false;
        }
    }
    private async Task LoadStudentAbsences()
    {
        try
        {
            var queryString = BuildQueryString();
            var response = await _httpClient.GetAsync($"api/Absence/student/{_currentGroupId}{queryString}");
            if (response.IsSuccessStatusCode)
            {
                var details = await response.Content.ReadFromJsonAsync<StudentAbsenceDetails>();
                if (details != null)
                {
                    // Преобразуем детали в статистику для отображения
                    var studentStats = new StudentAbsenceStatistics
                    {
                        StudentId = details.StudentInfo.Id,
                        Student = details.StudentInfo.FullName,
                        TotalHours = details.TotalHours,
                        ExcusedHours = details.ExcusedHours,
                        UnexcusedHours = details.UnexcusedHours
                    };

                    _allStudents.Clear();
                    _allStudents.Add(studentStats);
                    ApplyFilter();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading student absences: {ex}");
            await DisplayAlert("Ошибка", "Не удалось загрузить информацию о пропусках", "OK");
        }
    }

    private string BuildQueryString()
    {
        var parameters = new List<string>();

        if (_startDate.HasValue)
        {
            var startDate = _startDate.Value.ToUniversalTime().Date;
            parameters.Add($"startDate={startDate:o}");
        }
        if (_endDate.HasValue)
        {
            var endDate = _endDate.Value.ToUniversalTime().Date.AddDays(1).AddTicks(-1);
            parameters.Add($"endDate={endDate:o}");
        }
        if (!_showExcused && _showUnexcused)
            parameters.Add("isExcused=false");
        if (_showExcused && !_showUnexcused)
            parameters.Add("isExcused=true");

        return parameters.Any() ? $"?{string.Join("&", parameters)}" : string.Empty;
    }

    private void ApplyFilter()
    {
        var filteredStudents = _allStudents.ToList();

        // Применяем текстовый поиск
        if (!string.IsNullOrWhiteSpace(_searchQuery))
        {
            filteredStudents = filteredStudents.Where(s =>
                s.Student.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        // Применяем фильтр по типу пропусков
        if (!_showExcused || !_showUnexcused)
        {
            filteredStudents = filteredStudents.Where(s =>
                (_showExcused && s.ExcusedHours > 0) ||
                (_showUnexcused && s.UnexcusedHours > 0)).ToList();
        }

        // Обновляем коллекцию через ViewModel
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _viewModel.Students.Clear();
            foreach (var student in filteredStudents)
            {
                _viewModel.Students.Add(student);
            }
        });

        // Для отладки
        Debug.WriteLine($"Applied filter. Total students: {filteredStudents.Count}");
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        _searchQuery = e.NewTextValue?.Trim().ToLower() ?? string.Empty;
        ApplyFilter();
    }

    private void OnFilterChanged(object sender, EventArgs e)
    {
        _showExcused = ExcusedCheckBox.IsChecked;
        _showUnexcused = UnexcusedCheckBox.IsChecked;
        ApplyFilter();
    }

    private async void OnGroupSelected(object sender, EventArgs e)
    {
        if (_viewModel.SelectedGroup != null)
        {
            _currentGroupId = _viewModel.SelectedGroup.Id;
            await LoadGroupStatistics(); // Используем единый метод для загрузки статистики
        }
    }

    private void UpdateUIForSelectedGroup(GroupDetail groupDetail)
    {
        if (groupDetail?.GroupInfo == null || groupDetail.Statistics == null)
        {
            ShowEmptyGroupStatistics();
            return;
        }

        _viewModel.GroupName = groupDetail.GroupInfo.Name;
        _viewModel.TotalStudents = groupDetail.GroupInfo.StudentsCount;
        _viewModel.TotalAbsenceHours = groupDetail.Statistics.TotalAbsenceHours;
        _viewModel.ExcusedHours = groupDetail.Statistics.ExcusedHours;
        _viewModel.UnexcusedHours = groupDetail.Statistics.UnexcusedHours;
        _viewModel.AverageAbsenceHours = groupDetail.Statistics.AverageAbsenceHoursPerStudent;

        _allStudents.Clear();
        if (groupDetail.Statistics.TopAbsentStudents != null)
        {
            foreach (var student in groupDetail.Statistics.TopAbsentStudents)
            {
                if (student != null)
                {
                    var studentStats = new StudentAbsenceStatistics
                    {
                        StudentId = student.StudentId,
                        Student = student.StudentName,
                        TotalHours = student.TotalHours,
                        ExcusedHours = student.ExcusedHours,
                        UnexcusedHours = student.UnexcusedHours
                    };
                    _allStudents.Add(studentStats);
                }
            }
        }

        // Принудительно вызываем обновление фильтра
        ApplyFilter();

        // Для отладки
        Debug.WriteLine($"Updated UI for group {groupDetail.GroupInfo.Name}");
        Debug.WriteLine($"Total students in list: {_allStudents.Count}");
        Debug.WriteLine($"Filtered students: {_students.Count}");
    }

    private void OnDateFilterChanged(object sender, DateChangedEventArgs e)
    {
        if (sender == StartDatePicker)
            _startDate = e.NewDate;
        else if (sender == EndDatePicker)
            _endDate = e.NewDate;

        // Перезагружаем данные с новыми датами
        _ = LoadGroupStatistics();
    }

    private async void OnStudentSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is StudentAbsenceStatistics selectedStudent)
        {
            StudentsCollection.SelectedItem = null;
            try
            {
                LoadingIndicator.IsVisible = true;

                var response = await _httpClient.GetAsync($"api/Absence/student/{selectedStudent.StudentId}");
                if (response.IsSuccessStatusCode)
                {
                    var details = await response.Content.ReadFromJsonAsync<AbsenceDetailResponse>();
                    if (details != null)
                    {
                        var absenceList = string.Join("\n", details.Absences.OrderByDescending(a => a.Date).Select(a =>
                            $"Дата: {a.Date:dd.MM.yyyy}\n" +
                            $"Часов: {a.Hours}\n" +
                            $"Причина: {a.Reason}\n" +
                            $"{(a.IsExcused ? "Уважительная" : "Неуважительная")}" +
                            $"{(!string.IsNullOrEmpty(a.Comment) ? $"\nКомментарий: {a.Comment}" : "")}\n" +
                            $"Преподаватель: {a.Instructor.FullName}\n"));

                        string message =
                            $"Студент: {details.StudentInfo.FullName}\n" +
                            $"Всего пропущено: {details.TotalHours}ч\n" +
                            $"По уважительной: {details.ExcusedHours}ч\n" +
                            $"Без уважительной: {details.UnexcusedHours}ч\n\n" +
                            "История пропусков:\n" +
                            $"{absenceList}";

                        await DisplayAlert("Информация о пропусках", message, "OK");
                    }
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    await DisplayAlert("Ошибка", "Не удалось загрузить детали пропусков", "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading absence details: {ex}");
                await DisplayAlert("Ошибка", "Не удалось загрузить детали пропусков", "OK");
            }
            finally
            {
                LoadingIndicator.IsVisible = false;
            }
        }
    }
    private async void OnAddAbsenceClicked(object sender, EventArgs e)
    {
        try
        {
            LoadingIndicator.IsVisible = true;

            // Получаем список доступных студентов
            var studentsResponse = await _httpClient.GetAsync("api/Absence/available-students");
            if (!studentsResponse.IsSuccessStatusCode)
            {
                await DisplayAlert("Ошибка", "Не удалось загрузить список студентов", "OK");
                return;
            }

            var availableStudents = await studentsResponse.Content.ReadFromJsonAsync<AvailableStudentsResponse>();
            if (availableStudents?.Groups == null || !availableStudents.Groups.Any())
            {
                await DisplayAlert("Ошибка", "Нет доступных студентов", "OK");
                return;
            }

            // Создаем список для выбора студента
            var studentChoices = new List<string>();
            var studentIds = new List<int>();

            foreach (var group in availableStudents.Groups)
            {
                foreach (var student in group.Students)
                {
                    studentChoices.Add($"{student.FullName} ({group.GroupName})");
                    studentIds.Add(student.Id);
                }
            }

            // Показываем диалог выбора студента
            string selectedStudent = await DisplayActionSheet(
                "Выберите студента",
                "Отмена",
                null,
                studentChoices.ToArray());

            if (selectedStudent == "Отмена" || string.IsNullOrEmpty(selectedStudent))
                return;

            // Получаем ID выбранного студента
            int selectedIndex = studentChoices.IndexOf(selectedStudent);
            if (selectedIndex == -1)
                return;

            int studentId = studentIds[selectedIndex];

            // Получаем количество часов
            string hoursStr = await DisplayPromptAsync(
                "Новый пропуск",
                "Количество часов",
                keyboard: Keyboard.Numeric,
                maxLength: 2);

            if (string.IsNullOrWhiteSpace(hoursStr) || !int.TryParse(hoursStr, out int hours))
                return;

            // Получаем причину
            string reason = await DisplayPromptAsync(
                "Новый пропуск",
                "Причина",
                maxLength: 200);

            if (string.IsNullOrWhiteSpace(reason))
                return;

            // Получаем признак уважительности
            bool isExcused = await DisplayAlert(
                "Новый пропуск",
                "Пропуск по уважительной причине?",
                "Да",
                "Нет");

            // Получаем комментарий
            string comment = await DisplayPromptAsync(
                "Новый пропуск",
                "Комментарий (необязательно)",
                maxLength: 500);

            var createRequest = new
            {
                StudentId = studentId,
                Date = DateTime.UtcNow,
                Hours = hours,
                Reason = reason,
                IsExcused = isExcused,
                Comment = comment
            };

            var response = await _httpClient.PostAsJsonAsync("api/Absence", createRequest);
            if (response.IsSuccessStatusCode)
            {
                await DisplayAlert("Успех", "Пропуск успешно добавлен", "OK");
                await LoadGroupStatistics();
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                await DisplayAlert("Ошибка", error, "OK");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error adding absence: {ex}");
            await DisplayAlert("Ошибка", "Не удалось добавить пропуск", "OK");
        }
        finally
        {
            LoadingIndicator.IsVisible = false;
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _students?.Clear();
        _allStudents?.Clear();
        _httpClient = null;
        _isLoading = false;
    }

    private async Task LoadGroups()
    {
        try
        {
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
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        _viewModel.Groups.Clear();
                        // Добавляем опцию "Все группы" только для администраторов
                        if (_userRole?.ToLower() == "administrator")
                        {
                            _viewModel.Groups.Add(new GroupInfo { Id = 0, Name = "Все группы" });
                        }
                        
                        // Добавляем группы из ответа API
                        foreach (var group in result.Groups.OrderBy(g => g.Name))
                        {
                            _viewModel.Groups.Add(new GroupInfo 
                            { 
                                Id = group.Id,
                                Name = group.Name,
                                Description = group.Description,
                                StudentsCount = group.StudentsCount,
                                Curator = group.Curator != null ? new CuratorInfo 
                                { 
                                    Id = group.Curator.Id,
                                    FullName = group.Curator.FullName 
                                } : null
                            });
                        }
                        
                        // Устанавливаем начальное значение
                        _viewModel.SelectedGroup = _viewModel.Groups.FirstOrDefault();
                    });
                }
            }
            else
            {
                Debug.WriteLine($"Error loading groups: {await response.Content.ReadAsStringAsync()}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading groups: {ex}");
        }
    }
}

public class AbsenceStatisticsViewModel : INotifyPropertyChanged
{
    private string _groupName;
    private int _totalStudents;
    private int _totalAbsenceHours;
    private double _averageAbsenceHours;
    private int _excusedHours;
    private int _unexcusedHours;
    private bool _canManageStudents;
    private ObservableCollection<GroupInfo> _groups;
    private GroupInfo _selectedGroup;
    private ObservableCollection<StudentAbsenceStatistics> _students;

    public string GroupName
    {
        get => _groupName;
        set
        {
            _groupName = value;
            OnPropertyChanged();
        }
    }

    public int TotalStudents
    {
        get => _totalStudents;
        set
        {
            _totalStudents = value;
            OnPropertyChanged();
        }
    }

    public int TotalAbsenceHours
    {
        get => _totalAbsenceHours;
        set
        {
            _totalAbsenceHours = value;
            OnPropertyChanged();
        }
    }

    public double AverageAbsenceHours
    {
        get => _averageAbsenceHours;
        set
        {
            _averageAbsenceHours = value;
            OnPropertyChanged();
        }
    }

    public int ExcusedHours
    {
        get => _excusedHours;
        set
        {
            _excusedHours = value;
            OnPropertyChanged();
        }
    }

    public int UnexcusedHours
    {
        get => _unexcusedHours;
        set
        {
            _unexcusedHours = value;
            OnPropertyChanged();
        }
    }

    public bool CanManageStudents
    {
        get => _canManageStudents;
        set
        {
            _canManageStudents = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<GroupInfo> Groups
    {
        get => _groups;
        set
        {
            _groups = value;
            OnPropertyChanged();
        }
    }

    public GroupInfo SelectedGroup
    {
        get => _selectedGroup;
        set
        {
            _selectedGroup = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<StudentAbsenceStatistics> Students
    {
        get => _students;
        set
        {
            _students = value;
            OnPropertyChanged();
        }
    }
    
    public AbsenceStatisticsViewModel()
    {
        Groups = new ObservableCollection<GroupInfo>();
        Students = new ObservableCollection<StudentAbsenceStatistics>();
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

// Добавьте эти классы для десериализации ответа API
public class GroupsStatisticsResponse
{
    public TotalStatistics TotalStatistics { get; set; }
    public List<GroupDetail> GroupsDetails { get; set; }
}

public class TotalStatistics
{
    public int TotalGroups { get; set; }
    public int TotalStudents { get; set; }
    public int TotalAbsenceHours { get; set; }
    public int ExcusedHours { get; set; }
    public int UnexcusedHours { get; set; }
    public double AverageAbsenceHoursPerGroup { get; set; }
    public int GroupsWithNoAbsences { get; set; }
    public Period Period { get; set; }
}

public class Period
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public class GroupDetail
{
    public GroupInfo GroupInfo { get; set; }
    public GroupStatistics Statistics { get; set; }
}

public class GroupInfo
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public int StudentsCount { get; set; }
    public CuratorInfo Curator { get; set; }
}

public class CuratorInfo
{
    public int Id { get; set; }
    public string FullName { get; set; }
}

public class GroupStatistics
{
    public int TotalAbsenceHours { get; set; }
    public int ExcusedHours { get; set; }
    public int UnexcusedHours { get; set; }
    public double AverageAbsenceHoursPerStudent { get; set; }
    public int StudentsWithAbsences { get; set; }
    public List<TopAbsentStudent> TopAbsentStudents { get; set; }
}

public class TopAbsentStudent
{
    public int StudentId { get; set; }
    public string StudentName { get; set; }
    public int TotalHours { get; set; }
    public int ExcusedHours { get; set; }
    public int UnexcusedHours { get; set; }
}

// Добавьте эти классы для десериализации ответа API
public class StudentGroupListResponse
{
    public int TotalCount { get; set; }
    public List<GroupListItem> Groups { get; set; }
}

public class GroupListItem
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public int StudentsCount { get; set; }
    public CuratorBasicInfo Curator { get; set; }
    public bool HasStudents { get; set; }
}

public class CuratorBasicInfo
{
    public int Id { get; set; }
    public string FullName { get; set; }
}

// Добавьте новый класс для ответа API конкретной группы
public class GroupAbsenceResponse
{
    public string GroupName { get; set; }
    public int TotalStudents { get; set; }
    public int TotalAbsenceHours { get; set; }
    public double AverageAbsenceHours { get; set; }
    public int ExcusedHours { get; set; }
    public int UnexcusedHours { get; set; }
    public List<StudentStatistics> StudentStatistics { get; set; }
}

public class StudentStatistics
{
    public int StudentId { get; set; }
    public string Student { get; set; }
    public int TotalHours { get; set; }
    public int ExcusedHours { get; set; }
    public int UnexcusedHours { get; set; }
    public List<AbsenceDate> AbsenceDates { get; set; }
}

// Добавьте эти классы для модели ответа API
public class AvailableStudentsResponse
{
    public int TotalGroups { get; set; }
    public int TotalStudents { get; set; }
    public List<AvailableGroup> Groups { get; set; }
}

public class AvailableGroup
{
    public string GroupName { get; set; }
    public int GroupId { get; set; }
    public CuratorInfo Curator { get; set; }
    public List<AvailableStudent> Students { get; set; }
}

public class AvailableStudent
{
    public int Id { get; set; }
    public string FullName { get; set; }
    public string StudentId { get; set; }
    public int TotalAbsenceHours { get; set; }
    public int ExcusedHours { get; set; }
    public int UnexcusedHours { get; set; }
    public List<AbsenceDate> RecentAbsences { get; set; }
}

