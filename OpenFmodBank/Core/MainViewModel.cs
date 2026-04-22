using System.Diagnostics;
using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenFmodBank.Services;

namespace OpenFmodBank.Core;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly FmodBankService _bankService;

    [ObservableProperty]
    private string _banksPath = Path.Combine(Directory.GetCurrentDirectory(), "banks");

    [ObservableProperty]
    private string _wavsPath = Path.Combine(Directory.GetCurrentDirectory(), "wavs");

    [ObservableProperty]
    private string _buildPath = Path.Combine(Directory.GetCurrentDirectory(), "build");

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private double _progressValue;

    [ObservableProperty]
    private double _progressMaximum = 1;

    [ObservableProperty]
    private bool _isIndeterminate;

    [ObservableProperty]
    private string _consoleOutput = string.Empty;

    [ObservableProperty]
    private string _elapsedText = string.Empty;

    [ObservableProperty]
    private string _fsbankclPath = string.Empty;

    [ObservableProperty]
    private int _encodingQuality = 85;

    [ObservableProperty]
    private int _threadCount;

    [ObservableProperty]
    private bool _forceOverwrite;

    public MainViewModel(FmodBankService bankService)
    {
        _bankService = bankService;
        EnsureDir(BanksPath);
        EnsureDir(WavsPath);
        EnsureDir(BuildPath);
    }

    [RelayCommand]
    private void BrowseBanks()
    {
        var p = PickFolder("Select Banks Folder");
        if (p != null) BanksPath = p;
    }

    [RelayCommand]
    private void BrowseWavs()
    {
        var p = PickFolder("Select Output Folder");
        if (p != null) WavsPath = p;
    }

    [RelayCommand]
    private void BrowseBuild()
    {
        var p = PickFolder("Select Build Folder");
        if (p != null) BuildPath = p;
    }

    [RelayCommand]
    private async Task ExtractAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ConsoleOutput = string.Empty;
        IsIndeterminate = false;
        ProgressValue = 0;

        var sw = Stopwatch.StartNew();
        try
        {
            var config = BuildConfig();
            var progress = new Progress<BankProgress>(p =>
            {
                StatusText = p.StatusText;
                ProgressValue = p.Current;
                ProgressMaximum = Math.Max(p.Maximum, 1);
            });

            var result = await Task.Run(() => _bankService.Extract(config, progress));
            sw.Stop();
            ElapsedText = FormatElapsed(sw.Elapsed);

            if (result.Success)
            {
                StatusText = $"Extraction complete — {result.FilesProcessed} bank(s) in {sw.Elapsed.TotalSeconds:F1}s";
                AppendConsole($"Done. Processed {result.FilesProcessed} bank(s).");
            }
            else
            {
                StatusText = $"Extraction failed: {result.ErrorMessage}";
                AppendConsole($"ERROR: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            AppendConsole($"EXCEPTION: {ex}");
        }
        finally
        {
            IsBusy = false;
            IsIndeterminate = false;
        }
    }

    [RelayCommand]
    private async Task RebuildAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ConsoleOutput = string.Empty;
        IsIndeterminate = true;

        var sw = Stopwatch.StartNew();
        try
        {
            var config = BuildConfig();
            var progress = new Progress<BankProgress>(p =>
            {
                StatusText = p.StatusText;
                if (!string.IsNullOrEmpty(p.CurrentFile))
                    AppendConsole(p.StatusText);
            });

            var result = await Task.Run(() => _bankService.Rebuild(config, progress));
            sw.Stop();
            ElapsedText = FormatElapsed(sw.Elapsed);

            if (result.Success)
            {
                StatusText = $"Rebuild complete — {result.FilesProcessed} bank(s) in {sw.Elapsed.TotalSeconds:F1}s";
                AppendConsole($"Done. Rebuilt {result.FilesProcessed} bank(s).");
            }
            else
            {
                StatusText = $"Rebuild failed: {result.ErrorMessage}";
                AppendConsole($"ERROR: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            AppendConsole($"EXCEPTION: {ex}");
        }
        finally
        {
            IsBusy = false;
            IsIndeterminate = false;
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────

    private FmodBankConfig BuildConfig() => new()
    {
        BanksPath = BanksPath,
        WavsPath = WavsPath,
        BuildPath = BuildPath,
        Quality = EncodingQuality,
        ThreadCount = ThreadCount,
        ForceOverwrite = ForceOverwrite,
        FsbankclPath = string.IsNullOrWhiteSpace(FsbankclPath) ? null : FsbankclPath
    };

    private void AppendConsole(string line)
    {
        var sb = new StringBuilder(ConsoleOutput);
        if (sb.Length > 0) sb.AppendLine();
        sb.Append(line);
        ConsoleOutput = sb.ToString();
    }

    private static string? PickFolder(string title)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = title };
        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    private static string FormatElapsed(TimeSpan ts) => ts.TotalSeconds switch
    {
        < 1 => $"{ts.TotalMilliseconds:F0}ms",
        < 60 => $"{ts.TotalSeconds:F1}s",
        _ => $"{ts.Minutes}m {ts.Seconds}s"
    };

    private static void EnsureDir(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }
}
