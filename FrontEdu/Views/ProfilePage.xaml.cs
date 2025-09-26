using FrontEdu.Models.User;
using FrontEdu.Services;
using System.Net.Http.Json;

namespace FrontEdu.Views
{
    public partial class ProfilePage : ContentPage
    {
        private HttpClient? _httpClient;
        private UserProfileDto? _userProfile;

        public ProfilePage()
        {
            InitializeComponent();
            InitializeAsync();
        }

        private async void InitializeAsync()
        {
            try
            {
                LoadingIndicator.IsVisible = true;
                ErrorLabel.IsVisible = false;

                _httpClient = await AppConfig.CreateHttpClientAsync();
                await LoadProfileData();
            }
            catch (Exception ex)
            {
                ErrorLabel.Text = "������ ��� �������� �������";
                ErrorLabel.IsVisible = true;
            }
            finally
            {
                LoadingIndicator.IsVisible = false;
            }
        }

        private async Task LoadProfileData()
        {
            try
            {
                var response = await _httpClient.GetAsync("api/Profile/me");
                
                if (response.IsSuccessStatusCode)
                {
                    _userProfile = await response.Content.ReadFromJsonAsync<UserProfileDto>();
                    if (_userProfile != null)
                    {
                        // ��������� UI
                        FullNameEntry.Text = _userProfile.FullName;
                        PhoneEntry.Text = _userProfile.PhoneNumber;
                        AddressEntry.Text = _userProfile.Address;
                        SocialStatusEntry.Text = _userProfile.SocialStatus;
                        
                        // ����������� ��������� ��������� ��� ��������
                        bool isStudent = _userProfile.Role?.ToLower() == "student";
                        StudentGroupLabel.IsVisible = isStudent;
                        StudentIdLabel.IsVisible = isStudent;
                        
                        if (isStudent)
                        {
                            StudentGroupLabel.Text = $"������: {_userProfile.StudentGroup}";
                            StudentIdLabel.Text = $"������������ �����: {_userProfile.StudentId}";
                        }
                    }
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    ErrorLabel.Text = $"������: {error}";
                    ErrorLabel.IsVisible = true;
                }
            }
            catch (Exception ex)
            {
                ErrorLabel.Text = "������ ��� �������� ������";
                ErrorLabel.IsVisible = true;
            }
        }

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            try
            {
                SaveButton.IsEnabled = false;
                LoadingIndicator.IsVisible = true;
                ErrorLabel.IsVisible = false;

                var updateDto = new UpdateUserProfileDto
                {
                    FullName = FullNameEntry.Text,
                    PhoneNumber = PhoneEntry.Text,
                    Address = AddressEntry.Text,
                    SocialStatus = SocialStatusEntry.Text
                };

                var response = await _httpClient.PutAsJsonAsync("api/Profile/me", updateDto);

                if (response.IsSuccessStatusCode)
                {
                    await DisplayAlert("�����", "������� ������� ��������", "OK");
                    await LoadProfileData(); // ������������� ������
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    ErrorLabel.Text = $"������ ��� ����������: {error}";
                    ErrorLabel.IsVisible = true;
                }
            }
            catch (Exception ex)
            {
                ErrorLabel.Text = "������ ��� ���������� ������";
                ErrorLabel.IsVisible = true;
            }
            finally
            {
                SaveButton.IsEnabled = true;
                LoadingIndicator.IsVisible = false;
            }
        }

        private async void OnBackToMainClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("//MainPage");
        }
    }
}