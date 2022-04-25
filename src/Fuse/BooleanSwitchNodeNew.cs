using System;
using System.Collections.Generic;
using System.Text;
using Fuse.compute;
using Stride.Core.Extensions;

namespace Fuse
{
    public class BooleanSwitchNodeNew<T> : ShaderNode<T>
    {
        private readonly ShaderNode<bool> _inCheck;
        private readonly ShaderNode<T> _inFalse;
        private readonly ShaderNode<T> _inTrue;
        public BooleanSwitchNodeNew(ShaderNode<bool> inCheck, ShaderNode<T> inFalse, ShaderNode<T> inTrue, ShaderNode<T> theDefault = null) : base( "expression", theDefault)
        {

            var ins = new List<AbstractShaderNode>(){inCheck};
            _inCheck = inCheck;
            _inFalse = inFalse;
            _inTrue = inTrue;
            
            if(_inFalse != null)ins.Add(_inFalse);
            if(_inTrue != null)ins.Add(_inTrue);
            if(_inFalse == null && _inTrue == null)ins.Add(null);
            
            Setup(ins);
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
}