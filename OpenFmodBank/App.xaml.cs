using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using OpenFmodBank.Core;
using OpenFmodBank.Services;

namespace OpenFmodBank;

public partial class App : Application
{
    private ServiceProvider? _services;

    public App()
    {
        var sc = new ServiceCollection();

        sc.AddSingleton<FmodBankService>();
        sc.AddTransient<MainViewModel>();

        _services = sc.BuildServiceProvider();
    }

    /// <summary>Service locator for View constructors.</summary>
    public T GetService<T>() where T : notnull =>
        _services!.GetRequiredService<T>();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var mainWindow = new View.MainWindow();
        mainWindow.Show();
    }
}
