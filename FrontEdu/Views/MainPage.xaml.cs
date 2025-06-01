using FrontEdu.Services;
using System.IdentityModel.Tokens.Jwt;

namespace FrontEdu.Views
{
    public partial class MainPage : ContentPage
    {
        private string? userRole;

        public MainPage()
        {
            InitializeComponent();
            InitializeAsync();
        }

        private async void InitializeAsync()
        {
            try
            {
                var token = await SecureStorage.GetAsync("auth_token");
                if (!string.IsNullOrEmpty(token))
                {
                    var handler = new JwtSecurityTokenHandler();
                    var jsonToken = handler.ReadToken(token) as JwtSecurityToken;
                    userRole = jsonToken?.Claims.FirstOrDefault(c => c.Type.Contains("role"))?.Value;

                    // Показываем/скрываем элементы в зависимости от роли
                    AdminSection.IsVisible = userRole == "Administrator";
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Ошибка", "Не удалось загрузить данные пользователя", "OK");
            }
        }

        private async void OnLogoutClicked(object sender, EventArgs e)
        {
            bool answer = await DisplayAlert("Выход", "Вы уверены, что хотите выйти?", "Да", "Нет");
            if (answer)
            {
                await SecureStorage.Default.SetAsync("auth_token", null);
                AppConfig.ResetHttpClient();
                Application.Current.MainPage = new AppShell();
                await Shell.Current.GoToAsync("//Login");
            }
        }

        private async void OnCoursesClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("//CoursesPage");
        }

        private async void OnAssignmentsClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("//AssignmentsPage");
        }

        private async void OnProfileClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("//ProfilePage");
        }

        private async void OnSettingsClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("//SettingsPage");
        }

        private async void OnUsersClicked(object sender, EventArgs e)
        {
            if (userRole == "Administrator")
            {
                await Shell.Current.GoToAsync("//UsersPage");
            }
        }
    }
}