using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using OpenFmodBank.App.ViewModels;
using OpenFmodBank.App.Views;
using OpenFmodBank.Core.Services;

namespace OpenFmodBank.App;

public partial class App : Application
{
    private ServiceProvider? _services;

    public App()
    {
        var sc = new ServiceCollection();

        // Core services
        sc.AddSingleton<FmodBankService>();

        // ViewModels
        sc.AddTransient<MainViewModel>();

        _services = sc.BuildServiceProvider();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var mainWindow = new MainWindow();
        var vm = _services!.GetRequiredService<MainViewModel>();
        mainWindow.DataContext = vm;
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }
}
