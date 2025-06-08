using FrontEdu.Models.Admin;
using FrontEdu.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Windows.Input;

namespace FrontEdu.Views;

public partial class GroupManagementPage : ContentPage
{
    private HttpClient _httpClient;
    private bool _isLoading;
    private int _selectedGroupId;
    private ObservableCollection<StudentInfo> _students;
    public ICommand RemoveStudentCommand { get; }
    public GroupManagementPage()
	{
        InitializeComponent();
        _students = new ObservableCollection<StudentInfo>();
        StudentsCollection.ItemsSource = _students;
        RemoveStudentCommand = new Command<int>(async (id) => await RemoveStudent(id));
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
            _httpClient = await AppConfig.CreateHttpClientAsync();
            await LoadGroups();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Initialize error: {ex}");
            await DisplayAlert("Ошибка", "Не удалось загрузить список групп", "OK");
        }
    }

    private async Task LoadGroups()
    {
        if (_isLoading) return;

        try
        {
            _isLoading = true;
            LoadingIndicator.IsVisible = true;

            var response = await _httpClient.GetAsync("api/StudentGroup/list");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<StudentGroupListResponse>();
                if (result?.Groups != null)
                {
                    // Теперь используем сам объект GroupListItem вместо анонимного типа
                    GroupPicker.ItemsSource = result.Groups;
                    GroupPicker.ItemDisplayBinding = new Binding("Name");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading groups: {ex}");
            await DisplayAlert("Ошибка", "Не удалось загрузить список групп", "OK");
        }
        finally
        {
            _isLoading = false;
            LoadingIndicator.IsVisible = false;
        }
    }
    private async void OnGroupSelected(object sender, EventArgs e)
    {
        // Получаем выбранный элемент напрямую как GroupListItem
        if (GroupPicker.SelectedItem is GroupListItem group)
        {
            _selectedGroupId = group.Id;
            await LoadGroupDetails();
        }
    }
    private async Task LoadGroupDetails()
    {
        if (_isLoading || _selectedGroupId == 0) return;

        try
        {
            _isLoading = true;
            LoadingIndicator.IsVisible = true;

            var response = await _httpClient.GetAsync($"api/StudentGroup/{_selectedGroupId}");
            if (response.IsSuccessStatusCode)
            {
                var groupDetails = await response.Content.ReadFromJsonAsync<GroupDetailsResponse>();
                if (groupDetails != null)
                {
                    CuratorLabel.Text = groupDetails.Curator != null
                        ? $"Куратор: {groupDetails.Curator.FullName}"
                        : "Нет куратора";

                    _students.Clear();
                    foreach (var student in groupDetails.Students)
                    {
                        _students.Add(student);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading group details: {ex}");
            await DisplayAlert("Ошибка", "Не удалось загрузить информацию о группе", "OK");
        }
        finally
        {
            _isLoading = false;
            LoadingIndicator.IsVisible = false;
        }
    }

    private async void OnSetCuratorClicked(object sender, EventArgs e)
    {
        if (_selectedGroupId == 0)
        {
            await DisplayAlert("Внимание", "Выберите группу", "OK");
            return;
        }

        try
        {
            // Используем новый endpoint для получения доступных преподавателей
            var teacherResponse = await _httpClient.GetAsync("api/profile/teachers?onlyAvailable=true");
            if (!teacherResponse.IsSuccessStatusCode)
            {
                await DisplayAlert("Ошибка", "Не удалось загрузить список преподавателей", "OK");
                return;
            }

            var teachers = await teacherResponse.Content.ReadFromJsonAsync<List<TeacherProfileInfo>>();
            if (teachers == null || !teachers.Any())
            {
                await DisplayAlert("Внимание", "Нет доступных преподавателей", "OK");
                return;
            }

            var teacherChoices = teachers.Select(t => t.FullName).ToArray();
            var selectedTeacher = await DisplayActionSheet("Выберите преподавателя", "Отмена", null, teacherChoices);

            if (selectedTeacher == "Отмена" || string.IsNullOrEmpty(selectedTeacher)) return;

            var teacher = teachers.First(t => t.FullName == selectedTeacher);
            var request = new SetCuratorRequest { TeacherId = teacher.Id };

            var response = await _httpClient.PostAsJsonAsync($"api/StudentGroup/{_selectedGroupId}/curator", request);
            if (response.IsSuccessStatusCode)
            {
                await DisplayAlert("Успех", "Куратор назначен", "OK");
                await LoadGroupDetails();
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                await DisplayAlert("Ошибка", error, "OK");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error setting curator: {ex}");
            await DisplayAlert("Ошибка", "Не удалось назначить куратора", "OK");
        }
    }
    private async void OnRemoveCuratorClicked(object sender, EventArgs e)
    {
        if (_selectedGroupId == 0)
        {
            await DisplayAlert("Внимание", "Выберите группу", "OK");
            return;
        }

        var confirm = await DisplayAlert("Подтверждение",
            "Вы действительно хотите удалить куратора группы?",
            "Да", "Нет");

        if (!confirm) return;

        try
        {
            var response = await _httpClient.DeleteAsync($"api/StudentGroup/{_selectedGroupId}/curator");
            if (response.IsSuccessStatusCode)
            {
                await DisplayAlert("Успех", "Куратор удален", "OK");
                await LoadGroupDetails();
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                await DisplayAlert("Ошибка", error, "OK");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error removing curator: {ex}");
            await DisplayAlert("Ошибка", "Не удалось удалить куратора", "OK");
        }
    }

    private async void OnAddStudentsClicked(object sender, EventArgs e)
    {
        if (_selectedGroupId == 0)
        {
            await DisplayAlert("Внимание", "Выберите группу", "OK");
            return;
        }

        try
        {
            // Используем новый endpoint для получения студентов без группы
            var response = await _httpClient.GetAsync("api/profile/students?withoutGroup=true");
            if (!response.IsSuccessStatusCode)
            {
                await DisplayAlert("Ошибка", "Не удалось загрузить список студентов", "OK");
                return;
            }

            var studentsResponse = await response.Content.ReadFromJsonAsync<AvailableStudentsResponse>(); // Переименовали переменную
            if (studentsResponse?.Students == null || !studentsResponse.Students.Any())
            {
                await DisplayAlert("Внимание", "Нет доступных студентов", "OK");
                return;
            }

            var selectedIds = new List<int>();
            foreach (var student in studentsResponse.Students) // Используем переименованную переменную
            {
                var result = await DisplayAlert("Выбор студентов",
                    $"Добавить студента {student.FullName}?",
                    "Да", "Нет");

                if (result)
                {
                    selectedIds.Add(student.Id);
                }
            }

            if (selectedIds.Any())
            {
                var addResponse = await _httpClient.PostAsJsonAsync(
                    $"api/StudentGroup/{_selectedGroupId}/students",
                    selectedIds);

                if (addResponse.IsSuccessStatusCode)
                {
                    await DisplayAlert("Успех", "Студенты добавлены в группу", "OK");
                    await LoadGroupDetails();
                }
                else
                {
                    var error = await addResponse.Content.ReadAsStringAsync();
                    await DisplayAlert("Ошибка", error, "OK");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error adding students: {ex}");
            await DisplayAlert("Ошибка", "Не удалось добавить студентов", "OK");
        }
    }
    private async Task RemoveStudent(int studentId)
    {
        try
        {
            var confirm = await DisplayAlert("Подтверждение",
                "Вы действительно хотите удалить студента из группы?",
                "Да", "Нет");

            if (!confirm) return;

            var response = await _httpClient.DeleteAsync(
                $"api/StudentGroup/{_selectedGroupId}/students/{studentId}");

            if (response.IsSuccessStatusCode)
            {
                await DisplayAlert("Успех", "Студент удален из группы", "OK");
                await LoadGroupDetails();
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                await DisplayAlert("Ошибка", error, "OK");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error removing student: {ex}");
            await DisplayAlert("Ошибка", "Не удалось удалить студента", "OK");
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _httpClient = null;
        _isLoading = false;
        _selectedGroupId = 0;
        _students.Clear();
    }
    private async void OnRemoveStudentsClicked(object sender, EventArgs e)
    {
        if (_selectedGroupId == 0)
        {
            await DisplayAlert("Внимание", "Выберите группу", "OK");
            return;
        }

        try
        {
            // Получаем выбранных студентов
            var selectedStudents = _students.Select(s => s.Id).ToList();
            if (!selectedStudents.Any())
            {
                await DisplayAlert("Внимание", "Нет студентов для удаления", "OK");
                return;
            }

            var confirm = await DisplayAlert("Подтверждение",
                "Вы действительно хотите удалить выбранных студентов из группы?",
                "Да", "Нет");

            if (!confirm) return;

            // Создаем DELETE запрос с телом
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Delete,
                RequestUri = new Uri($"{_httpClient.BaseAddress}api/StudentGroup/{_selectedGroupId}/students"),
                Content = JsonContent.Create(selectedStudents)
            };

            // Отправляем запрос
            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<RemoveStudentsResponse>();
                if (result?.RemovedStudents != null)
                {
                    await DisplayAlert("Успех",
                        $"Удалено студентов: {result.RemovedStudents.Count}", "OK");
                    await LoadGroupDetails(); // Перезагружаем информацию о группе
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
            Debug.WriteLine($"Error removing students: {ex}");
            await DisplayAlert("Ошибка", "Не удалось удалить студентов", "OK");
        }
    }
    public class RemoveStudentsResponse
    {
        public List<StudentInfo> RemovedStudents { get; set; }
    }
    // Добавляем новые модели для ответов API
    public class TeacherProfileInfo
    {
        public int Id { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public bool IsCurator { get; set; }
        public List<object> CuratedGroups { get; set; }
        public List<object> TeachingCourses { get; set; }
    }

    public class AvailableStudentsResponse
    {
        public int TotalCount { get; set; }
        public List<StudentProfileInfo> Students { get; set; }
    }

    public class StudentProfileInfo
    {
        public int Id { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string StudentId { get; set; }
        public string PhoneNumber { get; set; }
        public string SocialStatus { get; set; }
        public object Group { get; set; }
        public List<EnrolledCourse> EnrolledCourses { get; set; }
    }

    public class EnrolledCourse
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string SubjectName { get; set; }
    }
    public class GroupDetailsResponse
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public CuratorInfo Curator { get; set; }
        public List<StudentInfo> Students { get; set; }
    }

    public class TeacherInfo
    {
        public int Id { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
    }

    public class StudentGroupListResponse
    {
        public int TotalCount { get; set; }
        public List<GroupListItem> Groups { get; set; }
    }

    public class GroupListItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int StudentsCount { get; set; }
        public CuratorInfo Curator { get; set; }
    }
}
