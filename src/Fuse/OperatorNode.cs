using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Fuse
{
    public class OperatorNode<TIn, TOut> : ShaderNode<TOut>
    {

        public OperatorNode(IEnumerable<GpuValue<TIn>> theInputs, ConstantValue<TOut> theDefault, string theOperator) : base("Operator", theDefault)
        { 
            var abstractGpuValues = theInputs.ToList();
            
            Setup(
               "${resultType} ${resultName} = ${Call};", 
               abstractGpuValues, 
               new Dictionary<string,string>
               {
                   {"Call",BuildCall(abstractGpuValues,theOperator)}
               });
        }

        public OperatorNode(GpuValue<TIn> input0, GpuValue<TIn> input1, ConstantValue<TOut> theDefault, string theOperator) :
            this(new List<GpuValue<TIn>> {input0, input1}, theDefault, theOperator){
        }

        private static string BuildCall(List<GpuValue<TIn>> inputs, string theOperator)
        {
            
            var stringBuilder = new StringBuilder();
            inputs.ForEach(input =>
            {
                if (input == null) return;
                stringBuilder.Append(input.ID);
                stringBuilder.Append(" " + theOperator + " ");
            });
            if(stringBuilder.Length > 3)stringBuilder.Remove(stringBuilder.Length - 3, 3);
            return stringBuilder.ToString();
        }
    }
    
    
}