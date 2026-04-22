using Wpf.Ui.Controls;

namespace OpenFmodBank.View;

public partial class MainPage : UiPage
{
    public MainPage()
    {
        InitializeComponent();

        // Resolve ViewModel from DI
        var vm = ((App)Application.Current).GetService<Core.MainViewModel>();
        DataContext = vm;
    }
}
