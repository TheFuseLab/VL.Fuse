using Stride.Rendering;

namespace VL.ShaderFXtension
{
    public interface IGPUInput : IShaderNode
    {
        void SetParameterValue(ParameterCollection theCollection);
    }
}