using System.Collections.Generic;
using Stride.Core.Extensions;

namespace VL.ShaderFXtension
{
    public static class ShaderNodesUtil
    {
        public static Dictionary<string,AbstractGPUValue> BuildInputs(IEnumerable<AbstractGPUValue> inputs)
        {
            var inputsDict = new Dictionary<string,AbstractGPUValue>();
            var c = 0;
            inputs.ForEach(input =>
            {
                inputsDict.Add("val" + c,input);
                c++;
            });
            return inputsDict;
        }
    }
}