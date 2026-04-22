using CommunityToolkit.Mvvm.ComponentModel;

namespace OpenFmodBank.App.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    [ObservableProperty]
    private string _fsbankclPath = string.Empty;

    [ObservableProperty]
    private int _encodingQuality = 85;

    [ObservableProperty]
    private int _threadCount = 0; // 0 = auto

    [ObservableProperty]
    private bool _forceOverwrite;
}
