using System.Text.Json;
using Stride.Core.Mathematics;

namespace Fuse.IO.Font;

#pragma warning disable CS1591

public sealed class MSDFFontAtlas
{
    private readonly FontData _font;
    private readonly Dictionary<int, Glyph> _glyphs;
    private readonly float _invW, _invH;
    private readonly bool _yOriginBottom;

    // 1:1 with rendering version + path support:
    // If input points to an existing file, load JSON from file. Otherwise treat input as JSON content.
    public MSDFFontAtlas(string jsonDataOrPath)
    {
        var jsonData = File.Exists(jsonDataOrPath) ? File.ReadAllText(jsonDataOrPath) : jsonDataOrPath;

        _font = JsonSerializer.Deserialize<FontData>(jsonData)
                ?? throw new ArgumentException("Invalid MSDF atlas JSON.", nameof(jsonDataOrPath));

        _invW = 1f / _font.atlas.width;
        _invH = 1f / _font.atlas.height;
        _yOriginBottom = string.Equals(_font.atlas.yOrigin, "bottom", StringComparison.OrdinalIgnoreCase);

        _glyphs = new Dictionary<int, Glyph>(_font.glyphs?.Count ?? 256);
        if (_font.glyphs != null)
            foreach (var g in _font.glyphs)
                _glyphs[g.unicode] = g;
    }

    public static MSDFFontAtlas LoadFromFile(string jsonPath)
    {
        if (string.IsNullOrWhiteSpace(jsonPath))
            throw new ArgumentException("JSON path is empty.", nameof(jsonPath));
        if (!File.Exists(jsonPath))
            throw new FileNotFoundException("JSON file not found.", jsonPath);

        return new MSDFFontAtlas(jsonPath);
    }

    public readonly struct GlyphQuad
    {
        public readonly Vector2 NextPen;
        public readonly Vector2 UvMin;
        public readonly Vector2 UvMax;
        public readonly Vector2 QuadMin;
        public readonly Vector2 QuadMax;
        public readonly float Advance;
        public readonly bool Drawable;

        public GlyphQuad(Vector2 nextPen, Vector2 uvMin, Vector2 uvMax, Vector2 quadMin, Vector2 quadMax, float advance, bool drawable)
        {
            NextPen = nextPen;
            UvMin = uvMin;
            UvMax = uvMax;
            QuadMin = quadMin;
            QuadMax = quadMax;
            Advance = advance;
            Drawable = drawable;
        }
    }

    public GlyphQuad GetGlyphQuad(char character, Vector2 position, float scale, bool insetHalfTexel = true)
    {
        if (!_glyphs.TryGetValue(character, out var g))
            return default;

        float advance = g.advance * scale;
        Vector2 nextPen = new(position.X + advance, position.Y);

        if (g.planeBounds == null || g.atlasBounds == null)
            return new GlyphQuad(nextPen, Vector2.Zero, Vector2.Zero, Vector2.Zero, Vector2.Zero, advance, drawable: false);

        float insetU = insetHalfTexel ? 0.5f : 0f;
        float insetV = insetHalfTexel ? 0.5f : 0f;

        float L = g.atlasBounds.left;
        float B = g.atlasBounds.bottom;
        float R = g.atlasBounds.right;
        float T = g.atlasBounds.top;

        float u0 = (L + insetU) * _invW;
        float u1 = (R - insetU) * _invW;

        float v0, v1;
        if (_yOriginBottom)
        {
            v0 = 1f - (T - insetV) * _invH;
            v1 = 1f - (B + insetV) * _invH;
        }
        else
        {
            v0 = (B + insetV) * _invH;
            v1 = (T - insetV) * _invH;
        }

        var uvMin = new Vector2(u0, v0);
        var uvMax = new Vector2(u1, v1);

        float ql = position.X + g.planeBounds.left * scale;
        float qb = position.Y + g.planeBounds.bottom * scale;
        float qr = position.X + g.planeBounds.right * scale;
        float qt = position.Y + g.planeBounds.top * scale;

        var quadMin = new Vector2(ql, qb);
        var quadMax = new Vector2(qr, qt);

        return new GlyphQuad(nextPen, uvMin, uvMax, quadMin, quadMax, advance, drawable: true);
    }

    public float GetAdvance(char character, float scale)
        => _glyphs.TryGetValue(character, out var g) ? g.advance * scale : 0f;

    public float GetLineHeight(float scale) => _font.metrics.lineHeight * scale;

    public AtlasInfo Atlas => _font.atlas;

    public sealed class AtlasInfo
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

    public sealed class GridInfo
    {
        public float cellWidth { get; set; }
        public float cellHeight { get; set; }
        public int columns { get; set; }
        public int rows { get; set; }
        public float originY { get; set; }
    }

    public sealed class MetricsInfo
    {
        public float emSize { get; set; }
        public float lineHeight { get; set; }
        public float ascender { get; set; }
        public float descender { get; set; }
        public float underlineY { get; set; }
        public float underlineThickness { get; set; }
    }

    public sealed class PlaneBounds { public float left { get; set; } public float bottom { get; set; } public float right { get; set; } public float top { get; set; } }
    public sealed class AtlasBounds { public float left { get; set; } public float bottom { get; set; } public float right { get; set; } public float top { get; set; } }

    public sealed class Glyph
    {
        public int unicode { get; set; }
        public float advance { get; set; }
        public PlaneBounds planeBounds { get; set; }
        public AtlasBounds atlasBounds { get; set; }
    }

    public sealed class FontData
    {
        public AtlasInfo atlas { get; set; }
        public MetricsInfo metrics { get; set; }
        public List<Glyph> glyphs { get; set; }
        public List<object> kerning { get; set; }
    }
}
