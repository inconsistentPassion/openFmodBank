namespace OpenFmodBank.Core;

public enum FsbEncodingFormat
{
    Vorbis,
    PCM,
    FADPCM,
    MP3,
    AAC
}

public sealed class FmodBankConfig
{
    public required string BanksPath { get; init; }
    public required string WavsPath { get; init; }
    public required string BuildPath { get; init; }
    public string? FsbankclPath { get; init; }
    public FsbEncodingFormat EncodingFormat { get; init; } = FsbEncodingFormat.Vorbis;
    public int Quality { get; init; } = 85;
    public int ThreadCount { get; init; } = 0;
    public bool ForceOverwrite { get; init; } = false;

    public int ResolvedThreadCount => ThreadCount > 0 ? ThreadCount : Environment.ProcessorCount;
}

public sealed class BankProgress
{
    public string StatusText { get; init; } = "Idle";
    public int Current { get; init; }
    public int Maximum { get; init; }
    public string? CurrentFile { get; init; }
}

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
