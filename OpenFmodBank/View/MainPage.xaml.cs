namespace OpenFmodBank.View;

public partial class MainPage : System.Windows.Controls.Page
{
    public MainPage()
    {
        InitializeComponent();

        var vm = ((App)Application.Current).GetService<Core.MainViewModel>();
        DataContext = vm;
    }
}
