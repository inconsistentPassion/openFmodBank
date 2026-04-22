using System.Diagnostics;
using System.IO;
using System.Text;
using Fmod5Sharp;
using Microsoft.IO;
using OpenFmodBank.Core;

namespace OpenFmodBank.Services;

public sealed class FmodBankService
{
    private static readonly byte[] SndhMagic = Encoding.ASCII.GetBytes("SNDH");
    private static readonly RecyclableMemoryStreamManager MemPool = new();

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

                if (!ExtractFsb(bankFile, fsbPath))
                {
                    progress?.Report(new BankProgress { StatusText = $"Skipping {bankName}.bank — no SNDH header." });
                    continue;
                }

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

            CleanupCache(config.WavsPath);
            GC.Collect();
            sw.Stop();
            return BankOperationResult.Ok(totalFiles, sw.Elapsed);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return BankOperationResult.Fail(ex.Message);
        }
    }

    public BankOperationResult Rebuild(FmodBankConfig config, IProgress<BankProgress>? progress = null)
    {
        var sw = Stopwatch.StartNew();
        int totalFiles = 0;

        try
        {
            ValidatePaths(config.BanksPath, config.WavsPath);

            var fsbankclPath = config.FsbankclPath ?? FindFsbankcl();
            if (fsbankclPath == null || !File.Exists(fsbankclPath))
                return BankOperationResult.Fail("fsbankcl.exe not found. Place in ./FMOD/ or set path in Settings.");

            Directory.CreateDirectory(config.BuildPath);

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

                var originalBankPath = Path.Combine(config.BanksPath, $"{bankName}.bank");
                if (!File.Exists(originalBankPath))
                {
                    progress?.Report(new BankProgress { StatusText = $"Skipping {bankName} — original .bank not found." });
                    continue;
                }

                var fsbDir = Path.Combine(config.WavsPath, ".fsb_cache");
                Directory.CreateDirectory(fsbDir);
                var rebuiltFsbPath = Path.Combine(fsbDir, $"{bankName}.fsb");

                if (!RunFsbankcl(fsbankclPath, lstFile, rebuiltFsbPath, config, progress))
                {
                    progress?.Report(new BankProgress { StatusText = $"fsbankcl failed for {bankName}." });
                    continue;
                }

                var outputPath = Path.Combine(config.BuildPath, $"{bankName}.bank");
                SpliceBank(originalBankPath, rebuiltFsbPath, outputPath);
                totalFiles++;
            }

            CleanupCache(config.WavsPath);
            sw.Stop();
            return BankOperationResult.Ok(totalFiles, sw.Elapsed);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return BankOperationResult.Fail(ex.Message);
        }
    }

    // ── Private ─────────────────────────────────────────────────────

    private static bool ExtractFsb(string bankPath, string fsbOutputPath)
    {
        using var fileStream = File.OpenRead(bankPath);
        using var memStream = MemPool.GetStream();
        fileStream.CopyTo(memStream);
        memStream.Position = 0;

        var headerOffset = BinarySearch.Find(memStream.GetBuffer(), SndhMagic);
        if (headerOffset < 0) return false;

        using var reader = new BinaryReader(memStream, Encoding.ASCII, leaveOpen: true);
        memStream.Position = headerOffset + 12;
        var nextOffset = reader.ReadInt32();
        memStream.Position = nextOffset;

        using var output = File.Create(fsbOutputPath);
        memStream.CopyTo(output);
        return true;
    }

    private static int UnpackFsb(string fsbPath, string outputDir, IProgress<BankProgress>? progress)
    {
        var fsbBytes = File.ReadAllBytes(fsbPath);
        var bank = FsbLoader.LoadFsbFromByteArray(fsbBytes);
        var listFile = new StringBuilder();
        int count = 0;

        foreach (var sample in bank.Samples)
        {
            if (sample is null) continue;
            if (!sample.RebuildAsStandardFileFormat(out var dataBytes, out var fileExtension)) continue;

            var fileName = $"{sample.Name}.{fileExtension}";
            File.WriteAllBytes(Path.Combine(outputDir, fileName), dataBytes);
            listFile.AppendLine(fileName);
            count++;
        }

        File.WriteAllText(Path.Combine(outputDir, "files.lst"), listFile.ToString());
        return count;
    }

    private static void SpliceBank(string originalBankPath, string rebuiltFsbPath, string outputPath)
    {
        using var origStream = MemPool.GetStream();
        using (var f = File.OpenRead(originalBankPath))
            f.CopyTo(origStream);

        origStream.Position = 0;
        var origBuffer = origStream.GetBuffer();
        var headerOffset = BinarySearch.Find(origBuffer, SndhMagic);
        if (headerOffset < 0)
            throw new InvalidOperationException("Original bank missing SNDH header.");

        using var origReader = new BinaryReader(origStream, Encoding.ASCII, leaveOpen: true);
        origStream.Position = headerOffset + 12;
        var headerSize = origReader.ReadInt32();
        var fsbSize = (int)new FileInfo(rebuiltFsbPath).Length;

        if (File.Exists(outputPath)) File.Delete(outputPath);

        using var outStream = File.Create(outputPath);
        using var outWriter = new BinaryWriter(outStream, Encoding.ASCII, leaveOpen: true);

        origStream.Position = 0;
        outStream.Write(origBuffer, 0, headerSize);

        outWriter.Seek(4, SeekOrigin.Begin);
        outWriter.Write(headerSize + fsbSize - 8);

        outWriter.Seek(headerOffset + 16, SeekOrigin.Begin);
        outWriter.Write(fsbSize - (headerSize + 8));

        outWriter.Seek(0, SeekOrigin.End);
        using (var fsbFile = File.OpenRead(rebuiltFsbPath))
            fsbFile.CopyTo(outStream);
    }

    private static bool RunFsbankcl(string path, string lstFile, string outputFsb, FmodBankConfig config, IProgress<BankProgress>? progress)
    {
        var formatArg = config.EncodingFormat switch
        {
            FsbEncodingFormat.PCM => "-format PCM",
            FsbEncodingFormat.FADPCM => "-format FADPCM",
            FsbEncodingFormat.MP3 => "-format MP3",
            FsbEncodingFormat.AAC => "-format AAC",
            _ => "-format Vorbis"
        };

        var args = $"-rebuild -thread_count {config.ResolvedThreadCount} {formatArg} -quality {config.Quality} -ignore_errors -verbosity 5 -o \"{outputFsb}\" \"{lstFile}\"";

        var psi = new ProcessStartInfo
        {
            FileName = path,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null) return false;

        while (!process.StandardOutput.EndOfStream)
        {
            var line = process.StandardOutput.ReadLine();
            if (!string.IsNullOrEmpty(line))
                progress?.Report(new BankProgress { StatusText = line });
        }

        process.WaitForExit();
        return process.ExitCode == 0;
    }

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

    private static void CleanupCache(string wavsPath)
    {
        var cacheDir = Path.Combine(wavsPath, ".fsb_cache");
        if (Directory.Exists(cacheDir))
            Directory.Delete(cacheDir, true);
    }
}
