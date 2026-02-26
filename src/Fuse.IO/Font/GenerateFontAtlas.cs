namespace Fuse.IO.Font;

#pragma warning disable CS1591

[ProcessNode]
public sealed class GenerateFontAtlas : IDisposable
{
    private const string PrintableAsciiSpec = "[0x20, 0x7E]";
    private CancellationTokenSource? _runCts;
    private bool _isBusy;
    private string _lastRequestedKey = string.Empty;
    private bool _lastForceRebuild;

    public GenerateFontAtlas()
    {
        _runCts = new CancellationTokenSource();
    }

    // Outputs
    public string AtlasPngPath { get; private set; } = string.Empty;
    public string AtlasJsonPath { get; private set; } = string.Empty;
    public string CacheKey { get; private set; } = string.Empty;
    public bool IsBusy { get; private set; }
    public bool Succeeded { get; private set; }
    public int ExitCode { get; private set; } = -1;
    public string StdOut { get; private set; } = string.Empty;
    public string StdErr { get; private set; } = string.Empty;
    public string ErrorMessage { get; private set; } = string.Empty;
    public string LogFilePath { get; private set; } = FontAtlasDebugLog.LogFilePath;
    public FontAtlasOutputPaths Paths { get; private set; }

    public GenerateFontAtlas Output => this;

    public void Update(
        string fontFilePath = "",
        string charset = PrintableAsciiSpec,
        string charsetFilePath = "",
        int atlasWidth = 1024,
        int atlasHeight = 1024,
        int pxRange = 8,
        MsdfAtlasType type = MsdfAtlasType.Mtsdf,
        int padding = 1,
        MsdfYOrigin yOrigin = MsdfYOrigin.Auto,
        string outputDirectory = "",
        string msdfAtlasGenExePath = "",
        bool forceRebuild = false,
        bool enabled = true,
        bool autoInstallFromGitHub = false,
        string gitHubReleaseZipUrl = MsdfAtlasExeResolver.LatestWindowsX64ZipUrl)
    {
        if (!enabled || _isBusy || string.IsNullOrWhiteSpace(fontFilePath))
            return;

        FontAtlasSettings settings;
        try
        {
            settings = BuildSettings(
                fontFilePath,
                charset,
                charsetFilePath,
                atlasWidth,
                atlasHeight,
                pxRange,
                type,
                padding,
                yOrigin,
                outputDirectory,
                msdfAtlasGenExePath,
                forceRebuild,
                autoInstallFromGitHub,
                gitHubReleaseZipUrl);
        }
        catch (Exception ex)
        {
            Succeeded = false;
            ErrorMessage = ex.Message;
            return;
        }

        var paths = FontAtlasCache.BuildPaths(settings);
        Paths = new FontAtlasOutputPaths(paths.AtlasJsonPath, paths.AtlasPngPath);
        var forceEdge = forceRebuild && !_lastForceRebuild;
        _lastForceRebuild = forceRebuild;
        if (!forceEdge && string.Equals(paths.CacheKey, _lastRequestedKey, StringComparison.Ordinal))
            return;

        _lastRequestedKey = paths.CacheKey;
        StartGenerate(settings);
    }

    public void Dispose()
    {
        _runCts?.Cancel();
        _runCts?.Dispose();
        _runCts = null;
    }

    private static FontAtlasSettings BuildSettings(
        string fontFilePath,
        string charset,
        string charsetFilePath,
        int atlasWidth,
        int atlasHeight,
        int pxRange,
        MsdfAtlasType type,
        int padding,
        MsdfYOrigin yOrigin,
        string outputDirectory,
        string msdfAtlasGenExePath,
        bool forceRebuild,
        bool autoInstallFromGitHub,
        string gitHubReleaseZipUrl)
    {
        var resolvedFontPath = Path.GetFullPath(fontFilePath.Trim());
        var resolvedOutputDirectory = string.IsNullOrWhiteSpace(outputDirectory)
            ? Path.Combine(Path.GetDirectoryName(resolvedFontPath) ?? AppContext.BaseDirectory, "msdf")
            : Path.GetFullPath(outputDirectory.Trim());

        return new FontAtlasSettings
        {
            FontFilePath = resolvedFontPath,
            Charset = charset ?? string.Empty,
            CharsetFilePath = string.IsNullOrWhiteSpace(charsetFilePath) ? string.Empty : Path.GetFullPath(charsetFilePath.Trim()),
            AtlasWidth = atlasWidth,
            AtlasHeight = atlasHeight,
            PxRange = pxRange,
            Type = type,
            Padding = padding,
            YOrigin = yOrigin,
            OutputDirectory = resolvedOutputDirectory,
            MsdfAtlasGenExePath = string.IsNullOrWhiteSpace(msdfAtlasGenExePath)
                ? MsdfAtlasExeResolver.GetPreferredDefaultExecutablePath()
                : Path.GetFullPath(msdfAtlasGenExePath.Trim()),
            ForceRebuild = forceRebuild,
            AutoInstallFromGitHub = autoInstallFromGitHub,
            GitHubReleaseZipUrl = gitHubReleaseZipUrl ?? string.Empty
        };
    }

    private async void StartGenerate(FontAtlasSettings settings)
    {
        _runCts?.Cancel();
        _runCts?.Dispose();
        _runCts = new CancellationTokenSource();

        _isBusy = true;
        IsBusy = true;
        Succeeded = false;
        ErrorMessage = string.Empty;
        StdOut = string.Empty;
        StdErr = string.Empty;
        ExitCode = -1;

        try
        {
            var result = await FontAtlasGenerator.GenerateAsync(settings, _runCts.Token);
            AtlasPngPath = result.AtlasPngPath;
            AtlasJsonPath = result.AtlasJsonPath;
            Paths = new FontAtlasOutputPaths(AtlasJsonPath, AtlasPngPath);
            CacheKey = result.CacheKey;
            Succeeded = result.Succeeded;
            ExitCode = result.ExitCode;
            StdOut = result.StdOut;
            StdErr = result.StdErr;
            ErrorMessage = result.ErrorMessage;
        }
        catch (Exception ex)
        {
            Succeeded = false;
            ErrorMessage = ex.Message;
        }
        finally
        {
            _isBusy = false;
            IsBusy = false;
        }
    }
}
