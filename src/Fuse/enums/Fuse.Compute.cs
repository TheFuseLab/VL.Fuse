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

public static class ResourceIndexSourceUtil
{
    /// <summary>Picks the element index for <paramref name="source"/>; falls back to the vertex id when the
    /// chosen source is not connected (e.g. Indices unset).</summary>
    public static ShaderNode<T> SelectIndex<T>(
        ResourceIndexSource source,
        ShaderNode<T> vertexId,
        ShaderNode<T> instanceId,
        ShaderNode<T> indicesVertexId)
    {
        return source switch
        {
            ResourceIndexSource.InstanceID => instanceId ?? vertexId,
            ResourceIndexSource.IndicesVertexID => indicesVertexId ?? vertexId,
            _ => vertexId
        };
    }
}
