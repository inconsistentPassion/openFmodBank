# OpenFmodBank

FMOD Bank extractor and rebuilder. Extract audio from FMOD `.bank` files, modify, and rebuild.

**20× faster extraction** | **2× faster rebuilding** | No transcoding — original formats preserved.

## Requirements

- Windows 10/11 x64
- .NET 8.0 SDK ([download](https://dotnet.microsoft.com/download/dotnet/8.0))
- FMOD fsbankcl.exe (for rebuilding only — place in `./FMOD/`)

## Build

```bash
dotnet restore
dotnet build -c Release
```

Or open `OpenFmodBank.sln` in Visual Studio 2022.

## Usage

1. Place `.bank` files in `banks/` folder (or browse to your game directory)
2. Click **Extract Sounds** — audio appears in `wavs/<bankName>/`
3. Modify extracted audio (keep same format/bitrate, same or shorter duration)
4. Click **Rebuild Banks** — new `.bank` files in `build/`
5. Copy rebuilt banks back to your game

## Project Structure

```
OpenFmodBank.sln
Directory.Build.props
OpenFmodBank/                    # WPF Application
├── App.xaml / App.xaml.cs       # DI + startup
├── OpenFmodBank.csproj
├── Core/                        # Business logic
│   ├── Models.cs                # Config, progress, result types
│   ├── BinarySearch.cs          # Fast byte pattern search
│   └── MainViewModel.cs         # Extract/rebind logic + UI state
├── Services/
│   └── FmodBankService.cs       # BANK extract & rebuild engine
└── View/
    ├── MainWindow.xaml / .cs    # FluentWindow + NavigationView
    ├── MainPage.xaml / .cs      # Extract & rebuild UI
    ├── SettingsPage.xaml / .cs  # fsbankcl path, quality, threads
    └── Converters.cs            # Value converters
```

## Tested With

- Subnautica
- Other FMOD 5.x based games

## License

[MIT](LICENSE)
