using System.Text.Json;

namespace Fuse.IO.Font;

#pragma warning disable CS1591

public sealed record FontAtlasJsonReadResult(
    MsdfFontAtlasData Atlas,
    MsdfMetricsInfo Metrics,
    MsdfAtlasInfo AtlasInfo,
    Dictionary<int, MsdfGlyph> GlyphByUnicode);

public static class FontAtlasJsonReader
{
    public static FontAtlasJsonReadResult ReadFromFile(string jsonPath)
    {
        if (string.IsNullOrWhiteSpace(jsonPath))
            throw new ArgumentException("JSON path is empty.", nameof(jsonPath));
        if (!File.Exists(jsonPath))
            throw new FileNotFoundException("JSON file not found.", jsonPath);

        var json = File.ReadAllText(jsonPath);
        return ReadFromJson(json);
    }

    public static FontAtlasJsonReadResult ReadFromJson(string json)
    {
        var data = JsonSerializer.Deserialize<MsdfFontAtlasData>(json)
                   ?? throw new InvalidOperationException("Invalid atlas JSON.");

        var glyphs = new Dictionary<int, MsdfGlyph>(data.glyphs?.Count ?? 0);
        if (data.glyphs != null)
        {
            foreach (var g in data.glyphs)
                glyphs[g.unicode] = g;
        }

        return new FontAtlasJsonReadResult(data, data.metrics, data.atlas, glyphs);
    }
}

public sealed class MsdfFontAtlasData
{
    public MsdfAtlasInfo atlas { get; set; } = new();
    public MsdfMetricsInfo metrics { get; set; } = new();
    public List<MsdfGlyph> glyphs { get; set; } = new();
    public List<object> kerning { get; set; } = new();
}

public sealed class MsdfAtlasInfo
{
    public string type { get; set; } = string.Empty;
    public float distanceRange { get; set; }
    public float distanceRangeMiddle { get; set; }
    public float size { get; set; }
    public int width { get; set; }
    public int height { get; set; }
    public string yOrigin { get; set; } = string.Empty;
    public MsdfGridInfo? grid { get; set; }
}

public sealed class MsdfGridInfo
{
    public float cellWidth { get; set; }
    public float cellHeight { get; set; }
    public int columns { get; set; }
    public int rows { get; set; }
    public float originY { get; set; }
}

public sealed class MsdfMetricsInfo
{
    public float emSize { get; set; }
    public float lineHeight { get; set; }
    public float ascender { get; set; }
    public float descender { get; set; }
    public float underlineY { get; set; }
    public float underlineThickness { get; set; }
}

public sealed class MsdfPlaneBounds
{
    public float left { get; set; }
    public float bottom { get; set; }
    public float right { get; set; }
    public float top { get; set; }
}

public sealed class MsdfAtlasBounds
{
    public float left { get; set; }
    public float bottom { get; set; }
    public float right { get; set; }
    public float top { get; set; }
}

public sealed class MsdfGlyph
{
    public int unicode { get; set; }
    public float advance { get; set; }
    public MsdfPlaneBounds? planeBounds { get; set; }
    public MsdfAtlasBounds? atlasBounds { get; set; }
}
