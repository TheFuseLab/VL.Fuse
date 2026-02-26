using System.Security.Cryptography;
using System.Text;

namespace Fuse.IO.Font;

#pragma warning disable CS1591

public enum MsdfAtlasType
{
    Sdf,
    Msdf,
    Mtsdf
}

public enum MsdfYOrigin
{
    Auto,
    Top,
    Bottom
}

public sealed record FontAtlasSettings
{
    public string FontFilePath { get; init; } = string.Empty;
    public string Charset { get; init; } = string.Empty;
    public string CharsetFilePath { get; init; } = string.Empty;
    public int AtlasWidth { get; init; } = 1024;
    public int AtlasHeight { get; init; } = 1024;
    public int PxRange { get; init; } = 4;
    public MsdfAtlasType Type { get; init; } = MsdfAtlasType.Msdf;
    public int Padding { get; init; } = 0;
    public MsdfYOrigin YOrigin { get; init; } = MsdfYOrigin.Auto;
    public string OutputDirectory { get; init; } = string.Empty;
    public string MsdfAtlasGenExePath { get; init; } = string.Empty;
    public bool ForceRebuild { get; init; }
    public bool AutoInstallFromGitHub { get; init; }
    public string GitHubReleaseZipUrl { get; init; } = string.Empty;
}

public sealed record FontAtlasPaths(string AtlasPngPath, string AtlasJsonPath, string BaseName, string CacheKey);

public sealed record FontAtlasResult
{
    public string AtlasPngPath { get; init; } = string.Empty;
    public string AtlasJsonPath { get; init; } = string.Empty;
    public string CacheKey { get; init; } = string.Empty;
    public bool IsBusy { get; init; }
    public bool Succeeded { get; init; }
    public int ExitCode { get; init; } = -1;
    public bool WasCacheHit { get; init; }
    public string StdOut { get; init; } = string.Empty;
    public string StdErr { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
}

public readonly record struct FontAtlasOutputPaths(string AtlasPath, string ImagePath);

internal static class FontAtlasCache
{
    public static string BuildFontIdentity(string fontPath)
    {
        var info = new FileInfo(fontPath);
        var stamp = $"{info.Name}|{info.Length}|{info.LastWriteTimeUtc.Ticks}";
        return ToShortHash(stamp, 12);
    }

    public static string BuildSettingsHash(FontAtlasSettings settings)
    {
        var normalized = string.Join("|", new[]
        {
            NormalizePath(settings.FontFilePath),
            settings.Charset.Trim(),
            NormalizePath(settings.CharsetFilePath),
            settings.AtlasWidth.ToString(),
            settings.AtlasHeight.ToString(),
            settings.PxRange.ToString(),
            settings.Type.ToString().ToLowerInvariant(),
            settings.Padding.ToString(),
            settings.YOrigin.ToString().ToLowerInvariant()
        });

        return ToShortHash(normalized, 16);
    }

    public static FontAtlasPaths BuildPaths(FontAtlasSettings settings)
    {
        var outputDirectory = string.IsNullOrWhiteSpace(settings.OutputDirectory)
            ? Path.Combine(AppContext.BaseDirectory, "FontAtlasCache")
            : settings.OutputDirectory;
        Directory.CreateDirectory(outputDirectory);

        var fontName = Sanitize(Path.GetFileNameWithoutExtension(settings.FontFilePath));
        var type = settings.Type.ToString().ToLowerInvariant();
        var charsetTag = BuildCharsetTag(settings);
        var fontHash = BuildFontIdentity(settings.FontFilePath)[..8];
        var settingsHash = BuildSettingsHash(settings)[..8];
        var baseName = $"{fontName}_{type}_w{settings.AtlasWidth}_h{settings.AtlasHeight}_px{settings.PxRange}_pad{settings.Padding}_cs{charsetTag}_{fontHash}_{settingsHash}";

        var png = Path.Combine(outputDirectory, baseName + ".png");
        var json = Path.Combine(outputDirectory, baseName + ".json");
        var cacheKey = $"{fontHash}{settingsHash}";
        return new FontAtlasPaths(png, json, baseName, cacheKey);
    }

    public static bool IsValidCacheHit(FontAtlasPaths paths)
    {
        return IsNonEmpty(paths.AtlasPngPath) && IsNonEmpty(paths.AtlasJsonPath);
    }

    private static bool IsNonEmpty(string path)
    {
        var file = new FileInfo(path);
        return file.Exists && file.Length > 0;
    }

    private static string BuildCharsetTag(FontAtlasSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.CharsetFilePath))
            return "file_" + ToShortHash(NormalizePath(settings.CharsetFilePath), 6);

        if (string.IsNullOrWhiteSpace(settings.Charset))
            return "default";

        var shortText = settings.Charset.Length <= 8 ? settings.Charset : settings.Charset[..8];
        return Sanitize(shortText) + "_" + ToShortHash(settings.Charset, 6);
    }

    private static string NormalizePath(string path) => (path ?? string.Empty).Trim().Replace('\\', '/').ToLowerInvariant();

    private static string Sanitize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "font";

        var chars = value.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch).ToArray();
        var safe = new string(chars).Replace(' ', '_');
        return safe.Length == 0 ? "font" : safe;
    }

    private static string ToShortHash(string value, int length)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
        var hex = Convert.ToHexString(bytes).ToLowerInvariant();
        return hex[..Math.Clamp(length, 1, hex.Length)];
    }
}
