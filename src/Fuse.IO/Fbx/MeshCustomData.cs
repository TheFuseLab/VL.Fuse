using Assimp;

namespace Fuse.IO.Fbx;

/// <summary>A named parallel vertex stream. Values always use a 16-byte float4 GPU stride.</summary>
public sealed class MeshCustomChannel
{
    /// <summary>Stable semantic name TEXCOORD1.. or COLOR0..; not the original authoring-layer name.</summary>
    public string Name { get; init; } = "";
    public string Kind { get; init; } = "";
    public int Index { get; init; }
    /// <summary>Maximum source component count across mesh instances; storage is always float4.</summary>
    public int ComponentCount { get; init; }
    public Vector4[] Values { get; init; } = [];
    /// <summary>False means this channel was absent in that vertex's source mesh; Values then contains zero.</summary>
    public bool[] Present { get; init; } = [];
    public Vector4[] GetValues() => Values;
}

/// <summary>Packs Assimp channels in exactly the same mesh-instance/vertex order as the geometry stream.</summary>
public static class MeshCustomDataPacking
{
    public static MeshCustomChannel[] Pack(IReadOnlyList<Mesh> meshes)
    {
        var total = meshes.Sum(m => m.VertexCount);
        var result = new List<MeshCustomChannel>();
        // UV0 remains in GpuVertex.Tex. All additional UV sets and all color sets are exposed here.
        var uvCount = meshes.Count == 0 ? 0 : meshes.Max(m => m.TextureCoordinateChannels.Length);
        var colorCount = meshes.Count == 0 ? 0 : meshes.Max(m => m.VertexColorChannels.Length);
        for (var i = 1; i < uvCount; i++)
            Add("TEXCOORD", i, false);
        for (var i = 0; i < colorCount; i++)
            Add("COLOR", i, true);
        return result.ToArray();

        void Add(string kind, int channelIndex, bool color)
        {
            bool Has(Mesh mesh) => color ? mesh.HasVertexColors(channelIndex) : mesh.HasTextureCoords(channelIndex);
            if (!meshes.Any(Has)) return;
            var values = new Vector4[total];
            var present = new bool[total];
            var components = color ? 4 : meshes.Where(Has).Max(m => m.UVComponentCount[channelIndex]);
            var offset = 0;
            foreach (var mesh in meshes)
            {
                if (Has(mesh))
                    for (var v = 0; v < mesh.VertexCount; v++)
                    {
                        if (color)
                        {
                            var c = mesh.VertexColorChannels[channelIndex][v];
                            values[offset + v] = new Vector4(c.R, c.G, c.B, c.A);
                        }
                        else
                        {
                            var uv = mesh.TextureCoordinateChannels[channelIndex][v];
                            values[offset + v] = new Vector4(uv.X, uv.Y, uv.Z, 0);
                        }
                        present[offset + v] = true;
                    }
                offset += mesh.VertexCount;
            }
            result.Add(new MeshCustomChannel { Name = kind + channelIndex, Kind = kind, Index = channelIndex,
                ComponentCount = components, Values = values, Present = present });
        }
    }
}
