using Stride.Core.Mathematics;
using System.Collections.Generic;
using Stride.Rendering.Materials;
using Stride.Shaders;
using VL.Stride.Shaders.ShaderFX.Functions;

namespace VL.ShaderFXtension
{
    /// <summary>
    /// Represents any shader that implements SDF3D with input compositions
    /// </summary>
    public class FuncFloat4InFloat4Out : Funk1In1Out<Vector4, Vector4>
    {
        public FuncFloat4InFloat4Out(string functionName, IEnumerable<KeyValuePair<string, IComputeNode>> inputs)
            : base(functionName, inputs)
        {

        }
    }
}
