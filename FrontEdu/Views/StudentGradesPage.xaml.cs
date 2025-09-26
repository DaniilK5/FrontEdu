using FrontEdu.Services;
using System.Diagnostics;
using System.Net.Http.Json;

namespace FrontEdu.Views;

public partial class StudentGradesPage : ContentPage
{
    private HttpClient _httpClient;
    private bool _isLoading;
    private DateTime? _startDate;
    private DateTime? _endDate;
    private int? _selectedSubjectId;
    private List<SubjectInfo> _subjects;
    public StudentGradesPage()
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

            // Загружаем список предметов
            var response = await _httpClient.GetAsync("api/Subject/list");
            if (response.IsSuccessStatusCode)
            {
                _subjects = await response.Content.ReadFromJsonAsync<List<SubjectInfo>>();
                if (_subjects != null)
                {
                    var allSubjectsOption = new SubjectInfo { Id = -1, Name = "Все предметы" };
                    _subjects.Insert(0, allSubjectsOption);

                    SubjectPicker.ItemsSource = _subjects;
                    SubjectPicker.ItemDisplayBinding = new Binding("Name");
                    SubjectPicker.SelectedIndex = 0;
                }
            }

            await LoadGrades();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Initialize error: {ex}");
            await DisplayAlert("Ошибка", "Не удалось загрузить данные", "OK");
        }
    }

    private async Task LoadGrades()
    {
        if (_isLoading) return;

        try
        {
            _isLoading = true;
            LoadingIndicator.IsVisible = true;

            // Формируем строку запроса
            var queryParams = new List<string>();

            if (_startDate.HasValue)
            {
                var utcStartDate = DateTime.SpecifyKind(_startDate.Value.Date, DateTimeKind.Utc);
                queryParams.Add($"startDate={utcStartDate:yyyy-MM-ddTHH:mm:ss.fffZ}");
            }

            if (_endDate.HasValue)
            {
                var utcEndDate = DateTime.SpecifyKind(_endDate.Value.Date.AddDays(1).AddSeconds(-1), DateTimeKind.Utc);
                queryParams.Add($"endDate={utcEndDate:yyyy-MM-ddTHH:mm:ss.fffZ}");
            }

            if (_selectedSubjectId.HasValue && _selectedSubjectId.Value != -1)
            {
                queryParams.Add($"subjectId={_selectedSubjectId.Value}");
            }

            var queryString = queryParams.Any() ? $"?{string.Join("&", queryParams)}" : string.Empty;
            Debug.WriteLine($"Query string: {queryString}");

            var response = await _httpClient.GetAsync($"api/Profile/me/grades{queryString}");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<StudentGradesResponse>();
                if (result != null)
                {
                    await MainThread.InvokeOnMainThreadAsync(() => UpdateUI(result));
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
            Debug.WriteLine($"Error loading grades: {ex}");
            await DisplayAlert("Ошибка", "Не удалось загрузить оценки", "OK");
        }
        finally
        {
            _isLoading = false;
            LoadingIndicator.IsVisible = false;
        }
    }

    private void UpdateUI(StudentGradesResponse data)
    {
        // Обновляем статистику
        AverageGradeLabel.Text = $"{data.AverageGrade:F1}";
        TotalGradesLabel.Text = data.TotalGrades.ToString();

        // Обновляем распределение оценок
        ExcellentLabel.Text = data.GradeDistribution.Excellent.ToString();
        GoodLabel.Text = data.GradeDistribution.Good.ToString();
        SatisfactoryLabel.Text = data.GradeDistribution.Satisfactory.ToString();
        PoorLabel.Text = data.GradeDistribution.Poor.ToString();

        // Обновляем список оценок
        GradesCollection.ItemsSource = data.Grades.OrderByDescending(g => g.GradedAt);
    }

    private async void OnApplyFilterClicked(object sender, EventArgs e)
    {
        _startDate = StartDatePicker.Date;
        _endDate = EndDatePicker.Date;

        if (SubjectPicker.SelectedItem is SubjectInfo subject)
        {
            _selectedSubjectId = subject.Id;
        }

        await LoadGrades();
    }
    public class StudentGradesResponse
    {
        public int TotalGrades { get; set; }
        public double AverageGrade { get; set; }
        public GradeDistribution GradeDistribution { get; set; }
        public List<SubjectGradeInfo> SubjectsPerformance { get; set; }
        public GradePeriod Period { get; set; }
        public List<GradeDetails> Grades { get; set; }
    }

    public class GradeDistribution
    {
        public int Excellent { get; set; }
        public int Good { get; set; }
        public int Satisfactory { get; set; }
        public int Poor { get; set; }
    }

    public class SubjectGradeInfo
    {
        public int SubjectId { get; set; }
        public string SubjectName { get; set; }
        public string SubjectCode { get; set; }
        public double AverageGrade { get; set; }
        public int GradesCount { get; set; }
        public int MinGrade { get; set; }
        public int MaxGrade { get; set; }
        public List<GradeDetails> LatestGrades { get; set; }
    }

    public class GradeDetails
    {
        public int Id { get; set; }
        public int Value { get; set; }
        public DateTime GradedAt { get; set; }
        public string Comment { get; set; }
        public AssignmentInfo Assignment { get; set; }
        public InstructorInfo Instructor { get; set; }
    }

    public class AssignmentInfo
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public CourseInfo Course { get; set; }
    }

    public class CourseInfo
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public SubjectInfo Subject { get; set; }
    }

    public class SubjectInfo
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Code { get; set; }
    }

    public class InstructorInfo
    {
        public int Id { get; set; }
        public string FullName { get; set; }
    }

    public class GradePeriod
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public DateTime FirstGradeDate { get; set; }
        public DateTime LastGradeDate { get; set; }
    }
}