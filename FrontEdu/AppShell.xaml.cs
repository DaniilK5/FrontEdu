using FrontEdu.Models.Auth;
using FrontEdu.Services;
using FrontEdu.Views;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;

namespace FrontEdu
{
    public partial class AppShell : Shell
    {
        private UserPermissionsResponse _userPermissions;
        public AppShell()
        {
            InitializeComponent();
            RegisterRoutes();
            SetupMenu();
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
            Routing.RegisterRoute("GroupChatPage", typeof(GroupChatPage));
            Routing.RegisterRoute("DirectChatPage", typeof(DirectChatPage));
            /*
            Routing.RegisterRoute("ChatPage", typeof(ChatPage));
            Routing.RegisterRoute("GroupChatsPage", typeof(GroupChatsPage));



            Routing.RegisterRoute("ProfileViewPage", typeof(ProfileViewPage));*/

            /* Регистрируем абсолютные пути
            Routing.RegisterRoute("/ChatPage", typeof(ChatPage));
            Routing.RegisterRoute("/DirectChatPage", typeof(DirectChatPage));*/

        }

        private async void SetupMenu()
        {
            try
            {
                var token = await SecureStorage.GetAsync("auth_token");
                if (string.IsNullOrEmpty(token))
                {
                    await Current.GoToAsync("//login");
                    return;
                }

                var httpClient = await AppConfig.CreateHttpClientAsync();
                var response = await httpClient.GetAsync("api/Profile/me/permissions");

                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine("Failed to get permissions");
                    await Current.GoToAsync("//login");
                    return;
                }

                _userPermissions = await response.Content.ReadFromJsonAsync<UserPermissionsResponse>();

                foreach (var item in Items)
                {
                    if (item is FlyoutItem flyoutItem)
                    {
                        switch (flyoutItem.Title)
                        {
                            case "Управление":
                                flyoutItem.IsVisible = _userPermissions.Permissions.ManageUsers;
                                break;

                            case "Учебные материалы":
                                flyoutItem.IsVisible = _userPermissions.Categories.Courses.CanView;
                                break;

                            case "Задания":
                                flyoutItem.IsVisible = _userPermissions.Categories.Assignments.CanView ||
                                                     _userPermissions.Categories.Assignments.CanSubmit ||
                                                     _userPermissions.Categories.Assignments.CanManage;
                                break;

                            case "Чаты":
                                flyoutItem.IsVisible = _userPermissions.Permissions.SendMessages;
                                break;

                            case "Профиль":
                                flyoutItem.IsVisible = true; // Доступно всем авторизованным
                                break;

                            case "Настройки":
                                flyoutItem.IsVisible = _userPermissions.Permissions.ManageSettings;
                                break;

                            case "Главная":
                                flyoutItem.IsVisible = true; // Доступно всем авторизованным
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting up menu: {ex}");
                await Current.GoToAsync("//login");
            }
        }        // Навигация на конкретную страницу
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
