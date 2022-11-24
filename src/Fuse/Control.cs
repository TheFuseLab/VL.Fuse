using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Fuse.compute;
using Stride.Core.Extensions;
using VL.Core;
using VL.Stride.Shaders.ShaderFX;

namespace Fuse
{
    public class SwitchBoolean<T> : ShaderNode<T>
    {
        private ShaderNode<bool> _inCheck;
        private ShaderNode<T> _inFalse;
        private ShaderNode<T> _inTrue;
        public SwitchBoolean(NodeContext nodeContext, ShaderNode<T> theDefault = null) : base(nodeContext, "expression", theDefault)
        {
            
        }

        public void SetInputs(ShaderNode<bool> inCheck, ShaderNode<T> inFalse, ShaderNode<T> inTrue)
        {
            var ins = new List<AbstractShaderNode>(){inCheck};
            _inCheck = inCheck;
            _inFalse = inFalse;
            _inTrue = inTrue;
            
            if(_inFalse != null)ins.Add(_inFalse);
            if(_inTrue != null)ins.Add(_inTrue);
            if(_inFalse == null && _inTrue == null)ins.Add(null);
            
            SetInputs(ins);
        }

        protected override string SourceTemplate()
        {
            return ShaderNodesUtil.Evaluate("${resultType} ${resultName} = ${check} ? ${inTrue} : ${inFalse};", 
                new Dictionary<string, string>
                {
                    {"check", _inCheck.ID},
                    {"inFalse", _inFalse.ID},
                    {"inTrue", _inTrue.ID}
                });
        }
    }
    
    public class SwitchNumeric<T> : ShaderNode<T>
    {
        private new const string ShaderCode = @"
    ${resultType} ${resultName}; 
    switch(${check}){
${cases}
    }
        ";

        private ShaderNode<T> _default;
        private List<ShaderNode<T>> _cases;
        private ShaderNode<int> _check;
        public SwitchNumeric(NodeContext nodeContext) : base(nodeContext, "numericSwitch")
        {
            
            
        }

        public void SetInputs(ShaderNode<int> theCheck, IEnumerable<ShaderNode<T>> inputs, ShaderNode<T> theDefault)
        {
            _check = theCheck;
            _cases = inputs.ToList();
            _default = theDefault;
            var myIns = new List<AbstractShaderNode>(_cases);
            if (theDefault != null)
            {
                myIns.Add(theDefault);
            }
            
            var myKeyMap = new Dictionary<string, string>
            {
                {"check", theCheck.ID}
            };
            SetInputs(myIns);
        }
        
        private static string BuildCases(IEnumerable<AbstractShaderNode> inputs, ShaderNode<T> theDefault)
        {
            var stringBuilder = new StringBuilder();
            var c = 0;
            inputs.ForEach(input =>
            {
                if (input == null) return;
                stringBuilder.Append("    case " + c + ":"+ Environment.NewLine);
                stringBuilder.Append("        ${resultName} = " + input.ID + ";"+ Environment.NewLine);
                stringBuilder.Append("        break;" + Environment.NewLine);
                c++;
            });
            if (theDefault != null)
            {
                stringBuilder.Append("    default:"+ Environment.NewLine);
                stringBuilder.Append("        ${resultName} = " + theDefault.ID + ";"+ Environment.NewLine);
                stringBuilder.Append("        break;" );
            }
            return stringBuilder.ToString();
        }

        protected override Dictionary<string, string> CreateTemplateMap()
        {
            var result =  base.CreateTemplateMap();
            result["check"] = _check.ID;
            return result;
        }

        protected override string SourceTemplate()
        {
            return ShaderNodesUtil.Evaluate(ShaderCode, new Dictionary<string, string>{{"cases", BuildCases(_cases, _default)}});
        }
    }
    
    public class Not: ShaderNode<bool>
    {
        private ShaderNode<bool> _in;

        public Not(NodeContext nodeContext) : base(nodeContext, "not")
        {
            _in = Default;
            
            SetInputs( new List<AbstractShaderNode>{_in});
        }

        public void SetInput(ShaderNode<bool> theIn)
        {
            _in = theIn ?? Default;
            
            SetInputs( new List<AbstractShaderNode>{_in});
        }
        
        protected override string SourceTemplate()
        {
            return ShaderNodesUtil.Evaluate("${resultType} ${resultName} = !${in};", 
                new Dictionary<string, string>
                {
                    {"in", _in.ID},
                });
        }
    }
    
    public class SimpleKeyword : ShaderNode<GpuVoid>, IComputeVoid
    {

        private string _keyword;

        protected SimpleKeyword(NodeContext nodeContext, string theKeyword) : base( nodeContext, "void")
        {
            _keyword = theKeyword;
            SetInputs(new List<AbstractShaderNode>{null});
        }
        
        protected override Dictionary<string, string> CreateTemplateMap()
        {
            return new Dictionary<string, string>();
        }
        
        protected override string GenerateDefaultSource()
        {
            return _keyword;
        }

        protected override string SourceTemplate()
        {
            return _keyword;
        }
    }
    
    public class EmptyVoid : SimpleKeyword
    {
    
        public EmptyVoid(NodeContext nodeContext) : base( nodeContext, "")
        {
        }
    }
    
    public class ReturnVoid : SimpleKeyword
    {
    
        public ReturnVoid(NodeContext nodeContext) : base( nodeContext, "return;")
        {
        }
    }
    
    public class BreakVoid : SimpleKeyword
    {
    
        public BreakVoid(NodeContext nodeContext) : base( nodeContext, "break;")
        {
        }
    }
    
    public class ContinueVoid : SimpleKeyword
    {
    
        public ContinueVoid(NodeContext nodeContext) : base( nodeContext, "continue;")
        {
        }
    }
    
    public class DiscardVoid : SimpleKeyword
    {
    
        public DiscardVoid(NodeContext nodeContext) : base( nodeContext, "discard;")
        {
        }
    }
}