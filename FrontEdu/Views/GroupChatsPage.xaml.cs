namespace FrontEdu.Views
{
    public partial class GroupChatsPage : ContentPage
    {
        public GroupChatsPage()
        {
            InitializeComponent();
        }

        private async void OnBackToMainClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("//MainPage");
        }
    }
}