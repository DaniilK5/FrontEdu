using FrontEdu.Views;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;

namespace FrontEdu
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            RegisterRoutes();
        }

        private void RegisterRoutes()
        {
            // Регистрация маршрутов для навигации
            Routing.RegisterRoute("MainPage", typeof(MainPage));
            Routing.RegisterRoute("Login", typeof(LoginPage));
            Routing.RegisterRoute("Register", typeof(RegisterPage));
            Routing.RegisterRoute("CoursesPage", typeof(CoursesPage));
            Routing.RegisterRoute("AssignmentsPage", typeof(AssignmentsPage));
            Routing.RegisterRoute("ProfilePage", typeof(ProfilePage));
            Routing.RegisterRoute("SettingsPage", typeof(SettingsPage));
            Routing.RegisterRoute("UsersPage", typeof(UsersPage));
            Routing.RegisterRoute("ChatPage", typeof(ChatPage));
            Routing.RegisterRoute("GroupChatsPage", typeof(GroupChatsPage));
            Routing.RegisterRoute("DirectChatPage", typeof(DirectChatPage));
        }

        private async void SetupMenu()
        {
            try
            {
                // Получаем токен
                var token = await SecureStorage.GetAsync("auth_token");
                if (string.IsNullOrEmpty(token))
                {
                    // Если токена нет, показываем только страницу входа
                    Current.GoToAsync("//login");
                    return;
                }

                // Декодируем JWT токен для получения роли
                var handler = new JwtSecurityTokenHandler();
                var jsonToken = handler.ReadToken(token) as JwtSecurityToken;
                var role = jsonToken?.Claims.FirstOrDefault(c => c.Type.Contains("role"))?.Value;

                // Настраиваем видимость пунктов меню в зависимости от роли
                foreach (var item in Items)
                {
                    if (item is FlyoutItem flyoutItem)
                    {
                        switch (flyoutItem.Title)
                        {
                            case "Управление":
                                flyoutItem.IsVisible = role == "Administrator";
                                break;
                            case "Курсы":
                            case "Задания":
                                flyoutItem.IsVisible = role == "Teacher" || role == "Student";
                                break;
                            // Добавьте другие case по необходимости
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting up menu: {ex}");
            }
        }

        // Навигация на конкретную страницу
        public async Task NavigateToCoursesPage()
        {
            await Shell.Current.GoToAsync("//courses");
        }

        // Навигация с параметрами
        public async Task NavigateToCoursesPageWithParams(string courseId)
        {
            await Shell.Current.GoToAsync($"//courses?id={courseId}");
        }

        // Возврат назад
        public async Task NavigateBack()
        {
            await Shell.Current.GoToAsync("..");
        }
    }

}
