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
            await DisplayAlert("������", "�� ������� ���������������� �����������", "OK");
        }
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(EmailEntry.Text) || string.IsNullOrWhiteSpace(PasswordEntry.Text))
        {
            ErrorLabel.Text = "����������, ��������� ��� ����";
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

                        // ��������� ���������� ������
                        var isTokenStored = await AppConfig.VerifyStoredToken();
                        if (!isTokenStored)
                        {
                            ErrorLabel.Text = "������ ���������� ������";
                            ErrorLabel.IsVisible = true;
                            return;
                        }

                        // ������� ����� � �������
                        Debug.WriteLine($"Stored token: {await AppConfig.GetStoredToken()}");

                        AppConfig.ResetHttpClient();

                        await MainThread.InvokeOnMainThreadAsync(async () =>
                        {
                            // ������� ����� AppShell
                            var appShell = new AppShell();
                            Application.Current.MainPage = appShell;

                            // ��������� �� ������� ��������
                            await Shell.Current.GoToAsync("//MainPage");
                        });
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Deserialization error: {ex}");
                    ErrorLabel.Text = "������ ��� ��������� ������ �������";
                    ErrorLabel.IsVisible = true;
                }
            }
            else
            {
                ErrorLabel.Text = $"������: {response.StatusCode}";
                ErrorLabel.IsVisible = true;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Login error: {ex}");
            ErrorLabel.Text = $"������: {ex.Message}";
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