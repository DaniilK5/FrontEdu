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

                // Проверяем права на управление предметами
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
                await DisplayAlert("Ошибка", "Не удалось загрузить предметы", "OK");
            }
            finally
            {
                LoadingIndicator.IsVisible = false;
            }
        }

        private async void OnSubjectSelected(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.FirstOrDefault() is SubjectCourseInfo selectedSubject)
            {
                try
                {
                    SubjectsCollection.SelectedItem = null;

                    // Загружаем курсы предмета
                    var response = await _httpClient.GetAsync($"api/Subject/{selectedSubject.Id}/courses");
                    if (response.IsSuccessStatusCode)
                    {
                        var subjectCourses = await response.Content.ReadFromJsonAsync<SubjectCoursesResponse>();
                        if (subjectCourses != null)
                        {
                            // Создаем параметры навигации
                            var navigationParameter = new Dictionary<string, object>
                    {
                        { "subjectId", selectedSubject.Id },
                        { "subjectName", subjectCourses.Name },
                        { "subjectCode", subjectCourses.Code },
                        { "courses", subjectCourses.Courses }
                    };

                            // Используем относительную навигацию с явным маршрутом
                            try
                            {
                                await Shell.Current.GoToAsync($"///courses", navigationParameter);
                            }
                            catch (Exception navEx)
                            {
                                Debug.WriteLine($"First navigation attempt failed: {navEx}");
                                try
                                {
                                    await Shell.Current.GoToAsync($"//courses", navigationParameter);
                                }
                                catch (Exception altNavEx)
                                {
                                    Debug.WriteLine($"Alternative navigation attempt failed: {altNavEx}");
                                    try
                                    {
                                        // Последняя попытка с относительным путём
                                        await Shell.Current.GoToAsync($"courses", navigationParameter);
                                    }
                                    catch (Exception lastNavEx)
                                    {
                                        Debug.WriteLine($"Final navigation attempt failed: {lastNavEx}");
                                        await DisplayAlert("Ошибка", "Не удалось открыть страницу курсов", "OK");
                                    }
                                }
                            }
                        }
                        else
                        {
                            await DisplayAlert("Ошибка", "Не удалось загрузить информацию о курсах предмета", "OK");
                        }
                    }
                    else
                    {
                        await DisplayAlert("Ошибка", "Не удалось загрузить информацию о курсах предмета", "OK");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Navigation error: {ex}");
                    await DisplayAlert("Ошибка", "Не удалось открыть курсы предмета", "OK");
                }
            }
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
                    await DisplayAlert("Ошибка", "Не удалось загрузить предметы", "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading subjects: {ex}");
                await DisplayAlert("Ошибка", "Не удалось загрузить предметы", "OK");
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

                // Получаем название предмета
                string name = await DisplayPromptAsync(
                    "Новый предмет",
                    "Название предмета",
                    maxLength: 100);

                if (string.IsNullOrWhiteSpace(name)) return;

                // Получаем код предмета
                string code = await DisplayPromptAsync(
                    "Новый предмет",
                    "Код предмета",
                    maxLength: 10);

                if (string.IsNullOrWhiteSpace(code)) return;

                // Преобразуем код в верхний регистр и убираем пробелы
                code = code.Trim().ToUpper();

                // Получаем описание предмета
                string description = await DisplayPromptAsync(
                    "Новый предмет",
                    "Описание предмета",
                    maxLength: 500);

                if (string.IsNullOrWhiteSpace(description)) return;

                // Создаем запрос
                var createSubjectRequest = new
                {
                    Name = name,
                    Code = code,
                    Description = description
                };

                // Отправляем запрос
                var response = await _httpClient.PostAsJsonAsync("api/Subject", createSubjectRequest);
                if (response.IsSuccessStatusCode)
                {
                    await DisplayAlert("Успех", "Предмет успешно создан", "OK");
                    await LoadSubjects(); // Перезагружаем список предметов
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    await DisplayAlert("Ошибка", error, "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating subject: {ex}");
                await DisplayAlert("Ошибка", "Не удалось создать предмет", "OK");
            }
            finally
            {
                LoadingIndicator.IsVisible = false;
            }
        }
    }
}