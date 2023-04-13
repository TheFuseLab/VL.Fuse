using Fuse.compute;
using Stride.Core.Assets;
using Stride.Rendering;
using Stride.Rendering.Materials;

namespace Fuse.ShaderFX;

public class FuseMaterialClearCoatFeature : MaterialClearCoatFeature
{
    public Material OriginalMaterial { get;}
    public ShaderNode<GpuVoid> MaterialShading { get; }
    
    public FuseMaterialClearCoatFeature(Material theOriginalMaterial, ShaderNode<GpuVoid> theShaderNode)
    {
        MaterialShading = theShaderNode;
        OriginalMaterial = theOriginalMaterial;
        Enabled = false;
    }
    
}