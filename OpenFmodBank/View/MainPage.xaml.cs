namespace OpenFmodBank.View;

public partial class MainPage : System.Windows.Controls.Page
{
    public MainPage()
    {
        InitializeComponent();

        var vm = ((System.Windows.Application.Current as App)!).GetService<Core.MainViewModel>();
        DataContext = vm;
    }
}
