namespace OpenFmodBank.Core.Models;

/// <summary>
/// Encoding format for FSB rebuild.
/// </summary>
public enum FsbEncodingFormat
{
    Vorbis,
    PCM,
    FADPCM,
    MP3,
    AAC
}

/// <summary>
/// Configuration for bank extraction and rebuild operations.
/// </summary>
public sealed class FmodBankConfig
{
    /// <summary>Path to the directory containing .bank files.</summary>
    public required string BanksPath { get; init; }

    /// <summary>Path to output extracted audio files.</summary>
    public required string WavsPath { get; init; }

    /// <summary>Path to output rebuilt .bank files.</summary>
    public required string BuildPath { get; init; }

    /// <summary>Path to the FMOD fsbankcl.exe tool. Auto-detected if null.</summary>
    public string? FsbankclPath { get; init; }

    /// <summary>Encoding format for rebuild (default: Vorbis).</summary>
    public FsbEncodingFormat EncodingFormat { get; init; } = FsbEncodingFormat.Vorbis;

    /// <summary>Encoding quality 1-100 (default: 85).</summary>
    public int Quality { get; init; } = 85;

    /// <summary>Number of threads for rebuild. 0 = auto (CPU count).</summary>
    public int ThreadCount { get; init; } = 0;

    /// <summary>Whether to overwrite existing files without prompting.</summary>
    public bool ForceOverwrite { get; init; } = false;

    /// <summary>Resolved thread count.</summary>
    public int ResolvedThreadCount => ThreadCount > 0 ? ThreadCount : Environment.ProcessorCount;
}

/// <summary>
/// Progress reporting for long operations.
/// </summary>
public sealed class BankProgress
{
    public string StatusText { get; init; } = "Idle";
    public int Current { get; init; }
    public int Maximum { get; init; }
    public string? CurrentFile { get; init; }
}

/// <summary>
/// Result of an extraction or rebuild operation.
/// </summary>
public sealed class BankOperationResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public int FilesProcessed { get; init; }
    public TimeSpan Elapsed { get; init; }

    public static BankOperationResult Ok(int files, TimeSpan elapsed) => new()
    {
        Success = true,
        FilesProcessed = files,
        Elapsed = elapsed
    };

    public static BankOperationResult Fail(string error) => new()
    {
        Success = false,
        ErrorMessage = error
    };
}

/// <summary>
/// Information about a single extracted sound.
/// </summary>
public sealed class SoundSampleInfo
{
    public required string Name { get; init; }
    public required string Extension { get; init; }
    public required long SizeBytes { get; init; }

    public string FileName => $"{Name}.{Extension}";
}
