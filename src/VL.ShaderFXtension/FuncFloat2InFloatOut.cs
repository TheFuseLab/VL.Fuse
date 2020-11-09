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
    public class FuncFloat2InFloatOut : Funk1In1Out<Vector2, float>
    {
        public FuncFloat2InFloatOut(string functionName, IEnumerable<KeyValuePair<string, IComputeNode>> inputs)
            : base(functionName, inputs)
        {

        }
    }
}
