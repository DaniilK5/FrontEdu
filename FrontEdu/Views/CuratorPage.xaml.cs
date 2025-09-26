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
        // ��������� ��������� ���
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

            // �������� ���������� ������ ����� ����� endpoint
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
                    await DisplayAlert("������", "�� �� ��������� ��������� ������", "OK");
                    await Shell.Current.GoToAsync(".."); // ������� �� ���������� ��������
                }
            }
            else
            {
                Debug.WriteLine($"Failed to get curated group: {response.StatusCode}");
                await DisplayAlert("������", "�� ������� �������� ���������� � ���������� ������", "OK");
                await Shell.Current.GoToAsync("..");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Initialize error: {ex}");
            await DisplayAlert("������", "�� ������� ���������������� ��������", "OK");
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

            // ��������� ������ ������� � ������ � UTC
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
                await DisplayAlert("������", error, "OK");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading statistics: {ex}");
            await DisplayAlert("������", "�� ������� ��������� ���������� ������", "OK");
        }
        finally
        {
            _isLoading = false;
            LoadingIndicator.IsVisible = false;
        }
    }


    private void UpdateUI(GroupCuratorResponse statistics)
    {
        // ��������� ���������� � ������
        GroupNameLabel.Text = statistics.GroupInfo.Name;
        TotalStudentsLabel.Text = statistics.Statistics.StudentsCount.ToString();
        AverageGradeLabel.Text = $"{statistics.Statistics.AverageGroupGrade:F1}";
        AverageAbsencesLabel.Text = $"{statistics.Statistics.Attendance.AverageAbsenceHoursPerStudent:F1}�";

        // ��������� ������������� ������
        ExcellentLabel.Text = statistics.Statistics.GradeDistribution.Excellent.ToString();
        GoodLabel.Text = statistics.Statistics.GradeDistribution.Good.ToString();
        SatisfactoryLabel.Text = statistics.Statistics.GradeDistribution.Satisfactory.ToString();
        PoorLabel.Text = statistics.Statistics.GradeDistribution.Poor.ToString();

        // ��������� ������ ���������
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
            // ��������� ��������� ���������� � ��������
            var details = new StringBuilder();
            details.AppendLine($"�������: {student.FullName}");
            details.AppendLine($"������� ����: {student.Performance.AverageGrade:F1}");
            details.AppendLine();

            details.AppendLine("������������ �� ���������:");
            foreach (var subject in student.Performance.SubjectsPerformance)
            {
                details.AppendLine($"- {subject.Subject}: {subject.AverageGrade:F1} (������: {subject.GradesCount})");
            }

            details.AppendLine();
            details.AppendLine("������������:");
            details.AppendLine($"����� ���������: {student.Attendance.TotalAbsences}�");
            details.AppendLine($"�� ������������: {student.Attendance.ExcusedAbsences}�");
            details.AppendLine($"��� ������������: {student.Attendance.UnexcusedAbsences}�");

            await DisplayAlert($"���������� � ��������", details.ToString(), "OK");

            // ������� ���������
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