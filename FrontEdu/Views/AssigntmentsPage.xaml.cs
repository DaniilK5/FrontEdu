namespace FrontEdu.Views;

public partial class AssignmentsPage : ContentPage
{
	public AssignmentsPage()
	{
		InitializeComponent();
	}
    private async void OnBackToMainClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//MainPage");
    }
}