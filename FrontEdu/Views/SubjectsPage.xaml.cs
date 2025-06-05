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
        private ObservableCollection<SubjectInfo> _subjects;
        private ObservableCollection<SubjectInfo> _allSubjects;
        private string _searchQuery = string.Empty;
        private bool _isLoading;
        private bool _canManageSubjects;

        public SubjectsPage()
        {
            InitializeComponent();
            _subjects = new ObservableCollection<SubjectInfo>();
            _allSubjects = new ObservableCollection<SubjectInfo>();
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
                    var subjects = await response.Content.ReadFromJsonAsync<List<SubjectInfo>>();

                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        _allSubjects.Clear();
                        foreach (var subject in subjects ?? Enumerable.Empty<SubjectInfo>())
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
                    s.Code.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase) ||
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