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
            Debug.WriteLine($"Setting SubjectId: {value}");
            if (_subjectId != value)
            {
                _subjectId = value;
                Debug.WriteLine($"SubjectId changed to: {_subjectId}");
            }
        }
    }
    public SubjectPage()
    {
        try
        {
            Debug.WriteLine("SubjectPage constructor started");
            InitializeComponent();
            Debug.WriteLine("SubjectPage constructor completed");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SubjectPage constructor error: {ex}");
            Debug.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    protected override async void OnAppearing()
    {
        Debug.WriteLine("OnAppearing started");
        base.OnAppearing();
        if (!_isInitialized)
        {
            Debug.WriteLine("Initializing page for the first time");
            await InitializeAsync();
            _isInitialized = true;
        }
        Debug.WriteLine("OnAppearing completed");
    }

    private async Task InitializeAsync()
    {
        Debug.WriteLine($"InitializeAsync started. SubjectId: {SubjectId}");

        if (_httpClient == null)
        {
            Debug.WriteLine("Creating new HttpClient");
            _httpClient = await AppConfig.CreateHttpClientAsync();
        }

        try
        {
            _isLoading = true;
            LoadingIndicator.IsVisible = true;
            Debug.WriteLine($"Requesting courses for subject {SubjectId}");

            var endpoint = $"api/Subject/{SubjectId}/courses";
            Debug.WriteLine($"Endpoint: {endpoint}");

            var response = await _httpClient.GetAsync(endpoint);
            Debug.WriteLine($"Response status code: {response.StatusCode}");

            var content = await response.Content.ReadAsStringAsync();
            Debug.WriteLine($"Response content: {content}");

            if (response.IsSuccessStatusCode)
            {
                var subject = await response.Content.ReadFromJsonAsync<SubjectCoursesResponse>();
                Debug.WriteLine($"Deserialized subject: {System.Text.Json.JsonSerializer.Serialize(subject)}");

                if (subject != null)
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        Debug.WriteLine($"Updating UI with subject name: {subject.Name}");
                        Debug.WriteLine($"Courses count: {subject.Courses?.Count ?? 0}");

                        Title = subject.Name;
                        BindingContext = subject;
                        CoursesCollection.ItemsSource = subject.Courses;
                    });
                }
                else
                {
                    Debug.WriteLine("Deserialized subject is null");
                    await DisplayAlert("Ошибка", "Получены некорректные данные о предмете", "OK");
                }
            }
            else
            {
                Debug.WriteLine($"Error response: {content}");
                await DisplayAlert("Ошибка", "Не удалось загрузить информацию о предмете", "OK");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in InitializeAsync: {ex.Message}");
            Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Debug.WriteLine($"Inner exception: {ex.InnerException.Message}");
                Debug.WriteLine($"Inner exception stack trace: {ex.InnerException.StackTrace}");
            }
            await DisplayAlert("Ошибка", "Не удалось загрузить информацию о предмете", "OK");
        }
        finally
        {
            _isLoading = false;
            LoadingIndicator.IsVisible = false;
            Debug.WriteLine("InitializeAsync completed");
        }
    }
    private async void OnCourseSelected(object sender, SelectionChangedEventArgs e)
    {
        Debug.WriteLine("OnCourseSelected started");
        
        var selectedCourse = e.CurrentSelection.FirstOrDefault() as SubjectCourseInfo;
        Debug.WriteLine($"Selected course: {System.Text.Json.JsonSerializer.Serialize(selectedCourse)}");

        if (selectedCourse != null)
        {
            try
            {
                Debug.WriteLine($"Navigating to AssignmentsPage with courseId: {selectedCourse.Id}");
                CoursesCollection.SelectedItem = null;
                var navigationParameter = new Dictionary<string, object>
                    {
                        { "courseId", selectedCourse.Id }
                    };
                Debug.WriteLine("Preparing navigation");
                await Shell.Current.GoToAsync($"//AssignmentsPage", navigationParameter);
                Debug.WriteLine("Navigation completed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Navigation error in OnCourseSelected: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                Debug.WriteLine($"Navigation error: {ex}");
                await DisplayAlert("Ошибка", "Не удалось открыть курс", "OK");
            }
        }
        else
        {
            Debug.WriteLine("Selected course is null");
        }
        Debug.WriteLine("OnCourseSelected completed");
    }

    protected override void OnDisappearing()
    {
        Debug.WriteLine("OnDisappearing started");
        base.OnDisappearing();
        _httpClient = null;
        _isInitialized = false;
        Debug.WriteLine("OnDisappearing completed");
    }
}