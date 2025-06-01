using FrontEdu.Services;

namespace FrontEdu.Views;
public partial class TokenTestPage : ContentPage
{
    public TokenTestPage()
    {
        InitializeComponent();
    }

    private async void OnCheckTokenClicked(object sender, EventArgs e)
    {
        var token = await AppConfig.GetStoredToken();
        if (!string.IsNullOrEmpty(token))
        {
            TokenStatusLabel.Text = $"Токен найден: {token.Substring(0, 20)}...";
        }
        else
        {
            TokenStatusLabel.Text = "Токен не найден";
        }
    }
}