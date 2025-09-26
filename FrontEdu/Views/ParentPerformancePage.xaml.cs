using FrontEdu.Services;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;

namespace FrontEdu.Views;

public partial class ParentPerformancePage : ContentPage
{

    private HttpClient _httpClient;
    private bool _isLoading;
    private DateTime? _startDate;
    private DateTime? _endDate;
    public ParentPerformancePage()
    {
        InitializeComponent();

        // Установка начальных дат
        var today = DateTime.Today;
        StartDatePicker.Date = today.AddMonths(-1);
        EndDatePicker.Date = today;
        _startDate = StartDatePicker.Date;
        _endDate = EndDatePicker.Date;
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
            _httpClient = await AppConfig.CreateHttpClientAsync();
            await LoadChildrenPerformance();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Initialize error: {ex}");
            await DisplayAlert("Ошибка", "Не удалось загрузить данные", "OK");
        }
    }

    private async Task LoadChildrenPerformance()
    {
        if (_isLoading) return;

        try
        {
            _isLoading = true;
            LoadingIndicator.IsVisible = true;

            // Формируем строку запроса с датами в UTC в формате ISO 8601
            var queryParams = new List<string>();
            if (_startDate.HasValue)
            {
                var utcStartDate = DateTime.SpecifyKind(_startDate.Value.Date, DateTimeKind.Utc);
                queryParams.Add($"startDate={utcStartDate:yyyy-MM-ddTHH:mm:ss.fffZ}");
            }

            if (_endDate.HasValue)
            {
                // Устанавливаем конец дня для конечной даты
                var utcEndDate = DateTime.SpecifyKind(_endDate.Value.Date.AddDays(1).AddSeconds(-1), DateTimeKind.Utc);
                queryParams.Add($"endDate={utcEndDate:yyyy-MM-ddTHH:mm:ss.fffZ}");
            }

            var queryString = queryParams.Any() ? $"?{string.Join("&", queryParams)}" : string.Empty;
            Debug.WriteLine($"Query string: {queryString}"); // Добавляем логирование запроса

            var response = await _httpClient.GetAsync($"api/ParentStudent/children/performance{queryString}");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ParentPerformanceResponse>();
                if (result != null)
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        ChildrenCollection.ItemsSource = result.PerformanceData;
                    });
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
            Debug.WriteLine($"Error loading performance: {ex}");
            await DisplayAlert("Ошибка", "Не удалось загрузить успеваемость", "OK");
        }
        finally
        {
            _isLoading = false;
            LoadingIndicator.IsVisible = false;
        }
    }
    private async void OnApplyFilterClicked(object sender, EventArgs e)
    {
        _startDate = StartDatePicker.Date;
        _endDate = EndDatePicker.Date;
        await LoadChildrenPerformance();
    }

    private async void OnChildSelected(object sender, SelectionChangedEventArgs e)
    {
        Debug.WriteLine("OnChildSelected triggered");  // Добавляем логирование

        if (e.CurrentSelection.FirstOrDefault() is ChildPerformanceData child)
        {
            Debug.WriteLine($"Selected child: {child.StudentInfo.FullName}");  // Логируем выбранного ребенка

            var details = new StringBuilder();
            details.AppendLine($"Студент: {child.StudentInfo.FullName}");
            details.AppendLine($"Группа: {child.StudentInfo.Group.Name}");
            details.AppendLine($"Средний балл: {child.OverallPerformance.AverageGrade:F1}");
            details.AppendLine();

            details.AppendLine("Успеваемость по предметам:");
            foreach (var subject in child.SubjectsPerformance)
            {
                details.AppendLine($"- {subject.Subject}: {subject.AverageGrade:F1}");
                details.AppendLine($"  Мин: {subject.MinGrade}, Макс: {subject.MaxGrade}, Всего оценок: {subject.GradesCount}");
                if (subject.LatestGrades.Any())
                {
                    details.AppendLine("  Последние оценки:");
                    foreach (var grade in subject.LatestGrades)
                    {
                        details.AppendLine($"    {grade.Value} - {grade.GradedAt:d} ({grade.Assignment})");
                    }
                }
            }

            details.AppendLine();
            details.AppendLine("Посещаемость:");
            details.AppendLine($"Всего пропусков: {child.AttendanceStats.TotalHours}ч");
            details.AppendLine($"По уважительной: {child.AttendanceStats.ExcusedHours}ч");
            details.AppendLine($"Без уважительной: {child.AttendanceStats.UnexcusedHours}ч");

            if (child.AttendanceStats.LatestAbsences.Any())
            {
                details.AppendLine("\nПоследние пропуски:");
                foreach (var absence in child.AttendanceStats.LatestAbsences.Take(5))
                {
                    details.AppendLine($"- {absence.Date:d}: {absence.Hours}ч - " +
                        $"{(absence.IsExcused ? "Уважительная" : "Неуважительная")}");
                    if (!string.IsNullOrEmpty(absence.Reason))
                        details.AppendLine($"  Причина: {absence.Reason}");
                }
            }

            await DisplayAlert($"Подробная информация", details.ToString(), "OK");

            ChildrenCollection.SelectedItem = null;
        }
        else
        {
            Debug.WriteLine("No child selected or wrong type");  // Логируем, если выбор не работает
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _httpClient = null;
        _isLoading = false;
    }


    public class ParentPerformanceResponse
    {
        public int TotalChildren { get; set; }
        public List<ChildPerformanceData> PerformanceData { get; set; }
    }

    public class ChildPerformanceData
    {
        public StudentInfo StudentInfo { get; set; }
        public OverallPerformance OverallPerformance { get; set; }
        public List<SubjectPerformance> SubjectsPerformance { get; set; }
        public AttendanceStats AttendanceStats { get; set; }
        public PerformancePeriod Period { get; set; }
    }

    public class StudentInfo
    {
        public int Id { get; set; }
        public string FullName { get; set; }
        public string StudentId { get; set; }
        public GroupInfo Group { get; set; }
    }

    public class OverallPerformance
    {
        public double AverageGrade { get; set; }
        public int TotalGrades { get; set; }
        public GradeDistribution GradeDistribution { get; set; }
    }

    public class GradeDistribution
    {
        public int Excellent { get; set; }
        public int Good { get; set; }
        public int Satisfactory { get; set; }
        public int Poor { get; set; }
    }

    public class SubjectPerformance
    {
        public string Subject { get; set; }
        public double AverageGrade { get; set; }
        public int GradesCount { get; set; }
        public int MinGrade { get; set; }
        public int MaxGrade { get; set; }
        public List<GradeInfo> LatestGrades { get; set; }
    }

    public class GradeInfo
    {
        public int Value { get; set; }
        public DateTime GradedAt { get; set; }
        public string Assignment { get; set; }
        public string Instructor { get; set; }
    }

    public class AttendanceStats
    {
        public int TotalHours { get; set; }
        public int ExcusedHours { get; set; }
        public int UnexcusedHours { get; set; }
        public List<AbsenceInfo> LatestAbsences { get; set; }
    }

    public class AbsenceInfo
    {
        public DateTime Date { get; set; }
        public int Hours { get; set; }
        public bool IsExcused { get; set; }
        public string Reason { get; set; }
    }

    public class PerformancePeriod
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public DateTime? FirstGradeDate { get; set; }
        public DateTime? LastGradeDate { get; set; }
    }
}