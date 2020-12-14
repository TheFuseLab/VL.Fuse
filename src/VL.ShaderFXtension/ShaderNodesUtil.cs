using System.Collections.Generic;
using Stride.Core.Extensions;
using Stride.Core.Mathematics;

namespace VL.ShaderFXtension
{
    public static class ShaderNodesUtil
    {
        public static OrderedDictionary<string,AbstractGpuValue> BuildInputs(IEnumerable<AbstractGpuValue> inputs)
        {
            var inputsDict = new OrderedDictionary<string,AbstractGpuValue>();
            var c = 0;
            inputs.ForEach(input =>
            {
                inputsDict.Add("val" + c,input);
                c++;
            });
            return inputsDict;
        }
        
        public static OrderedDictionary<string,AbstractGpuValue> BuildInputs(params AbstractGpuValue[] inputs)
        {
            var inputsDict = new OrderedDictionary<string,AbstractGpuValue>();
            var c = 0;
            inputs.ForEach(input =>
            {
                inputsDict.Add("val" + c,input);
                c++;
            });
            return inputsDict;
        }

        public static string BuildResult(GpuValue<Vector4> theValue)
        {
            return "return " + theValue.ID;
        }
    }
}