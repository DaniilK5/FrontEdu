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
            Loaded += OnShellLoaded;
        }
        private async void OnShellLoaded(object? sender, EventArgs e)
        {
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
            Routing.RegisterRoute("ChatPage", typeof(ChatPage));
            Routing.RegisterRoute("GroupChatsPage", typeof(GroupChatsPage));
            Routing.RegisterRoute("ProfileViewPage", typeof(ProfileViewPage));
        }

        private async void SetupMenu()
        {
            try
            {
                var token = await SecureStorage.GetAsync("auth_token");
                if (string.IsNullOrEmpty(token))
                {
                    AddLoginItem();
                    return;
                }

                var httpClient = await AppConfig.CreateHttpClientAsync();
                var response = await httpClient.GetAsync("api/Profile/me/permissions");

                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine("Failed to get permissions");
                    AddLoginItem();
                    return;
                }

                _userPermissions = await response.Content.ReadFromJsonAsync<UserPermissionsResponse>();

                // Очищаем существующие элементы
                Items.Clear();

                // Добавляем элементы меню в зависимости от разрешений
                AddMainPageItem();

                if (_userPermissions.Permissions.ManageUsers)
                {
                    AddAdminSection();
                }

                // Профиль доступен всем авторизованным
                AddProfileSection();

                if (_userPermissions.Permissions.SendMessages)
                {
                    AddChatsSection();
                }

                if (_userPermissions.Categories.Courses.CanView || 
                    _userPermissions.Categories.Assignments.CanView || 
                    _userPermissions.Categories.Assignments.CanSubmit)
                {
                    AddEducationSection();
                }

                if (_userPermissions.Permissions.ManageSettings)
                {
                    AddSettingsSection();
                }

                // После создания всех элементов меню, навигируем на главную страницу
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    try
                    {
                        await Current.GoToAsync("//MainPage");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Navigation error: {ex}");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting up menu: {ex}");
                AddLoginItem();
            }
        }

        private void AddLoginItem()
        {
            Items.Clear();
            Items.Add(new ShellContent
            {
                Title = "Вход",
                Route = "Login",
                ContentTemplate = new DataTemplate(typeof(LoginPage))
            });
        }

        private void AddMainPageItem()
        {
            if (MenuItemExists("Главная")) return;
            
            var flyoutItem = new FlyoutItem
            {
                Title = "Главная",
                Route = "MainPage"
            };
            flyoutItem.Items.Add(new ShellContent
            {
                ContentTemplate = new DataTemplate(typeof(MainPage))
            });
            Items.Add(flyoutItem);
        }

        private void AddAdminSection()
        {
            if (MenuItemExists("Управление")) return;

            var adminSection = new FlyoutItem
            {
                Title = "Управление"
            };
            adminSection.Items.Add(new ShellContent
            {
                Title = "Пользователи",
                Route = "UsersPage",
                ContentTemplate = new DataTemplate(typeof(UsersPage))
            });
            Items.Add(adminSection);
        }

        private void AddProfileSection()
        {
            if (MenuItemExists("Профиль")) return;

            var profileSection = new FlyoutItem
            {
                Title = "Профиль"
            };
            profileSection.Items.Add(new ShellContent
            {
                Title = "Мой профиль",
                Route = "ProfilePage",
                ContentTemplate = new DataTemplate(typeof(ProfilePage))
            });
            Items.Add(profileSection);
        }

        private void AddChatsSection()
        {
            if (MenuItemExists("Чаты")) return;

            var chatsSection = new FlyoutItem
            {
                Title = "Чаты"
            };
            var tab = new Tab();
            tab.Items.Add(new ShellContent
            {
                Title = "Личные чаты",
                Route = "ChatPage",
                ContentTemplate = new DataTemplate(typeof(ChatPage))
            });
            tab.Items.Add(new ShellContent
            {
                Title = "Групповые чаты",
                Route = "GroupChatsPage",
                ContentTemplate = new DataTemplate(typeof(GroupChatsPage))
            });
            chatsSection.Items.Add(tab);
            Items.Add(chatsSection);
        }

        private void AddEducationSection()
        {
            if (MenuItemExists("Учебные материалы")) return;

            var educationSection = new FlyoutItem
            {
                Title = "Учебные материалы"
            };
            var tab = new Tab();
            
            if (_userPermissions.Categories.Courses.CanView)
            {
                tab.Items.Add(new ShellContent
                {
                    Title = "Курсы",
                    Route = "CoursesPage",
                    ContentTemplate = new DataTemplate(typeof(CoursesPage))
                });
            }
            
            if (_userPermissions.Categories.Assignments.CanView || 
                _userPermissions.Categories.Assignments.CanSubmit)
            {
                tab.Items.Add(new ShellContent
                {
                    Title = "Задания",
                    Route = "AssignmentsPage",
                    ContentTemplate = new DataTemplate(typeof(AssignmentsPage))
                });
            }
            
            educationSection.Items.Add(tab);
            Items.Add(educationSection);
        }

        private void AddSettingsSection()
        {
            if (MenuItemExists("Настройки")) return;

            var settingsSection = new FlyoutItem
            {
                Title = "Настройки"
            };
            settingsSection.Items.Add(new ShellContent
            {
                Route = "SettingsPage",
                ContentTemplate = new DataTemplate(typeof(SettingsPage))
            });
            Items.Add(settingsSection);
        }

        private bool MenuItemExists(string title)
        {
            return Items.Any(item => item is FlyoutItem flyoutItem && flyoutItem.Title == title);
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
