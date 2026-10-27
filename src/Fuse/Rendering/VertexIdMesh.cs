using System;
using System.Collections.Generic;
using System.Linq;
using Stride.Core.Mathematics;
using Stride.Graphics;
using Stride.Rendering;
using VL.Stride.Rendering;
using Buffer = Stride.Graphics.Buffer;

namespace Fuse.Rendering;

/// <summary>
/// Mesh for drawing a structured resource per SV_VertexID / SV_InstanceID: the draw shader reads its attributes from
/// the StructuredBuffer, so no resource data goes through the input assembler.
/// </summary>
/// <remarks>
/// D3D11 needs at least one input element, otherwise CreateInputLayout fails and Stride binds against a stale
/// pipeline state (NRE in ResourceBinder.BindResources). The mesh therefore carries one dummy vertex buffer with
/// stride 0 (every vertex reads element 0, filled with zeros). Its declaration is PositionNormalTexture plus the
/// optional <paramref name="extraDeclaration"/> (e.g. the resource's vertex declaration), so a draw shader that
/// still reads resource attributes as vertex streams gets a valid layout and reads zeros instead of failing.
/// With <c>drawArgs</c> the draw is non-indexed indirect (VL.Stride issues DrawInstancedIndirect when a
/// MeshDrawIndirect has DrawArgs and no index buffer; a DrawIndexed argument layout works as long as StartIndex and
/// BaseVertex are 0).
/// </remarks>
public sealed class VertexIdMesh : IDisposable
{
    private readonly Mesh _mesh = new() { Draw = new MeshDrawIndirect { PrimitiveType = PrimitiveType.PointList } };
    private GraphicsDevice _device;
    private VertexDeclaration _declaration;
    private Buffer _dummy;

    public Mesh Update(
        GraphicsDevice graphicsDevice,
        int vertexCount = 1,
        PrimitiveType topology = PrimitiveType.PointList,
        BoundingBox boundingBox = default,
        VertexDeclaration extraDeclaration = null,
        Buffer drawArgs = null)
    {
        var declaration = Combine(VertexPositionNormalTexture.Layout, extraDeclaration);
        if (!ReferenceEquals(graphicsDevice, _device) || !Same(declaration, _declaration))
        {
            ReleaseDummy();
            _device = graphicsDevice;
            _declaration = declaration;
            if (graphicsDevice != null)
            {
                _dummy = Buffer.Vertex.New(graphicsDevice, new byte[declaration.VertexStride]);
                _mesh.Draw.VertexBuffers = new[] { new VertexBufferBinding(_dummy, declaration, 1, vertexStride: 0) };
            }
        }

        var draw = (MeshDrawIndirect)_mesh.Draw;
        draw.PrimitiveType = topology;
        draw.DrawCount = System.Math.Max(0, vertexCount);
        draw.DrawArgs = drawArgs;
        _mesh.BoundingBox = boundingBox;
        _mesh.BoundingSphere = BoundingSphere.FromBox(boundingBox);
        return _mesh;
    }

    private static VertexDeclaration Combine(VertexDeclaration baseLayout, VertexDeclaration extra)
    {
        if (extra == null)
            return baseLayout;
        var elements = new List<VertexElement>(baseLayout.VertexElements);
        var offset = baseLayout.VertexStride;
        foreach (var e in extra.VertexElements)
        {
            if (elements.Any(x => x.SemanticName == e.SemanticName && x.SemanticIndex == e.SemanticIndex))
                continue;
            elements.Add(new VertexElement(e.SemanticName, e.SemanticIndex, e.Format, offset));
            offset += e.Format.SizeInBytes();
        }
        return new VertexDeclaration(elements.ToArray());
    }

    private static bool Same(VertexDeclaration a, VertexDeclaration b)
        => a != null && b != null && a.VertexElements.SequenceEqual(b.VertexElements);

    private void ReleaseDummy()
    {
        _mesh.Draw.VertexBuffers = Array.Empty<VertexBufferBinding>();
        _dummy?.Dispose();
        _dummy = null;
    }

    public void Dispose() => ReleaseDummy();
}
