using FrontEdu.Models.Auth;
using FrontEdu.Models.Common;
using FrontEdu.Models.Constants;
using FrontEdu.Services;
using System.Net.Http.Json;

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
        if (_httpClient == null)
        {
            try
            {
                _httpClient = await AppConfig.CreateHttpClientAsync();
            }
            catch (Exception)
            {
                ErrorLabel.Text = "Ошибка подключения к серверу";
                ErrorLabel.IsVisible = true;
                return;
            }
        }

        try
        {
            ErrorLabel.IsVisible = false;
            LoadingIndicator.IsVisible = true;
            RegisterButton.IsEnabled = false;

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

            var registerRequest = new RegisterRequest
            {
                FullName = FullNameEntry.Text.Trim(),
                Email = EmailEntry.Text.Trim(),
                Password = PasswordEntry.Text,
                Role = RolePicker.SelectedItem?.ToString(),
                StudentGroup = StudentGroupEntry.IsVisible ? StudentGroupEntry.Text?.Trim() : null,
                ChildrenIds = null // Пока не реализуем выбор детей для родителей
            };

            var response = await _httpClient.PostAsJsonAsync("api/Auth/register", registerRequest);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<string>>();

                if (apiResponse?.Success == true)
                {
                    await DisplayAlert("Успех", "Регистрация выполнена успешно", "OK");
                    await Navigation.PopAsync();
                }
                else
                {
                    ErrorLabel.Text = apiResponse?.Message ?? "Ошибка при регистрации";
                    ErrorLabel.IsVisible = true;
                }
            }
            else
            {
                ErrorLabel.Text = $"Ошибка сервера: {response.StatusCode}\n{responseContent}";
                ErrorLabel.IsVisible = true;
            }
        }
        catch (HttpRequestException ex)
        {
            ErrorLabel.Text = $"Ошибка сети: {ex.Message}";
            ErrorLabel.IsVisible = true;
        }
        catch (Exception ex)
        {
            ErrorLabel.Text = $"Произошла ошибка при регистрации: {ex.Message}";
            ErrorLabel.IsVisible = true;
        }
        finally
        {
            LoadingIndicator.IsVisible = false;
            RegisterButton.IsEnabled = true;
        }
    }
}