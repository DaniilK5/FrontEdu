using FrontEdu.Models.Absence;
using FrontEdu.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.Http.Json;

namespace FrontEdu.Views;

public partial class AbsencesPage : ContentPage
{
    private HttpClient _httpClient;
    private ObservableCollection<StudentAbsenceStatistics> _students;
    private ObservableCollection<StudentAbsenceStatistics> _allStudents;
    private string _searchQuery = string.Empty;
    private bool _isLoading;
    private int _groupId = 1; // TODO: Добавить выбор группы
    public AbsencesPage()
    {
        InitializeComponent();
        _students = new ObservableCollection<StudentAbsenceStatistics>();
        _allStudents = new ObservableCollection<StudentAbsenceStatistics>();
        StudentsCollection.ItemsSource = _students;
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
            await LoadGroupStatistics();
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

            var response = await _httpClient.GetAsync($"api/Absence/group/{_groupId}/statistics");
            if (response.IsSuccessStatusCode)
            {
                var statistics = await response.Content.ReadFromJsonAsync<GroupAbsenceStatistics>();
                if (statistics != null)
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        BindingContext = statistics;
                        _allStudents.Clear();
                        foreach (var student in statistics.StudentStatistics)
                        {
                            _allStudents.Add(student);
                        }
                        ApplyFilter();
                    });
                }
            }
            else
            {
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

    private void ApplyFilter()
    {
        _students.Clear();
        var filteredStudents = string.IsNullOrWhiteSpace(_searchQuery)
            ? _allStudents
            : _allStudents.Where(s =>
                s.Student.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase));

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