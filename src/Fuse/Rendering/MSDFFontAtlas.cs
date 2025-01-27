using System.Collections.Generic;
using Stride.Core.Mathematics;
// ReSharper disable InconsistentNaming

namespace Fuse.Rendering;
using System.Text.Json;

public class MSDFFontAtlas
{
    public class AtlasInfo
    {
        public string type { get; set; }
        public float distanceRange { get; set; }
        public float distanceRangeMiddle { get; set; }
        public float size { get; set; }
        public int width { get; set; }
        public int height { get; set; }
        public string yOrigin { get; set; }
        public GridInfo grid { get; set; }
    }

    public class GridInfo
    {
        public float cellWidth { get; set; }
        public float cellHeight { get; set; }
        public int columns { get; set; }
        public int rows { get; set; }
        public float originY { get; set; }
    }

    public class MetricsInfo
    {
        public float emSize { get; set; }
        public float lineHeight { get; set; }
        public float ascender { get; set; }
        public float descender { get; set; }
        public float underlineY { get; set; }
        public float underlineThickness { get; set; }
    }

    public class PlaneBounds
    {
        public float left { get; set; }
        public float bottom { get; set; }
        public float right { get; set; }
        public float top { get; set; }
    }

    public class AtlasBounds
    {
        public float left { get; set; }
        public float bottom { get; set; }
        public float right { get; set; }
        public float top { get; set; }
    }

    public class GlyphInfo
    {
        public int unicode { get; set; }
        public float advance { get; set; }
        public PlaneBounds planeBounds { get; set; }
        public AtlasBounds atlasBounds { get; set; }
    }

    public class MSDFFont
    {
        public AtlasInfo atlas { get; set; }
        public MetricsInfo metrics { get; set; }
        public List<GlyphInfo> glyphs { get; set; }
        public List<object> kerning { get; set; }
    }

    private Dictionary<char, GlyphInfo> glyphMap;
    private readonly MSDFFont fontData;

    public MSDFFontAtlas(string jsonData)
    {
        fontData = JsonSerializer.Deserialize<MSDFFont>(jsonData);
        InitializeGlyphMap();
    }

    private void InitializeGlyphMap()
    {
        glyphMap = new Dictionary<char, GlyphInfo>();
        foreach (var glyph in fontData.glyphs)
        {
            if (glyph.unicode > 0)
            {
                glyphMap[(char)glyph.unicode] = glyph;
            }
        }
    }

    public (Vector2 position, Vector2 uvMin, Vector2 uvMax) GetGlyphQuad(char character, Vector2 position, float scale)
    {
        if (!glyphMap.TryGetValue(character, out GlyphInfo glyph))
        {
            return default;
        }

        // Skip if glyph has no bounds (like spaces)
        if (glyph.planeBounds == null || glyph.atlasBounds == null)
        {
            return (new Vector2(position.X + glyph.advance * scale, position.Y), Vector2.Zero, Vector2.Zero);
        }

        float atlasWidth = fontData.atlas.width;
        float atlasHeight = fontData.atlas.height;

        // Calculate UV coordinates
        var uvMin = new Vector2(
            glyph.atlasBounds.left / atlasWidth,
            1 - glyph.atlasBounds.bottom / atlasHeight
        );
        
        var uvMax = new Vector2(
            glyph.atlasBounds.right / atlasWidth,
            1 - glyph.atlasBounds.top / atlasHeight
        );

        // Calculate quad position
        var quadLeft = position.X + glyph.planeBounds.left * scale;
        var quadBottom = position.Y + glyph.planeBounds.bottom * scale;
        var quadRight = position.X + glyph.planeBounds.right * scale;
        var quadTop = position.Y + glyph.planeBounds.top * scale;

        return (new Vector2(quadRight, quadTop), uvMin, uvMax);
    }

    // Helper method to get advance width for a character
    public float GetAdvance(char character, float scale)
    {
        return glyphMap.TryGetValue(character, out GlyphInfo glyph) ? glyph.advance * scale : 0;
    }

    // Get the line height
    public float GetLineHeight(float scale)
    {
        return fontData.metrics.lineHeight * scale;
    }
}

