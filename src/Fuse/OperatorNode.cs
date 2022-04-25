using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Fuse
{
    public class OperatorNode<TIn, TOut> : ShaderNode<TOut> where TIn : struct where TOut : struct
    {

        private readonly string _operator;
        public OperatorNode(IEnumerable<ShaderNode<TIn>> theInputs, ShaderNode<TOut> theDefault, string theOperator) : base("result", theDefault)
        {
            _operator = theOperator;
            Setup(theInputs);
        }

        public OperatorNode(ShaderNode<TIn> input0, ShaderNode<TIn> input1, ShaderNode<TOut> theDefault, string theOperator) :
            this(new List<ShaderNode<TIn>> {input0, input1}, theDefault, theOperator){
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