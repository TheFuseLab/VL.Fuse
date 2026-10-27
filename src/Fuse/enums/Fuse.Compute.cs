namespace Fuse.Compute;

/// <summary>Which element of a structured resource a draw shader vertex reads (variant A draw path).</summary>
public enum ResourceIndexSource
{
    /// <summary>SV_VertexID: one vertex per element (points, sprites, quads expanded in the geometry stage).</summary>
    VertexID,

    /// <summary>SV_InstanceID: one instance per element (instanced meshes).</summary>
    InstanceID,

    /// <summary>Indices[SV_VertexID]: indexed draws (lines/strips) drawn non-indexed, the index read in the shader.</summary>
    IndicesVertexID
}
