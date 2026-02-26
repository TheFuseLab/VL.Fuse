using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Compression;
using System.Text;

namespace Fuse.IO.Font;

internal sealed record ProcessRunResult(int ExitCode, string StdOut, string StdErr);

internal static class FontAtlasDebugLog
{
    private static readonly object SyncRoot = new();
    private static readonly string DesktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
    public static readonly string LogFilePath = Path.Combine(DesktopPath, "Fuse.FontAtlas.Debug.log");

    public static void Write(string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}";
        lock (SyncRoot)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogFilePath)!);
            File.AppendAllText(LogFilePath, line);
        }
    }
}

internal static class MsdfAtlasArgumentBuilder
{
    public static string Build(FontAtlasSettings s, string fontPath, string outputPngPath, string outputJsonPath)
    {
        // Note: different msdf-atlas-gen builds can vary in flag names. Keep changes localized here.
        var args = new List<string>
        {
            "-font", Quote(fontPath),
            "-type", s.Type.ToString().ToLowerInvariant(),
            "-dimensions", s.AtlasWidth.ToString(), s.AtlasHeight.ToString(),
            "-pxrange", s.PxRange.ToString(),
            "-imageout", Quote(outputPngPath),
            "-json", Quote(outputJsonPath)
        };

        if (s.Padding > 0)
        {
            args.Add("-pxpadding");
            args.Add(s.Padding.ToString());
        }

        if (!string.IsNullOrWhiteSpace(s.CharsetFilePath))
        {
            args.Add("-charset");
            args.Add(Quote(s.CharsetFilePath));
        }
        else if (!string.IsNullOrWhiteSpace(s.Charset))
        {
            var charsetSpec = BuildCharsetSpec(s.Charset);
            args.Add("-chars");
            args.Add(Quote(charsetSpec));
        }

        if (s.YOrigin != MsdfYOrigin.Auto)
        {
            args.Add("-yorigin");
            args.Add(s.YOrigin == MsdfYOrigin.Bottom ? "bottom" : "top");
        }

        return string.Join(" ", args);
    }

    private static string BuildCharsetSpec(string input)
    {
        var trimmed = input.Trim();
        if (LooksLikeCharsetSpec(trimmed))
            return trimmed;

        // Treat input as a raw character list and convert to a valid charset string literal.
        var sb = new StringBuilder(trimmed.Length + 2);
        sb.Append('"');
        foreach (var ch in trimmed)
        {
            sb.Append(ch switch
            {
                '\\' => "\\\\",
                '"' => "\\\"",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                _ => ch
            });
        }
        sb.Append('"');
        return sb.ToString();
    }

    private static bool LooksLikeCharsetSpec(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (value.StartsWith("[", StringComparison.Ordinal) ||
            value.StartsWith("'", StringComparison.Ordinal) ||
            value.StartsWith("\"", StringComparison.Ordinal))
            return true;

        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return true;

        return char.IsDigit(value[0]);
    }

    private static string Quote(string value) => $"\"{value.Replace("\"", "\\\"")}\"";
}

internal static class MsdfAtlasExeResolver
{
    public const string LatestWindowsX64ZipUrl = "https://github.com/Chlumsky/msdf-atlas-gen/releases/download/v1.3/msdf-atlas-gen-1.3-win64.zip";

    public static async Task<string> ResolveAsync(FontAtlasSettings settings, CancellationToken ct)
    {
        if (TryResolveExecutable(settings.MsdfAtlasGenExePath, out var explicitExe))
        {
            FontAtlasDebugLog.Write($"Resolver: using explicit executable '{explicitExe}'.");
            return explicitExe;
        }

        foreach (var candidate in GetDefaultExecutableCandidates())
        {
            if (File.Exists(candidate))
            {
                FontAtlasDebugLog.Write($"Resolver: found bundled executable '{candidate}'.");
                return candidate;
            }
        }

        if (!settings.AutoInstallFromGitHub)
            throw new FileNotFoundException("msdf-atlas-gen executable not found. Set MsdfAtlasGenExePath or provide a bundled tool under Tools/msdf-atlas-gen.");

        var zipUrl = string.IsNullOrWhiteSpace(settings.GitHubReleaseZipUrl)
            ? LatestWindowsX64ZipUrl
            : settings.GitHubReleaseZipUrl;

        var installRoot = GetBundledToolRoot();
        Directory.CreateDirectory(installRoot);
        FontAtlasDebugLog.Write($"Resolver: executable missing, auto-install enabled. Download URL: '{zipUrl}'. Install root: '{installRoot}'.");
        await MsdfAtlasGitHubInstaller.InstallFromZipAsync(zipUrl, installRoot, ct);

        foreach (var candidate in GetDefaultExecutableCandidates())
        {
            if (File.Exists(candidate))
            {
                FontAtlasDebugLog.Write($"Resolver: executable available after install '{candidate}'.");
                return candidate;
            }
        }

        throw new FileNotFoundException("Downloaded msdf-atlas-gen archive but no executable was found after extraction.");
    }

    private static bool TryResolveExecutable(string path, out string resolved)
    {
        resolved = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var full = Path.GetFullPath(path);
        if (!File.Exists(full))
            return false;

        resolved = full;
        return true;
    }

    private static IEnumerable<string> GetDefaultExecutableCandidates()
    {
        var baseDir = AppContext.BaseDirectory;
        var assemblyDir = Path.GetDirectoryName(typeof(MsdfAtlasExeResolver).Assembly.Location) ?? baseDir;
        var cwd = Directory.GetCurrentDirectory();

        foreach (var root in new[] { baseDir, assemblyDir, cwd })
        {
            yield return Path.Combine(root, "Tools", "msdf-atlas-gen", "msdf-atlas-gen.exe");
            yield return Path.Combine(root, "Font", "Tools", "msdf-atlas-gen", "msdf-atlas-gen.exe");
        }

        yield return Path.Combine(GetBundledToolRoot(), "msdf-atlas-gen.exe");
    }

    public static string GetPreferredDefaultExecutablePath()
    {
        foreach (var candidate in GetDefaultExecutableCandidates())
        {
            if (File.Exists(candidate))
                return candidate;
        }

        return string.Empty;
    }

    private static string GetBundledToolRoot()
    {
        var assemblyDir = Path.GetDirectoryName(typeof(MsdfAtlasExeResolver).Assembly.Location) ?? AppContext.BaseDirectory;
        return Path.Combine(assemblyDir, "Tools", "msdf-atlas-gen");
    }
}

internal static class MsdfAtlasGitHubInstaller
{
    public static async Task InstallFromZipAsync(string zipUrl, string installDirectory, CancellationToken ct)
    {
        var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var tempZip = Path.Combine(Path.GetTempPath(), $"msdf-atlas-gen-{stamp}.zip");
        var extractDir = Path.Combine(Path.GetTempPath(), $"msdf-atlas-gen-{stamp}");

        try
        {
            FontAtlasDebugLog.Write($"Installer: downloading '{zipUrl}' to '{tempZip}'.");
            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Fuse.IO FontAtlas Installer");
            await using (var remote = await client.GetStreamAsync(zipUrl, ct))
            await using (var local = File.Create(tempZip))
                await remote.CopyToAsync(local, ct);

            Directory.CreateDirectory(extractDir);
            ZipFile.ExtractToDirectory(tempZip, extractDir, overwriteFiles: true);
            FontAtlasDebugLog.Write($"Installer: extracted archive to '{extractDir}'.");

            var exePath = Directory
                .GetFiles(extractDir, "msdf-atlas-gen.exe", SearchOption.AllDirectories)
                .FirstOrDefault();

            if (exePath == null)
                throw new FileNotFoundException("Archive does not contain msdf-atlas-gen.exe.");

            Directory.CreateDirectory(installDirectory);
            foreach (var file in Directory.GetFiles(Path.GetDirectoryName(exePath)!, "*", SearchOption.TopDirectoryOnly))
                File.Copy(file, Path.Combine(installDirectory, Path.GetFileName(file)), overwrite: true);
            FontAtlasDebugLog.Write($"Installer: copied executable directory to '{installDirectory}'.");
        }
        finally
        {
            TryDeleteFile(tempZip);
            TryDeleteDir(extractDir);
        }
    }

    private static void TryDeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    private static void TryDeleteDir(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); } catch { }
    }
}

internal static class ProcessRunner
{
    public static async Task<ProcessRunResult> RunAsync(string exePath, string arguments, CancellationToken ct)
    {
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        var stdoutTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stderrTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null) stdoutTcs.TrySetResult();
            else stdout.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data == null) stderrTcs.TrySetResult();
            else stderr.AppendLine(e.Data);
        };

        if (!process.Start())
            throw new InvalidOperationException($"Failed to start process '{exePath}'.");

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var reg = ct.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch
            {
                // ignore
            }
        });

        await process.WaitForExitAsync(ct);
        await Task.WhenAll(stdoutTcs.Task, stderrTcs.Task);

        return new ProcessRunResult(process.ExitCode, stdout.ToString(), stderr.ToString());
    }
}

internal static class FontAtlasGenerator
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> KeyLocks = new();

    public static async Task<FontAtlasResult> GenerateAsync(FontAtlasSettings settings, CancellationToken ct)
    {
        FontAtlasDebugLog.Write($"Generate: start. Font='{settings.FontFilePath}', Type={settings.Type}, Size={settings.AtlasWidth}x{settings.AtlasHeight}, PxRange={settings.PxRange}, Padding={settings.Padding}, ForceRebuild={settings.ForceRebuild}, OutputDir='{settings.OutputDirectory}'.");
        Validate(settings);
        var paths = FontAtlasCache.BuildPaths(settings);
        FontAtlasDebugLog.Write($"Generate: paths resolved. CacheKey='{paths.CacheKey}', PNG='{paths.AtlasPngPath}', JSON='{paths.AtlasJsonPath}'.");

        var gate = KeyLocks.GetOrAdd(paths.CacheKey, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            if (!settings.ForceRebuild && FontAtlasCache.IsValidCacheHit(paths))
            {
                FontAtlasDebugLog.Write("Generate: cache hit. Skipping external process.");
                return new FontAtlasResult
                {
                    AtlasPngPath = paths.AtlasPngPath,
                    AtlasJsonPath = paths.AtlasJsonPath,
                    CacheKey = paths.CacheKey,
                    Succeeded = true,
                    ExitCode = 0,
                    WasCacheHit = true
                };
            }

            var exePath = await MsdfAtlasExeResolver.ResolveAsync(settings, ct);
            var tempPng = paths.AtlasPngPath + ".tmp";
            var tempJson = paths.AtlasJsonPath + ".tmp";
            CleanupTemp(tempPng, tempJson);

            var args = MsdfAtlasArgumentBuilder.Build(settings, Path.GetFullPath(settings.FontFilePath), tempPng, tempJson);
            FontAtlasDebugLog.Write($"Generate: executing '{exePath}' {args}");
            var run = await ProcessRunner.RunAsync(exePath, args, ct);
            FontAtlasDebugLog.Write($"Generate: process finished with ExitCode={run.ExitCode}. StdOut length={run.StdOut.Length}, StdErr length={run.StdErr.Length}.");

            if (run.ExitCode != 0)
            {
                CleanupTemp(tempPng, tempJson);
                FontAtlasDebugLog.Write($"Generate: process failed. StdOut='{run.StdOut}', StdErr='{run.StdErr}'.");
                return new FontAtlasResult
                {
                    AtlasPngPath = paths.AtlasPngPath,
                    AtlasJsonPath = paths.AtlasJsonPath,
                    CacheKey = paths.CacheKey,
                    ExitCode = run.ExitCode,
                    StdOut = run.StdOut,
                    StdErr = run.StdErr,
                    ErrorMessage = "msdf-atlas-gen returned a non-zero exit code."
                };
            }

            if (!File.Exists(tempPng) || !File.Exists(tempJson))
                throw new InvalidOperationException("msdf-atlas-gen did not produce expected PNG and JSON outputs.");

            MoveReplace(tempPng, paths.AtlasPngPath);
            MoveReplace(tempJson, paths.AtlasJsonPath);
            FontAtlasDebugLog.Write($"Generate: success. Final PNG='{paths.AtlasPngPath}', JSON='{paths.AtlasJsonPath}'.");

            return new FontAtlasResult
            {
                AtlasPngPath = paths.AtlasPngPath,
                AtlasJsonPath = paths.AtlasJsonPath,
                CacheKey = paths.CacheKey,
                Succeeded = true,
                ExitCode = run.ExitCode,
                StdOut = run.StdOut,
                StdErr = run.StdErr
            };
        }
        catch (OperationCanceledException)
        {
            FontAtlasDebugLog.Write("Generate: canceled.");
            return new FontAtlasResult
            {
                CacheKey = paths.CacheKey,
                AtlasPngPath = paths.AtlasPngPath,
                AtlasJsonPath = paths.AtlasJsonPath,
                ErrorMessage = "Generation canceled."
            };
        }
        catch (Exception ex)
        {
            FontAtlasDebugLog.Write($"Generate: exception '{ex}'.");
            return new FontAtlasResult
            {
                CacheKey = paths.CacheKey,
                AtlasPngPath = paths.AtlasPngPath,
                AtlasJsonPath = paths.AtlasJsonPath,
                ErrorMessage = ex.Message
            };
        }
        finally
        {
            gate.Release();
            FontAtlasDebugLog.Write("Generate: end.");
        }
    }

    private static void Validate(FontAtlasSettings s)
    {
        if (string.IsNullOrWhiteSpace(s.FontFilePath))
            throw new ArgumentException("FontFilePath is empty.");
        if (!File.Exists(s.FontFilePath))
            throw new FileNotFoundException($"Font file not found: {s.FontFilePath}");
        if (s.AtlasWidth <= 0 || s.AtlasHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(s), "Atlas dimensions must be > 0.");
        if (s.PxRange <= 0)
            throw new ArgumentOutOfRangeException(nameof(s), "PxRange must be > 0.");
    }

    private static void MoveReplace(string source, string target)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Move(source, target, overwrite: true);
    }

    private static void CleanupTemp(params string[] tempPaths)
    {
        foreach (var p in tempPaths)
        {
            try
            {
                if (File.Exists(p))
                    File.Delete(p);
            }
            catch
            {
                // best effort only
            }
        }
    }
}
