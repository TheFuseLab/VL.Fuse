using System.Collections.Generic;
using System.Linq;
using System.Text;
using Stride.Core.Extensions;

namespace Fuse
{
    public class OperatorNode<TIn, TOut> : ShaderNode<TOut> where TIn : struct where TOut : struct
    {

        private readonly string _operator;
        public OperatorNode(IEnumerable<GpuValue<TIn>> theInputs, ConstantValue<TOut> theDefault, string theOperator) : base("Operator", theDefault)
        { 
            
            _operator = theOperator;
            
            var abstractGpuValues = new List<GpuValue<TIn>>();
            theInputs.ForEach(input =>
            {
                if (input == null) return;
                abstractGpuValues.Add(input);
            });
            
            Setup(abstractGpuValues);
        }

        public OperatorNode(GpuValue<TIn> input0, GpuValue<TIn> input1, ConstantValue<TOut> theDefault, string theOperator) :
            this(new List<GpuValue<TIn>> {input0, input1}, theDefault, theOperator){
        }

        protected override string SourceTemplate()
        {
            var call = new StringBuilder();
            Ins.ForEach(input =>
            {
                call.Append(input.ID);
                call.Append(" " + _operator + " ");
            });
            
            if(call.Length > 3)call.Remove(call.Length - 3, 3);
            return ShaderNodesUtil.Evaluate("${resultType} ${resultName} = ${Call};",new Dictionary<string,string>
            {
                {"Call",call.ToString()}
            });
        }
    }
    
    
}