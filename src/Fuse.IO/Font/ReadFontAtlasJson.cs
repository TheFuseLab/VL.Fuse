namespace Fuse.IO.Font;

#pragma warning disable CS1591

[ProcessNode]
public sealed class ReadFontAtlasJson
{
    public string AtlasJsonPath { private get; set; } = string.Empty;
    public bool Read { private get; set; }

    public MSDFFontAtlas? Atlas { get; private set; }
    public Dictionary<int, MSDFFontAtlas.Glyph> GlyphByUnicode { get; private set; } = new();
    public MSDFFontAtlas.AtlasInfo? AtlasInfo { get; private set; }
    public MSDFFontAtlas.MetricsInfo? Metrics { get; private set; }
    public bool Succeeded { get; private set; }
    public string ErrorMessage { get; private set; } = string.Empty;

    public void Update()
    {
        if (!Read || string.IsNullOrWhiteSpace(AtlasJsonPath))
            return;

        try
        {
            var result = FontAtlasJsonReader.ReadFromFile(AtlasJsonPath);
            Atlas = result.Atlas;
            GlyphByUnicode = result.GlyphByUnicode;
            AtlasInfo = result.AtlasInfo;
            Metrics = result.Metrics;
            Succeeded = true;
            ErrorMessage = string.Empty;
        }
        catch (Exception ex)
        {
            Succeeded = false;
            ErrorMessage = ex.Message;
        }
    }
}
