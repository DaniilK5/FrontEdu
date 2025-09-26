using FrontEdu.Services;
using FrontEdu.Views;
using System.Diagnostics;

namespace FrontEdu
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
#if DEBUG
            AppConfig.ApiBaseUrl = "http://localhost:5105"; // Используем http вместо https для отладки
            CheckTokenOnStartup();
#else
            AppConfig.ApiBaseUrl = "http://localhost:5105"; 
#endif
            MainPage = new NavigationPage(new LoginPage());
        }

        private async void CheckTokenOnStartup()
        {
            var token = await AppConfig.GetStoredToken();
            Debug.WriteLine($"Startup token check: {(token != null ? "Token exists" : "No token found")}");
            
            if (!string.IsNullOrEmpty(token))
            {
                MainPage = new AppShell();
            }
        }
    }
}
