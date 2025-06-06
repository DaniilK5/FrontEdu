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
        _students = new ObservableCollection<StudentAbsenceStatistics>();
        _allStudents = new ObservableCollection<StudentAbsenceStatistics>();
        
        // Установка источника данных
        StudentsCollection.ItemsSource = _students;
        
        // Установка начальных дат
        StartDatePicker.Date = DateTime.Today.AddMonths(-1);
        EndDatePicker.Date = DateTime.Today;
        _startDate = StartDatePicker.Date;
        _endDate = EndDatePicker.Date;

        // Установка начального значения
        _currentGroupId = 1;
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

                // Обновляем ViewModel вместо установки BindingContext
                _viewModel.CanManageStudents = _canManageStudents;
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

            // Преобразуем даты в UTC
            var startDate = _startDate?.ToUniversalTime().Date;
            var endDate = _endDate?.ToUniversalTime().Date;

            // Формируем строку запроса с UTC датами
            var queryParams = new List<string>();
            if (startDate.HasValue)
                queryParams.Add($"startDate={startDate.Value:yyyy-MM-ddTHH:mm:ssZ}");
            if (endDate.HasValue)
                queryParams.Add($"endDate={endDate.Value:yyyy-MM-ddTHH:mm:ssZ}");

            var queryString = queryParams.Any() ? $"?{string.Join("&", queryParams)}" : string.Empty;

            _currentGroupId = 1;

            if (_httpClient == null)
            {
                _httpClient = await AppConfig.CreateHttpClientAsync();
            }

            var response = await _httpClient.GetAsync($"api/Absence/group/{_currentGroupId}/statistics{queryString}");
            if (response.IsSuccessStatusCode)
            {
                var statistics = await response.Content.ReadFromJsonAsync<GroupAbsenceStatistics>();
                if (statistics != null)
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        try
                        {
                            Title = $"Пропуски - Группа {statistics.GroupName ?? "Неизвестная"}";

                            // Обновляем данные в ViewModel
                            _viewModel.GroupName = statistics.GroupName ?? "Неизвестная";
                            _viewModel.TotalStudents = statistics.TotalStudents;
                            _viewModel.TotalAbsenceHours = statistics.TotalAbsenceHours;
                            _viewModel.AverageAbsenceHours = statistics.AverageAbsenceHours;
                            _viewModel.ExcusedHours = statistics.ExcusedHours;
                            _viewModel.UnexcusedHours = statistics.UnexcusedHours;

                            // Обновляем список студентов
                            _allStudents.Clear();
                            if (statistics.StudentStatistics != null)
                            {
                                foreach (var student in statistics.StudentStatistics)
                                {
                                    if (student != null)
                                    {
                                        _allStudents.Add(student);
                                    }
                                }
                            }

                            ApplyFilter();
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error updating UI: {ex}");
                            MainThread.BeginInvokeOnMainThread(async () =>
                            {
                                await DisplayAlert("Ошибка", "Не удалось обновить информацию на экране", "OK");
                            });
                        }
                    });
                }
                else
                {
                    await DisplayAlert("Ошибка", "Получены некорректные данные от сервера", "OK");
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

    private async Task LoadChildrenAbsences()
    {
        try
        {
            var queryString = BuildQueryString();
            var response = await _httpClient.GetAsync($"api/Absence/parent/children{queryString}");
            if (response.IsSuccessStatusCode)
            {
                var absences = await response.Content.ReadFromJsonAsync<List<StudentAbsenceStatistics>>();
                if (absences != null)
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        _allStudents.Clear();
                        foreach (var absence in absences)
                        {
                            _allStudents.Add(absence);
                        }
                        ApplyFilter();
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading children absences: {ex}");
            await DisplayAlert("Ошибка", "Не удалось загрузить информацию о пропусках", "OK");
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
            parameters.Add($"startDate={_startDate.Value:yyyy-MM-dd}");
        if (_endDate.HasValue)
            parameters.Add($"endDate={_endDate.Value:yyyy-MM-dd}");
        if (!_showExcused && _showUnexcused)
            parameters.Add("isExcused=false");
        if (_showExcused && !_showUnexcused)
            parameters.Add("isExcused=true");

        return parameters.Any() ? $"?{string.Join("&", parameters)}" : string.Empty;
    }

    private void ApplyFilter()
    {
        _students.Clear();
        var filteredStudents = _allStudents.ToList(); // Преобразуем в List для дальнейшей работы

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

        foreach (var student in filteredStudents)
        {
            _students.Add(student);
        }
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
        if (GroupPicker.SelectedItem is string groupName)
        {
            // Здесь нужно добавить логику получения ID группы по её имени
            await LoadGroupStatistics();
        }
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
                var parameters = new Dictionary<string, object>
                    {
                        { "studentId", selectedStudent.StudentId }
                    };
                await Shell.Current.GoToAsync($"AbsenceDetailsPage", parameters);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Navigation error: {ex}");
                await DisplayAlert("Ошибка", "Не удалось открыть детали пропусков", "OK");
            }
        }
    }

    private async void OnAddAbsenceClicked(object sender, EventArgs e)
    {
        try
        {
            LoadingIndicator.IsVisible = true;

            // Получаем ID студента (в реальном приложении нужно добавить выбор студента)
            int studentId = 1; // TODO: Добавить выбор студента

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

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}