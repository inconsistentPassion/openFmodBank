# OpenFmodBank

An open-source FMOD Bank extractor and rebuilder. Extract audio files from FMOD `.bank` files, modify them, and rebuild new banks.

**20× faster extraction** | **2× faster rebuilding** | No transcoding — original formats preserved.

## Features

- Extract sounds from FMOD `.bank` files to their original format (no WAV conversion)
- Rebuild modified banks using `fsbankcl.exe` (FMOD Studio tools)
- Modern Fluent UI (WPF UI 3.4)
- Configurable encoding format and quality
- Clean MVVM architecture with full async support

## Requirements

- **Windows** (WPF application)
- **.NET 8.0 Runtime** — [download](https://dotnet.microsoft.com/download/dotnet/8.0)
- **FMOD fsbankcl.exe** — place in `./FMOD/` folder (only needed for rebuilding)

## Build

```bash
dotnet restore
dotnet build -c Release
```

## Usage

1. **Place `.bank` files** in the `banks/` folder (or browse to your game's bank directory)
2. Click **Extract Sounds** — audio files appear in `wavs/<bankName>/`
3. **Modify** any extracted audio files (keep same format/bitrate, same or shorter duration)
4. Click **Rebuild Banks** — new `.bank` files appear in `build/`
5. Copy rebuilt banks back to your game directory

## Project Structure

```
OpenFmodBank/
├── OpenFmodBank.sln
├── LICENSE
└── src/
    ├── OpenFmodBank.Core/          # Core logic (no UI dependency)
    │   ├── Models/                 # Config, progress, result types
    │   └── Services/
    │       ├── FmodBankService.cs  # Extract & rebuild engine
    │       └── BinarySearch.cs     # Fast byte pattern search
    └── OpenFmodBank.App/           # WPF UI application
        ├── ViewModels/             # MVVM view models
        ├── Views/                  # XAML views
        └── Services/               # DI service registrations
```

## Tested With

- Subnautica
- Other FMOD 5.x based games

## License

[MIT](LICENSE)
