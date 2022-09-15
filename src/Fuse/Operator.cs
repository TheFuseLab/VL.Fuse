using System.Collections.Generic;
using System.Linq;
using System.Text;
using VL.Core;

namespace Fuse
{
    public class Operator<TIn, TOut> : ShaderNode<TOut> where TIn : struct where TOut : struct
    {

        private readonly string _operator;
        public Operator(NodeContext nodeContext, ShaderNode<TOut> theDefault, string theOperator) : base(nodeContext,"result",  theDefault)
        {
            _operator = theOperator;
        }

        public void SetInput(IEnumerable<ShaderNode<TIn>> theInputs)
        {
            
            SetInputs(theInputs);
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