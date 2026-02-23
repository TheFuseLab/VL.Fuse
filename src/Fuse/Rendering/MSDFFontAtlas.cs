using System;
using System.Collections.Generic;
using System.Text.Json;
using Stride.Core.Mathematics;

namespace Fuse.Rendering;

public sealed class MSDFFontAtlas
{
    private readonly FontData _font;
    private readonly Dictionary<int, Glyph> _glyphs;
    private readonly float _invW, _invH;
    private readonly bool _yOriginBottom;

    public MSDFFontAtlas(string jsonData)
    {
        _font = JsonSerializer.Deserialize<FontData>(jsonData)
                ?? throw new ArgumentException("Invalid MSDF atlas JSON.", nameof(jsonData));

        _invW = 1f / _font.atlas.width;
        _invH = 1f / _font.atlas.height;
        _yOriginBottom = string.Equals(_font.atlas.yOrigin, "bottom", StringComparison.OrdinalIgnoreCase);

        _glyphs = new Dictionary<int, Glyph>(_font.glyphs?.Count ?? 256);
        if (_font.glyphs != null)
            foreach (var g in _font.glyphs)
                _glyphs[g.unicode] = g;
    }

    // Same intent as your original return tuple, but explicit + includes advance.
    public readonly struct GlyphQuad
    {
        // Next pen position (baseline). This is the only thing you should use to step the cursor.
        public readonly Vector2 NextPen;

        // UV rect for sampling (0..1). Zero when not drawable (space etc).
        public readonly Vector2 UvMin;
        public readonly Vector2 UvMax;

        // The actual quad bounds in target space (baseline anchored), useful if you want it.
        public readonly Vector2 QuadMin;
        public readonly Vector2 QuadMax;

        // Advance in target units (scaled).
        public readonly float Advance;

        // Whether this glyph has atlas+plane bounds and can be drawn.
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

    /// <summary>
    /// Keeps your original signature: (character, position, scale) -> glyph quad.
    /// position is the current pen/baseline position.
    /// Returns:
    ///  - NextPen = (position.X + advance*scale, position.Y)
    ///  - UvMin/UvMax for sampling (if drawable)
    ///  - QuadMin/QuadMax bounds (if drawable)
    /// </summary>
    public GlyphQuad GetGlyphQuad(char character, Vector2 position, float scale, bool insetHalfTexel = true)
    {
        if (!_glyphs.TryGetValue(character, out var g))
            return default;

        float advance = g.advance * scale;
        Vector2 nextPen = new(position.X + advance, position.Y);

        // Spaces / non-renderable glyphs: advance only
        if (g.planeBounds == null || g.atlasBounds == null)
            return new GlyphQuad(nextPen, Vector2.Zero, Vector2.Zero, Vector2.Zero, Vector2.Zero, advance, drawable: false);

        // --- UV rect from atlasBounds (pixel coords) -> normalized UV
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
            // bottom-origin pixels -> top-origin UV (D3D)
            v0 = 1f - (T - insetV) * _invH; // top
            v1 = 1f - (B + insetV) * _invH; // bottom
        }
        else
        {
            v0 = (B + insetV) * _invH;
            v1 = (T - insetV) * _invH;
        }

        var uvMin = new Vector2(u0, v0);
        var uvMax = new Vector2(u1, v1);

        // --- Quad bounds from planeBounds (baseline anchored)
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

    // ---------------- JSON types ----------------

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