using FrontEdu.Models.Auth;
using FrontEdu.Models.Common;
using FrontEdu.Models.Constants;
using FrontEdu.Services;
using System.Net.Http.Json;
using System.Diagnostics;

namespace FrontEdu.Views;

public partial class RegisterPage : ContentPage
{
    private HttpClient? _httpClient;

    public RegisterPage()
    {
        InitializeComponent();
        InitializeHttpClient();

        // Заполняем роли из UserRole
        RolePicker.ItemsSource = UserRoles.AllRoles;
        RolePicker.SelectedIndexChanged += OnRoleSelected;
    }

    private async void InitializeHttpClient()
    {
        try
        {
            _httpClient = await AppConfig.CreateHttpClientAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Ошибка", "Не удалось инициализировать подключение", "OK");
        }
    }

    private void OnRoleSelected(object sender, EventArgs e)
    {
        StudentGroupEntry.IsVisible = RolePicker.SelectedItem?.ToString() == UserRoles.Student;
    }

    private async void OnRegisterClicked(object sender, EventArgs e)
    {
        // Валидация полей
        if (string.IsNullOrWhiteSpace(FullNameEntry.Text) ||
            string.IsNullOrWhiteSpace(EmailEntry.Text) ||
            string.IsNullOrWhiteSpace(PasswordEntry.Text) ||
            RolePicker.SelectedItem == null)
        {
            ErrorLabel.Text = "Пожалуйста, заполните все обязательные поля";
            ErrorLabel.IsVisible = true;
            return;
        }

        try
        {
            ErrorLabel.IsVisible = false;
            LoadingIndicator.IsVisible = true;
            RegisterButton.IsEnabled = false;

            var registerRequest = new RegisterRequest
            {
                FullName = FullNameEntry.Text?.Trim(),
                Email = EmailEntry.Text?.Trim(),
                Password = PasswordEntry.Text,
                Role = RolePicker.SelectedItem?.ToString(),
                StudentGroup = StudentGroupEntry.IsVisible ? StudentGroupEntry.Text?.Trim() : null,
                ChildrenIds = null // Пока не реализуем выбор детей для родителей
            };

            _httpClient = await AppConfig.CreateHttpClientAsync();
            var response = await _httpClient.PostAsJsonAsync("api/Auth/register", registerRequest);
            var responseContent = await response.Content.ReadAsStringAsync();
            Debug.WriteLine($"Response Status: {response.StatusCode}");
            Debug.WriteLine($"Response Content: {responseContent}");

            if (response.IsSuccessStatusCode)
            {
                // Просто проверяем успешность по StatusCode
                await DisplayAlert("Успех", "Регистрация выполнена успешно", "OK");
                
                // После успешной регистрации возвращаемся на страницу входа
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await Navigation.PopAsync();
                });
                return;
            }
            else
            {
                try
                {
                    // Пробуем получить сообщение об ошибке из ответа
                    var errorResponse = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
                    ErrorLabel.Text = errorResponse?.Message ?? 
                                    errorResponse?.Errors?.FirstOrDefault() ?? 
                                    "Произошла ошибка при регистрации";
                }
                catch
                {
                    // Если не удалось распарсить ответ, показываем raw content
                    ErrorLabel.Text = $"Ошибка: {response.StatusCode}\n{responseContent}";
                }
                ErrorLabel.IsVisible = true;
            }
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"Register network error: {ex}");
            ErrorLabel.Text = $"Ошибка сети: {ex.Message}";
            ErrorLabel.IsVisible = true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Register error: {ex}");
            ErrorLabel.Text = $"Ошибка: {ex.Message}";
            ErrorLabel.IsVisible = true;
        }
        finally
        {
            LoadingIndicator.IsVisible = false;
            RegisterButton.IsEnabled = true;
        }
    }
}