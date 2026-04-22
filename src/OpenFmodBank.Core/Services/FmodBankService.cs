using System.Diagnostics;
using System.Text;
using Fmod5Sharp;
using Fmod5Sharp.FmodTypes;
using Microsoft.IO;
using OpenFmodBank.Core.Models;

namespace OpenFmodBank.Core.Services;

/// <summary>
/// Core service for FMOD Bank extraction and rebuild operations.
/// All I/O and CPU-intensive work lives here — no UI dependencies.
/// </summary>
public sealed class FmodBankService
{
    private static readonly byte[] SndhMagic = Encoding.ASCII.GetBytes("SNDH");
    private static readonly RecyclableMemoryStreamManager MemPool = new();

    /// <summary>
    /// Extract audio from .bank files in <paramref name="config"/>.BanksPath.
    /// </summary>
    public BankOperationResult Extract(FmodBankConfig config, IProgress<BankProgress>? progress = null)
    {
        var sw = Stopwatch.StartNew();
        int totalFiles = 0;

        try
        {
            ValidatePaths(config.BanksPath, config.WavsPath);

            var bankFiles = Directory.EnumerateFiles(config.BanksPath, "*.bank").ToList();
            if (bankFiles.Count == 0)
                return BankOperationResult.Fail("No .bank files found in the banks directory.");

            foreach (var bankFile in bankFiles)
            {
                var bankName = Path.GetFileNameWithoutExtension(bankFile);
                var wavDir = Path.Combine(config.WavsPath, bankName);
                var fsbTempDir = Path.Combine(config.WavsPath, ".fsb_cache");

                progress?.Report(new BankProgress
                {
                    StatusText = $"Extracting: {bankName}.bank",
                    Current = totalFiles,
                    Maximum = bankFiles.Count,
                    CurrentFile = bankFile
                });

                Directory.CreateDirectory(fsbTempDir);
                var fsbPath = Path.Combine(fsbTempDir, $"{bankName}.fsb");

                // Step 1: Extract FSB from BANK
                if (!ExtractFsb(bankFile, fsbPath))
                {
                    LogWarning(progress, $"Skipping {bankName}.bank — SNDH header not found.");
                    continue;
                }

                // Step 2: Unpack FSB samples
                Directory.CreateDirectory(wavDir);
                int sampleCount = UnpackFsb(fsbPath, wavDir, progress);
                totalFiles++;

                progress?.Report(new BankProgress
                {
                    StatusText = $"Done: {bankName}.bank ({sampleCount} samples)",
                    Current = totalFiles,
                    Maximum = bankFiles.Count
                });
            }

            // Clean up FSB cache
            var cacheDir = Path.Combine(config.WavsPath, ".fsb_cache");
            if (Directory.Exists(cacheDir))
                Directory.Delete(cacheDir, true);

            sw.Stop();
            GC.Collect();
            return BankOperationResult.Ok(totalFiles, sw.Elapsed);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return BankOperationResult.Fail(ex.Message);
        }
    }

    /// <summary>
    /// Rebuild .bank files from previously extracted audio directories.
    /// Requires FMOD fsbankcl.exe at the configured path.
    /// </summary>
    public BankOperationResult Rebuild(FmodBankConfig config, IProgress<BankProgress>? progress = null)
    {
        var sw = Stopwatch.StartNew();
        int totalFiles = 0;

        try
        {
            ValidatePaths(config.BanksPath, config.WavsPath);

            // Resolve fsbankcl path
            var fsbankclPath = config.FsbankclPath
                ?? FindFsbankcl();
            if (fsbankclPath == null || !File.Exists(fsbankclPath))
                return BankOperationResult.Fail(
                    $"fsbankcl.exe not found. Set FsbankclPath in config or place it at ./FMOD/fsbankcl.exe");

            Directory.CreateDirectory(config.BuildPath);

            // Find directories containing files.lst
            var wavDirs = Directory.EnumerateDirectories(config.WavsPath)
                .Where(d => File.Exists(Path.Combine(d, "files.lst")))
                .ToList();

            if (wavDirs.Count == 0)
                return BankOperationResult.Fail("No extracted sound folders found (missing files.lst).");

            foreach (var wavDir in wavDirs)
            {
                var bankName = Path.GetFileName(wavDir);
                var lstFile = Path.Combine(wavDir, "files.lst");

                progress?.Report(new BankProgress
                {
                    StatusText = $"Building: {bankName}.bank",
                    Current = totalFiles,
                    Maximum = wavDirs.Count,
                    CurrentFile = wavDir
                });

                // Find original bank to reuse its header
                var originalBankPath = Path.Combine(config.BanksPath, $"{bankName}.bank");
                if (!File.Exists(originalBankPath))
                {
                    LogWarning(progress, $"Skipping {bankName} — original .bank not found.");
                    continue;
                }

                // Step 1: Rebuild FSB using fsbankcl
                var fsbDir = Path.Combine(config.WavsPath, ".fsb_cache");
                Directory.CreateDirectory(fsbDir);
                var rebuiltFsbPath = Path.Combine(fsbDir, $"{bankName}.fsb");

                if (!RunFsbankcl(fsbankclPath, lstFile, rebuiltFsbPath, config, progress))
                {
                    LogWarning(progress, $"fsbankcl failed for {bankName}.bank");
                    continue;
                }

                // Step 2: Splice rebuilt FSB into original BANK header
                var outputPath = Path.Combine(config.BuildPath, $"{bankName}.bank");
                SpliceBank(originalBankPath, rebuiltFsbPath, outputPath);

                totalFiles++;
            }

            // Clean up
            var cacheDir = Path.Combine(config.WavsPath, ".fsb_cache");
            if (Directory.Exists(cacheDir))
                Directory.Delete(cacheDir, true);

            sw.Stop();
            return BankOperationResult.Ok(totalFiles, sw.Elapsed);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return BankOperationResult.Fail(ex.Message);
        }
    }

    // ── Private helpers ──────────────────────────────────────────────

    /// <summary>
    /// Extract FSB data from a BANK file by locating the SNDH header.
    /// </summary>
    private static bool ExtractFsb(string bankPath, string fsbOutputPath)
    {
        using var fileStream = File.OpenRead(bankPath);
        using var memStream = MemPool.GetStream();
        fileStream.CopyTo(memStream);
        memStream.Position = 0;

        var buffer = memStream.GetBuffer();
        var headerOffset = BinarySearch.Find(buffer, SndhMagic);

        if (headerOffset < 0)
            return false;

        using var reader = new BinaryReader(memStream, Encoding.ASCII, leaveOpen: true);

        // SNDH header: magic(4) + unknown(8) + nextOffset(4) + ...
        memStream.Position = headerOffset + 12;
        var nextOffset = reader.ReadInt32();

        // Extract from nextOffset to end
        memStream.Position = nextOffset;

        using var output = File.Create(fsbOutputPath);
        memStream.CopyTo(output);

        return true;
    }

    /// <summary>
    /// Unpack all samples from an FSB file to the output directory.
    /// Returns the number of samples extracted.
    /// </summary>
    private static int UnpackFsb(string fsbPath, string outputDir, IProgress<BankProgress>? progress)
    {
        var fsbBytes = File.ReadAllBytes(fsbPath);
        var bank = FsbLoader.LoadFsbFromByteArray(fsbBytes);
        var listFile = new StringBuilder();
        int count = 0;

        foreach (var sample in bank.Samples)
        {
            if (sample is null)
                continue;

            if (!sample.RebuildAsStandardFileFormat(out var dataBytes, out var fileExtension))
                continue;

            var fileName = $"{sample.Name}.{fileExtension}";
            var outputPath = Path.Combine(outputDir, fileName);

            File.WriteAllBytes(outputPath, dataBytes);
            listFile.AppendLine(fileName);
            count++;
        }

        // Write manifest
        var lstPath = Path.Combine(outputDir, "files.lst");
        File.WriteAllText(lstPath, listFile.ToString());

        return count;
    }

    /// <summary>
    /// Splice a rebuilt FSB into an original BANK file, preserving the original header.
    /// </summary>
    private static void SpliceBank(string originalBankPath, string rebuiltFsbPath, string outputPath)
    {
        // Read original bank
        using var origStream = MemPool.GetStream();
        using (var origFile = File.OpenRead(originalBankPath))
            origFile.CopyTo(origStream);

        origStream.Position = 0;
        var origBuffer = origStream.GetBuffer();

        var headerOffset = BinarySearch.Find(origBuffer, SndhMagic);
        if (headerOffset < 0)
            throw new InvalidOperationException("Original bank missing SNDH header.");

        using var origReader = new BinaryReader(origStream, Encoding.ASCII, leaveOpen: true);
        origStream.Position = headerOffset + 12;
        var headerSize = origReader.ReadInt32();

        var fsbSize = (int)new FileInfo(rebuiltFsbPath).Length;

        // Write new bank
        if (File.Exists(outputPath))
            File.Delete(outputPath);

        using var outStream = File.Create(outputPath);
        using var outWriter = new BinaryWriter(outStream, Encoding.ASCII, leaveOpen: true);

        // Copy original header
        origStream.Position = 0;
        outStream.Write(origBuffer, 0, headerSize);

        // Patch: update total file size at offset 4
        outWriter.Seek(4, SeekOrigin.Begin);
        outWriter.Write(headerSize + fsbSize - 8);

        // Patch: update FSB offset at SNDH+16
        outWriter.Seek(headerOffset + 16, SeekOrigin.Begin);
        outWriter.Write(fsbSize - (headerSize + 8));

        // Append new FSB
        outWriter.Seek(0, SeekOrigin.End);
        using (var fsbFile = File.OpenRead(rebuiltFsbPath))
            fsbFile.CopyTo(outStream);
    }

    /// <summary>
    /// Run fsbankcl.exe to rebuild an FSB from extracted audio files.
    /// </summary>
    private static bool RunFsbankcl(
        string fsbankclPath,
        string lstFile,
        string outputFsb,
        FmodBankConfig config,
        IProgress<BankProgress>? progress)
    {
        var formatArg = config.EncodingFormat switch
        {
            FsbEncodingFormat.PCM => "-format PCM",
            FsbEncodingFormat.FADPCM => "-format FADPCM",
            FsbEncodingFormat.MP3 => "-format MP3",
            FsbEncodingFormat.AAC => "-format AAC",
            _ => "-format Vorbis"
        };

        var args = string.Join(" ",
            "-rebuild",
            $"-thread_count {config.ResolvedThreadCount}",
            formatArg,
            $"-quality {config.Quality}",
            "-ignore_errors",
            "-verbosity 5",
            $"-o \"{outputFsb}\"",
            $"\"{lstFile}\"");

        var psi = new ProcessStartInfo
        {
            FileName = fsbankclPath,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null)
            return false;

        // Stream output to progress
        while (!process.StandardOutput.EndOfStream)
        {
            var line = process.StandardOutput.ReadLine();
            if (!string.IsNullOrEmpty(line))
                progress?.Report(new BankProgress { StatusText = line });
        }

        process.WaitForExit();
        return process.ExitCode == 0;
    }

    /// <summary>
    /// Auto-locate fsbankcl.exe in common locations.
    /// </summary>
    private static string? FindFsbankcl()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "FMOD", "fsbankcl.exe"),
            Path.Combine(Directory.GetCurrentDirectory(), "FMOD", "fsbankcl.exe"),
            Path.Combine(Directory.GetCurrentDirectory(), "fsbankcl.exe"),
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private static void ValidatePaths(string banksPath, string wavsPath)
    {
        if (!Directory.Exists(banksPath))
            throw new DirectoryNotFoundException($"Banks directory not found: {banksPath}");

        if (!Directory.Exists(wavsPath))
            Directory.CreateDirectory(wavsPath);
    }

    private static void LogWarning(IProgress<BankProgress>? progress, string message)
    {
        progress?.Report(new BankProgress { StatusText = $"⚠ {message}" });
    }
}
