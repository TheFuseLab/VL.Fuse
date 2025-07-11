using Fuse.compute;
using Stride.Rendering;
using Stride.Rendering.Materials;

namespace Fuse.ShaderFX;

public class FuseMaterialClearCoatFeature : MaterialClearCoatFeature
{
    public FuseMaterialClearCoatFeature(Material theOriginalMaterial, ShaderNode<GpuVoid> theShaderNode, Mesh theMesh)
    {
        MaterialShading = theShaderNode;
        OriginalMaterial = theOriginalMaterial;
        Enabled = false;
    }

    public Material OriginalMaterial { get; }
    public ShaderNode<GpuVoid> MaterialShading { get; }
}