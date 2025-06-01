using FrontEdu.Models.Auth;
using FrontEdu.Models.Common;
using FrontEdu.Services;
using System.Net.Http.Json;
using System.Diagnostics;
namespace FrontEdu.Views;

public partial class LoginPage : ContentPage
{
    private HttpClient? _httpClient;

    public LoginPage()
    {
        InitializeComponent();
        InitializeHttpClient();
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

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(EmailEntry.Text) || string.IsNullOrWhiteSpace(PasswordEntry.Text))
        {
            ErrorLabel.Text = "Пожалуйста, заполните все поля";
            ErrorLabel.IsVisible = true;
            return;
        }

        try
        {
            ErrorLabel.IsVisible = false;
            LoadingIndicator.IsVisible = true;
            LoginButton.IsEnabled = false;

            var loginRequest = new LoginRequest
            {
                Email = EmailEntry.Text?.Trim(),
                Password = PasswordEntry.Text
            };

            _httpClient = await AppConfig.CreateHttpClientAsync();
            var response = await _httpClient.PostAsJsonAsync("api/Auth/login", loginRequest);
            var responseContent = await response.Content.ReadAsStringAsync();
            Debug.WriteLine($"Response Status: {response.StatusCode}");
            Debug.WriteLine($"Response Content: {responseContent}");

            if (response.IsSuccessStatusCode)
            {
                try
                {
                    var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();
                    if (!string.IsNullOrEmpty(authResponse?.Token))
                    {
                        await SecureStorage.SetAsync("auth_token", authResponse.Token);

                        // Проверяем сохранение токена
                        var isTokenStored = await AppConfig.VerifyStoredToken();
                        if (!isTokenStored)
                        {
                            ErrorLabel.Text = "Ошибка сохранения токена";
                            ErrorLabel.IsVisible = true;
                            return;
                        }

                        // Выводим токен в отладку
                        Debug.WriteLine($"Stored token: {await AppConfig.GetStoredToken()}");

                        AppConfig.ResetHttpClient();

                        await MainThread.InvokeOnMainThreadAsync(async () =>
                        {
                            // Создаем новый AppShell
                            var appShell = new AppShell();
                            Application.Current.MainPage = appShell;

                            // Переходим на главную страницу
                            await Shell.Current.GoToAsync("//MainPage");
                        });
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Deserialization error: {ex}");
                    ErrorLabel.Text = "Ошибка при обработке ответа сервера";
                    ErrorLabel.IsVisible = true;
                }
            }
            else
            {
                ErrorLabel.Text = $"Ошибка: {response.StatusCode}";
                ErrorLabel.IsVisible = true;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Login error: {ex}");
            ErrorLabel.Text = $"Ошибка: {ex.Message}";
            ErrorLabel.IsVisible = true;
        }
        finally
        {
            LoadingIndicator.IsVisible = false;
            LoginButton.IsEnabled = true;
        }
    }
    private async void OnRegisterClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new RegisterPage());
    }
}