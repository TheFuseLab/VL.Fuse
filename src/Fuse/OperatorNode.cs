using System.Collections.Generic;
using System.Linq;
using System.Text;
using Stride.Core.Extensions;

namespace Fuse
{
    public class OperatorNode<TIn, TOut> : ShaderNode<TOut>
    {

        public OperatorNode(IEnumerable<GpuValue<TIn>> theInputs, ConstantValue<TOut> theDefault, string theOperator) : base("Operator", theDefault)
        { 
            
            var abstractGpuValues = new List<GpuValue<TIn>>();
            
            var call = new StringBuilder();
            theInputs.ForEach(input =>
            {
                if (input == null) return;
                abstractGpuValues.Add(input);
                call.Append(input.ID);
                call.Append(" " + theOperator + " ");
            });
            
            if(call.Length > 3)call.Remove(call.Length - 3, 3);
            
            Setup(
               "${resultType} ${resultName} = ${Call};", 
               abstractGpuValues, 
               new Dictionary<string,string>
               {
                   {"Call",call.ToString()}
               });
        }

        public OperatorNode(GpuValue<TIn> input0, GpuValue<TIn> input1, ConstantValue<TOut> theDefault, string theOperator) :
            this(new List<GpuValue<TIn>> {input0, input1}, theDefault, theOperator){
        }
    }
    
    
}