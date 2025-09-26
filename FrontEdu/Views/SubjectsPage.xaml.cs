// Views/SubjectsPage.xaml.cs
using FrontEdu.Models.Auth;
using FrontEdu.Models.Course;
using FrontEdu.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.Http.Json;

namespace FrontEdu.Views
{
    public partial class SubjectsPage : ContentPage
    {
        private HttpClient _httpClient;
        private ObservableCollection<SubjectCourseInfo> _subjects;
        private ObservableCollection<SubjectCourseInfo> _allSubjects;
        private string _searchQuery = string.Empty;
        private bool _isLoading;
        private bool _canManageSubjects;

        public SubjectsPage()
        {
            InitializeComponent();
            _subjects = new ObservableCollection<SubjectCourseInfo>();
            _allSubjects = new ObservableCollection<SubjectCourseInfo>();
            SubjectsCollection.ItemsSource = _subjects;
            SubjectsRefreshView.Command = new Command(async () => await RefreshSubjects());
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

                // ��������� ����� �� ���������� ����������
                var permissionsResponse = await _httpClient.GetAsync("api/Profile/me/permissions");
                if (permissionsResponse.IsSuccessStatusCode)
                {
                    var permissions = await permissionsResponse.Content.ReadFromJsonAsync<UserPermissionsResponse>();
                    _canManageSubjects = permissions?.Permissions.ManageCourses ?? false;
                    AddSubjectButton.IsVisible = _canManageSubjects;
                }

                await LoadSubjects();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Initialize error: {ex}");
                await DisplayAlert("������", "�� ������� ��������� ��������", "OK");
            }
            finally
            {
                LoadingIndicator.IsVisible = false;
            }
        }

        private async void OnSubjectSelected(object sender, SelectionChangedEventArgs e)
        {
            Debug.WriteLine("OnSubjectSelected started");

            var selectedSubject = e.CurrentSelection.FirstOrDefault() as SubjectCourseInfo;
            Debug.WriteLine($"Selected subject: {System.Text.Json.JsonSerializer.Serialize(selectedSubject)}");

            if (selectedSubject != null)
            {
                try
                {
                    Debug.WriteLine($"Clearing selection for subject with ID: {selectedSubject.Id}");
                    SubjectsCollection.SelectedItem = null;

                    Debug.WriteLine($"Making API request for subject courses. Subject ID: {selectedSubject.Id}");
                    var endpoint = $"api/Subject/{selectedSubject.Id}/courses";
                    Debug.WriteLine($"API Endpoint: {endpoint}");

                    // ��������� ��������� HttpClient
                    if (_httpClient == null)
                    {
                        Debug.WriteLine("HttpClient is null, creating new instance");
                        _httpClient = await AppConfig.CreateHttpClientAsync();
                    }

                    var response = await _httpClient.GetAsync(endpoint);
                    Debug.WriteLine($"API Response Status: {response.StatusCode}");

                    var responseContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"API Response Content: {responseContent}");

                    if (response.IsSuccessStatusCode)
                    {
                        Debug.WriteLine("Deserializing response content");
                        var subjectCourses = await response.Content.ReadFromJsonAsync<SubjectCoursesResponse>();
                        Debug.WriteLine($"Deserialized subject courses: {System.Text.Json.JsonSerializer.Serialize(subjectCourses)}");

                        if (subjectCourses != null)
                        {
                            Debug.WriteLine("Preparing navigation parameters");
                            var navigationParameter = new Dictionary<string, object>
                    {
                        { "subjectId", selectedSubject.Id },
                        { "subjectName", subjectCourses.Name },
                        { "subjectCode", subjectCourses.Code },
                        { "courses", subjectCourses.Courses }
                    };

                            Debug.WriteLine($"Navigation parameters prepared: {System.Text.Json.JsonSerializer.Serialize(navigationParameter)}");

                            // ������� ������ �������� ��������� � ��������� ������������
                            try
                            {
                                Debug.WriteLine("Attempting navigation with '///' prefix");
                                await Shell.Current.GoToAsync($"///courses", navigationParameter);
                                Debug.WriteLine("First navigation attempt successful");
                            }
                            catch (Exception navEx)
                            {
                                Debug.WriteLine($"First navigation attempt failed: {navEx.Message}");
                                Debug.WriteLine($"Stack trace: {navEx.StackTrace}");

                                try
                                {
                                    Debug.WriteLine("Attempting navigation with '//' prefix");
                                    await Shell.Current.GoToAsync($"//courses", navigationParameter);
                                    Debug.WriteLine("Second navigation attempt successful");
                                }
                                catch (Exception altNavEx)
                                {
                                    Debug.WriteLine($"Second navigation attempt failed: {altNavEx.Message}");
                                    Debug.WriteLine($"Stack trace: {altNavEx.StackTrace}");

                                    try
                                    {
                                        Debug.WriteLine("Attempting navigation with relative path");
                                        await Shell.Current.GoToAsync($"courses", navigationParameter);
                                        Debug.WriteLine("Final navigation attempt successful");
                                    }
                                    catch (Exception lastNavEx)
                                    {
                                        Debug.WriteLine($"Final navigation attempt failed: {lastNavEx.Message}");
                                        Debug.WriteLine($"Stack trace: {lastNavEx.StackTrace}");
                                        Debug.WriteLine($"Navigation parameter types: {string.Join(", ", navigationParameter.Select(p => $"{p.Key}: {p.Value?.GetType().FullName}"))}");
                                        await DisplayAlert("������", "�� ������� ������� �������� ������", "OK");
                                    }
                                }
                            }
                        }
                        else
                        {
                            Debug.WriteLine("Deserialized subject courses is null");
                            await DisplayAlert("������", "�� ������� ��������� ���������� � ������ ��������", "OK");
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"API request failed with status code: {response.StatusCode}");
                        Debug.WriteLine($"Error response content: {responseContent}");
                        await DisplayAlert("������", "�� ������� ��������� ���������� � ������ ��������", "OK");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Exception in OnSubjectSelected: {ex.Message}");
                    Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                    if (ex.InnerException != null)
                    {
                        Debug.WriteLine($"Inner exception: {ex.InnerException.Message}");
                        Debug.WriteLine($"Inner exception stack trace: {ex.InnerException.StackTrace}");
                    }
                    await DisplayAlert("������", "�� ������� ������� ����� ��������", "OK");
                }
            }
            else
            {
                Debug.WriteLine("Selected subject is null");
            }

            Debug.WriteLine("OnSubjectSelected completed");
        }
        private async Task LoadSubjects()
        {
            if (_isLoading || _httpClient == null) return;

            try
            {
                _isLoading = true;
                LoadingIndicator.IsVisible = true;

                var response = await _httpClient.GetAsync("api/Subject?includeCourses=false");
                if (response.IsSuccessStatusCode)
                {
                    var subjects = await response.Content.ReadFromJsonAsync<List<SubjectCourseInfo>>();

                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        _allSubjects.Clear();
                        foreach (var subject in subjects ?? Enumerable.Empty<SubjectCourseInfo>())
                        {
                            _allSubjects.Add(subject);
                        }
                        ApplyFilter();
                    });
                }
                else
                {
                    await DisplayAlert("������", "�� ������� ��������� ��������", "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading subjects: {ex}");
                await DisplayAlert("������", "�� ������� ��������� ��������", "OK");
            }
            finally
            {
                _isLoading = false;
                LoadingIndicator.IsVisible = false;
                SubjectsRefreshView.IsRefreshing = false;
            }
        }

        private void ApplyFilter()
        {
            _subjects.Clear();
            var filteredSubjects = string.IsNullOrWhiteSpace(_searchQuery)
                ? _allSubjects
                : _allSubjects.Where(s =>
                    s.Name.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase) ||
                    s.Description.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase));

            foreach (var subject in filteredSubjects)
            {
                _subjects.Add(subject);
            }
        }

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            _searchQuery = e.NewTextValue?.Trim().ToLower() ?? string.Empty;
            ApplyFilter();
        }

        private async Task RefreshSubjects()
        {
            await LoadSubjects();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _subjects?.Clear();
            _allSubjects?.Clear();
            _httpClient = null;
            _isLoading = false;
        }
        private async void OnAddSubjectClicked(object sender, EventArgs e)
        {
            if (!_canManageSubjects) return;

            try
            {
                LoadingIndicator.IsVisible = true;

                // �������� �������� ��������
                string name = await DisplayPromptAsync(
                    "����� �������",
                    "�������� ��������",
                    maxLength: 100);

                if (string.IsNullOrWhiteSpace(name)) return;

                // �������� ��� ��������
                string code = await DisplayPromptAsync(
                    "����� �������",
                    "��� ��������",
                    maxLength: 10);

                if (string.IsNullOrWhiteSpace(code)) return;

                // ����������� ��� � ������� ������� � ������� �������
                code = code.Trim().ToUpper();

                // �������� �������� ��������
                string description = await DisplayPromptAsync(
                    "����� �������",
                    "�������� ��������",
                    maxLength: 500);

                if (string.IsNullOrWhiteSpace(description)) return;

                // ������� ������
                var createSubjectRequest = new
                {
                    Name = name,
                    Code = code,
                    Description = description
                };

                // ���������� ������
                var response = await _httpClient.PostAsJsonAsync("api/Subject", createSubjectRequest);
                if (response.IsSuccessStatusCode)
                {
                    await DisplayAlert("�����", "������� ������� ������", "OK");
                    await LoadSubjects(); // ������������� ������ ���������
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    await DisplayAlert("������", error, "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating subject: {ex}");
                await DisplayAlert("������", "�� ������� ������� �������", "OK");
            }
            finally
            {
                LoadingIndicator.IsVisible = false;
            }
        }
    }
}