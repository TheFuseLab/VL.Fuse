using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        
        public static string BuildArguments(OrderedDictionary<string,AbstractGpuValue> inputs)
        {
            var stringBuilder = new StringBuilder();
            inputs.ForEach(input =>
            {
                if (input.Value == null) return;
                stringBuilder.Append(input.Value.ID);
                stringBuilder.Append(", ");
            });
            if(stringBuilder.Length > 2)stringBuilder.Remove(stringBuilder.Length - 2, 2);
            return stringBuilder.ToString();
        }

        public static string BuildResult(GpuValue<Vector4> theValue)
        {
            return "return " + theValue.ID;
        }
        public static  bool HasNullValue<T1,T2>(IDictionary<T1,T2> theMap)
        {
            return theMap.Any(kv => kv.Value == null);
        }
        
    }
}