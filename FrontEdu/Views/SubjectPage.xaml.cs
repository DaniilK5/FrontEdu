using FrontEdu.Models.Course;
using FrontEdu.Services;
using System.Diagnostics;
using System.Net.Http.Json;

namespace FrontEdu.Views;

public partial class SubjectPage : ContentPage
{
    private HttpClient _httpClient;
    private int _subjectId;
    private bool _isLoading;
    private bool _isInitialized;
    public int SubjectId
    {
        get => _subjectId;
        set
        {
            if (_subjectId != value)
            {
                _subjectId = value;
            }
        }
    }
    public SubjectPage()
	{
        try
        {
            InitializeComponent();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SubjectPage constructor error: {ex}");
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_isInitialized)
        {
            await InitializeAsync();
            _isInitialized = true;
        }
    }

    private async Task InitializeAsync()
    {
        if (_httpClient == null)
        {
            _httpClient = await AppConfig.CreateHttpClientAsync();
        }

        try
        {
            _isLoading = true;
            LoadingIndicator.IsVisible = true;

            // Запрашиваем информацию о курсах предмета
            var response = await _httpClient.GetAsync($"api/Subject/{SubjectId}/courses");
            if (response.IsSuccessStatusCode)
            {
                var subject = await response.Content.ReadFromJsonAsync<SubjectCoursesResponse>();
                if (subject != null)
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        Title = subject.Name;
                        BindingContext = subject;
                        CoursesCollection.ItemsSource = subject.Courses;
                    });
                }
            }
            else
            {
                await DisplayAlert("Ошибка", "Не удалось загрузить информацию о предмете", "OK");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading subject: {ex}");
            await DisplayAlert("Ошибка", "Не удалось загрузить информацию о предмете", "OK");
        }
        finally
        {
            _isLoading = false;
            LoadingIndicator.IsVisible = false;
        }
    }

    private async void OnCourseSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is SubjectCourseInfo selectedCourse)
        {
            try
            {
                CoursesCollection.SelectedItem = null;
                var navigationParameter = new Dictionary<string, object>
                    {
                        { "courseId", selectedCourse.Id }
                    };
                await Shell.Current.GoToAsync($"//AssignmentsPage", navigationParameter);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Navigation error: {ex}");
                await DisplayAlert("Ошибка", "Не удалось открыть курс", "OK");
            }
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _httpClient = null;
        _isInitialized = false;
    }
}