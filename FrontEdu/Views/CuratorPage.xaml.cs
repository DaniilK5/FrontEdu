using FrontEdu.Models.User;
using FrontEdu.Services;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using FrontEdu.Models.Curator;
namespace FrontEdu.Views;

public partial class CuratorPage : ContentPage
{
    private HttpClient _httpClient;
    private bool _isLoading;
    private DateTime? _startDate;
    private DateTime? _endDate;
    private int _groupId;
    public CuratorPage()
	{
		InitializeComponent();
        // Установка начальных дат
        StartDatePicker.Date = DateTime.Today.AddMonths(-1);
        EndDatePicker.Date = DateTime.Today;
        _startDate = StartDatePicker.Date;
        _endDate = EndDatePicker.Date;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await InitializeAsync();
        if (_httpClient != null)
        {
            await LoadGroupStatistics();
        }
    }


    private async Task InitializeAsync()
    {
        try
        {
            _httpClient = await AppConfig.CreateHttpClientAsync();

            // Получаем курируемую группу через новый endpoint
            var response = await _httpClient.GetAsync("api/StudentGroup/curated");
            if (response.IsSuccessStatusCode)
            {
                var curatedGroups = await response.Content.ReadFromJsonAsync<List<CuratedGroupResponse>>();
                var curatedGroup = curatedGroups?.FirstOrDefault();

                if (curatedGroup != null)
                {
                    _groupId = curatedGroup.Id;
                }
                else
                {
                    await DisplayAlert("Ошибка", "Вы не являетесь куратором группы", "OK");
                    await Shell.Current.GoToAsync(".."); // Возврат на предыдущую страницу
                }
            }
            else
            {
                Debug.WriteLine($"Failed to get curated group: {response.StatusCode}");
                await DisplayAlert("Ошибка", "Не удалось получить информацию о курируемой группе", "OK");
                await Shell.Current.GoToAsync("..");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Initialize error: {ex}");
            await DisplayAlert("Ошибка", "Не удалось инициализировать страницу", "OK");
            await Shell.Current.GoToAsync("..");
        }
    }

    private async Task LoadGroupStatistics()
    {
        if (_isLoading) return;

        try
        {
            _isLoading = true;
            LoadingIndicator.IsVisible = true;

            // Формируем строку запроса с датами в UTC
            var queryParams = new List<string>();
            if (_startDate.HasValue)
                queryParams.Add($"startDate={_startDate.Value.ToUniversalTime():o}");
            if (_endDate.HasValue)
                queryParams.Add($"endDate={_endDate.Value.ToUniversalTime():o}");

            var queryString = queryParams.Any() ? $"?{string.Join("&", queryParams)}" : string.Empty;

            var response = await _httpClient.GetAsync($"api/StudentGroup/{_groupId}/statistics{queryString}");
            if (response.IsSuccessStatusCode)
            {
                var statistics = await response.Content.ReadFromJsonAsync<GroupCuratorResponse>();
                if (statistics != null)
                {
                    await MainThread.InvokeOnMainThreadAsync(() => UpdateUI(statistics));
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
            Debug.WriteLine($"Error loading statistics: {ex}");
            await DisplayAlert("Ошибка", "Не удалось загрузить статистику группы", "OK");
        }
        finally
        {
            _isLoading = false;
            LoadingIndicator.IsVisible = false;
        }
    }


    private void UpdateUI(GroupCuratorResponse statistics)
    {
        // Обновляем информацию о группе
        GroupNameLabel.Text = statistics.GroupInfo.Name;
        TotalStudentsLabel.Text = statistics.Statistics.StudentsCount.ToString();
        AverageGradeLabel.Text = $"{statistics.Statistics.AverageGroupGrade:F1}";
        AverageAbsencesLabel.Text = $"{statistics.Statistics.Attendance.AverageAbsenceHoursPerStudent:F1}ч";

        // Обновляем распределение оценок
        ExcellentLabel.Text = statistics.Statistics.GradeDistribution.Excellent.ToString();
        GoodLabel.Text = statistics.Statistics.GradeDistribution.Good.ToString();
        SatisfactoryLabel.Text = statistics.Statistics.GradeDistribution.Satisfactory.ToString();
        PoorLabel.Text = statistics.Statistics.GradeDistribution.Poor.ToString();

        // Обновляем список студентов
        StudentsCollection.ItemsSource = statistics.Students;
    }

    private async void OnApplyFilterClicked(object sender, EventArgs e)
    {
        _startDate = StartDatePicker.Date;
        _endDate = EndDatePicker.Date;
        await LoadGroupStatistics();
    }

    private async void OnStudentSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is StudentDetails student)
        {
            // Формируем детальную информацию о студенте
            var details = new StringBuilder();
            details.AppendLine($"Студент: {student.FullName}");
            details.AppendLine($"Средний балл: {student.Performance.AverageGrade:F1}");
            details.AppendLine();

            details.AppendLine("Успеваемость по предметам:");
            foreach (var subject in student.Performance.SubjectsPerformance)
            {
                details.AppendLine($"- {subject.Subject}: {subject.AverageGrade:F1} (оценок: {subject.GradesCount})");
            }

            details.AppendLine();
            details.AppendLine("Посещаемость:");
            details.AppendLine($"Всего пропусков: {student.Attendance.TotalAbsences}ч");
            details.AppendLine($"По уважительной: {student.Attendance.ExcusedAbsences}ч");
            details.AppendLine($"Без уважительной: {student.Attendance.UnexcusedAbsences}ч");

            await DisplayAlert($"Информация о студенте", details.ToString(), "OK");

            // Очищаем выделение
            StudentsCollection.SelectedItem = null;
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _httpClient = null;
        _isLoading = false;
    }
}