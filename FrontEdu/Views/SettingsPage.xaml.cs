using FrontEdu.Models.Auth;
using FrontEdu.Services;
using System.ComponentModel;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace FrontEdu.Views
{
    public partial class SettingsPage : ContentPage
    {
        private HttpClient _httpClient;
        private bool _isAdmin;
        private AdminSettingsResponse _settings;

        #region Properties
        private string _siteName;
        public string SiteName
        {
            get => _siteName;
            set
            {
                _siteName = value;
                OnPropertyChanged();
            }
        }
        private string _defaultTimeZone;
        public string DefaultTimeZone
        {
            get => _defaultTimeZone;
            set
            {
                _defaultTimeZone = value;
                OnPropertyChanged();
            }
        }

        private int _maxFileSize;
        public int MaxFileSize
        {
            get => _maxFileSize;
            set
            {
                _maxFileSize = value;
                OnPropertyChanged();
            }
        }
        private string _allowedFileTypesString;
        public string AllowedFileTypesString
        {
            get => _allowedFileTypesString;
            set
            {
                _allowedFileTypesString = value;
                OnPropertyChanged();
            }
        }
        private int _maxUploadFilesPerMessage;
        public int MaxUploadFilesPerMessage
        {
            get => _maxUploadFilesPerMessage;
            set
            {
                _maxUploadFilesPerMessage = value;
                OnPropertyChanged();
            }
        }
        private int _defaultPageSize;
        public int DefaultPageSize
        {
            get => _defaultPageSize;
            set
            {
                _defaultPageSize = value;
                OnPropertyChanged();
            }
        }

        private bool _requireEmailVerification;
        public bool RequireEmailVerification
        {
            get => _requireEmailVerification;
            set
            {
                _requireEmailVerification = value;
                OnPropertyChanged();
            }
        }

        private int _passwordMinLength;
        public int PasswordMinLength
        {
            get => _passwordMinLength;
            set
            {
                _passwordMinLength = value;
                OnPropertyChanged();
            }
        }

        private bool _requireStrongPassword;
        public bool RequireStrongPassword
        {
            get => _requireStrongPassword;
            set
            {
                _requireStrongPassword = value;
                OnPropertyChanged();
            }
        }
        #endregion

        public SettingsPage()
        {
            InitializeComponent();
            BindingContext = this;
        }

        private async void OnBackToMainClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("//MainPage");
        }
        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await InitializePage();
        }

        private async Task InitializePage()
        {
            try
            {
                LoadingIndicator.IsVisible = true;
                _httpClient = await AppConfig.CreateHttpClientAsync();

                // Проверяем права администратора
                var permissionsResponse = await _httpClient.GetAsync("api/Profile/me/permissions");
                if (permissionsResponse.IsSuccessStatusCode)
                {
                    var permissions = await permissionsResponse.Content.ReadFromJsonAsync<UserPermissionsResponse>();
                    _isAdmin = permissions?.Permissions.ManageSettings ?? false;

                    if (_isAdmin)
                    {
                        await LoadSettings();
                    }
                    else
                    {
                        await DisplayAlert("Ошибка", "Недостаточно прав для просмотра настроек", "OK");
                        await Shell.Current.GoToAsync("..");
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Ошибка", "Не удалось загрузить настройки", "OK");
            }
            finally
            {
                LoadingIndicator.IsVisible = false;
            }
        }

        private async Task LoadSettings()
        {
            try
            {
                var response = await _httpClient.GetAsync("api/Admin/settings");
                if (response.IsSuccessStatusCode)
                {
                    _settings = await response.Content.ReadFromJsonAsync<AdminSettingsResponse>();
                    UpdateUI(_settings);
                }
                else
                {
                    await DisplayAlert("Ошибка", "Не удалось загрузить настройки", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Ошибка", "Не удалось загрузить настройки", "OK");
            }
        }

        private void UpdateUI(AdminSettingsResponse settings)
        {
            if (settings == null) return;

            SiteName = settings.SiteName;
            DefaultTimeZone = settings.DefaultTimeZone;
            MaxFileSize = settings.MaxFileSize;
            AllowedFileTypesString = string.Join(", ", settings.AllowedFileTypes);
            MaxUploadFilesPerMessage = settings.MaxUploadFilesPerMessage;
            DefaultPageSize = settings.DefaultPageSize;
            RequireEmailVerification = settings.RequireEmailVerification;
            PasswordMinLength = settings.PasswordMinLength;
            RequireStrongPassword = settings.RequireStrongPassword;
        }

        private async void OnSaveSettingsClicked(object sender, EventArgs e)
        {
            if (!_isAdmin) return;

            try
            {
                LoadingIndicator.IsVisible = true;

                var settings = new AdminSettingsResponse
                {
                    SiteName = SiteName,
                    DefaultTimeZone = DefaultTimeZone,
                    MaxFileSize = MaxFileSize,
                    AllowedFileTypes = AllowedFileTypesString.Split(',').Select(x => x.Trim()).ToList(),
                    MaxUploadFilesPerMessage = MaxUploadFilesPerMessage,
                    DefaultPageSize = DefaultPageSize,
                    RequireEmailVerification = RequireEmailVerification,
                    PasswordMinLength = PasswordMinLength,
                    RequireStrongPassword = RequireStrongPassword
                };

                var response = await _httpClient.PutAsJsonAsync("api/Admin/settings", settings);
                if (response.IsSuccessStatusCode)
                {
                    await DisplayAlert("Успех", "Настройки успешно сохранены", "OK");
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    await DisplayAlert("Ошибка", error, "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Ошибка", "Не удалось сохранить настройки", "OK");
            }
            finally
            {
                LoadingIndicator.IsVisible = false;
            }
        }

        public new event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        public class AdminSettingsResponse
        {
            public string SiteName { get; set; }
            public string DefaultTimeZone { get; set; }
            public int MaxFileSize { get; set; }
            public List<string> AllowedFileTypes { get; set; }
            public int MaxUploadFilesPerMessage { get; set; }
            public int DefaultPageSize { get; set; }
            public bool RequireEmailVerification { get; set; }
            public int PasswordMinLength { get; set; }
            public bool RequireStrongPassword { get; set; }
        }
    }
}