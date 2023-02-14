using System.Collections.Generic;
using System.Linq;
using System.Text;
using VL.Core;

namespace Fuse
{
    public class Operator<T> : ResultNode<T> where T : struct 
    {

        private readonly string _operator;
        public Operator(NodeContext nodeContext, ShaderNode<T> theDefault, string theOperator) : base(nodeContext,"result",  theDefault)
        {
            _operator = theOperator;
        }

        public void SetInput(IEnumerable<AbstractShaderNode> theInputs)
        {
            var myCheckedInputs = new List<AbstractShaderNode>();
            foreach (var myInput in theInputs)
            {
                myCheckedInputs.Add(myInput ?? Default);
            }
            SetInputs(myCheckedInputs);
        }

        protected override string ImplementationTemplate()
        {
            var call = new StringBuilder();
            Ins.ForEach(input =>
            {
                call.Append(input.ID);
                call.Append(" " + _operator + " ");
            });
            
            if(call.Length > 3)call.Remove(call.Length - 3, 3);
            return ShaderNodesUtil.Evaluate("(${Call})",new Dictionary<string,string>
            {
                {"Call",call.ToString()}
            });
        }
    }
    
    public class AssignOperator<T> : ShaderNode<T> where T : struct 
    {

        private readonly string _operator;

        private AbstractShaderNode _inputA;
        private AbstractShaderNode _inputB;
        public AssignOperator(NodeContext nodeContext, ShaderNode<T> theDefault, string theOperator) : base(nodeContext,"result",  theDefault)
        {
            _operator = theOperator;
        }

        public void SetInput(AbstractShaderNode theInputA, AbstractShaderNode theInputB)
        {
            _inputA = theInputA;
            _inputB = theInputB;
            SetInputs(new List<AbstractShaderNode>{theInputA, theInputB});
        }
        
        protected override string SourceTemplate()
        {
            
            return ShaderNodesUtil.Evaluate("${inputA} ${operator}= ${inputB};",new Dictionary<string, string> {
                {"inputA", _inputA.ID},
                {"operator", _operator},
                {"inputB", _inputB.ID},
            });
        }
    }
}