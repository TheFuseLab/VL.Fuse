using System.Text.Json;

namespace Fuse.IO.Font;

#pragma warning disable CS1591

public sealed record FontAtlasJsonReadResult(
    MSDFFontAtlas Atlas,
    MSDFFontAtlas.MetricsInfo Metrics,
    MSDFFontAtlas.AtlasInfo AtlasInfo,
    Dictionary<int, MSDFFontAtlas.Glyph> GlyphByUnicode);

public static class FontAtlasJsonReader
{
    public static FontAtlasJsonReadResult ReadFromFile(string jsonPath)
    {
        var atlas = MSDFFontAtlas.LoadFromFile(jsonPath);
        var data = JsonSerializer.Deserialize<MSDFFontAtlas.FontData>(File.ReadAllText(jsonPath))
                   ?? throw new InvalidOperationException("Invalid atlas JSON.");
        var glyphByUnicode = data.glyphs.ToDictionary(g => g.unicode, g => g);
        return new FontAtlasJsonReadResult(atlas, data.metrics, data.atlas, glyphByUnicode);
    }

    public static FontAtlasJsonReadResult ReadFromJson(string json)
    {
        var atlas = new MSDFFontAtlas(json);
        var data = JsonSerializer.Deserialize<MSDFFontAtlas.FontData>(json)
                   ?? throw new InvalidOperationException("Invalid atlas JSON.");
        var glyphByUnicode = data.glyphs.ToDictionary(g => g.unicode, g => g);
        return new FontAtlasJsonReadResult(atlas, data.metrics, data.atlas, glyphByUnicode);
    }
}
