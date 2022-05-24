using System;
using System.Collections.Generic;
using System.Text;
using Fuse.compute;
using Stride.Core.Extensions;

namespace Fuse
{
    public class IfNodeDeprecated<T> : ShaderNode<T>
    {
        private readonly ShaderNode<bool> _inCheck;
        private readonly ShaderNode<T> _inFalse;
        private readonly ShaderNode<T> _inTrue;
        public IfNodeDeprecated(ShaderNode<bool> inCheck, ShaderNode<T> inFalse, ShaderNode<T> inTrue, ShaderNode<T> theDefault = null) : base( "bool_switch_result", theDefault)
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

        private string BuildCaseSource(AbstractShaderNode theCase,StringBuilder theSourceBuilder, HashSet<int> theHashes)
        {
            if (theCase == null) return "";
            if (_inTrue != null && !_inTrue.SourceCode.IsNullOrEmpty())
            {
                theCase.BuildChildrenSource(theSourceBuilder, theHashes);
                return theCase.SourceCode;
            }
            
            theCase.Children.ForEach(
            child =>
            {
                if (!(child is AbstractShaderNode node)) return;
               
                node.BuildChildrenSource(theSourceBuilder, theHashes);
            });

            var childrenString = new StringBuilder();
            theCase.BuildChildrenSource(childrenString, theHashes);
            return childrenString.ToString();
        }
        
        protected internal override void BuildSource(StringBuilder theSourceBuilder, HashSet<int> theHashes)
        {
            if (typeof(T) != typeof(GpuVoid))base.BuildSource(theSourceBuilder, theHashes);

            if (_inCheck == null) return;
            if (_inTrue == null && _inFalse == null) return;
            
            _inCheck.BuildSource(theSourceBuilder, theHashes);

            if (!theHashes.Add(HashCode)) return;

            const string shaderCode = @"
        if(${check}){
            ${inTrue}
        }else{
            ${inFalse}
        }
        ";
                
            theSourceBuilder.Append(ShaderNodesUtil.Evaluate(shaderCode,new Dictionary<string, string>()
            {
                {"check", _inCheck.ID},
                {"inFalse", BuildCaseSource(_inFalse, theSourceBuilder, theHashes)},
                {"inTrue", BuildCaseSource(_inTrue, theSourceBuilder, theHashes)}
            }));

            theSourceBuilder.Append(Environment.NewLine);
        }

        protected override string SourceTemplate()
        {
            if (typeof(T) == typeof(GpuVoid))
            {
                return "";
            }
               
            var shaderCode = @"
        ${resultType} ${resultName}; 
        if(${check}){
            ${inTrue}
        }else{
            ${inFalse}
        }
        ";
            var trueTemplate = "";
            var falseTemplate = "";
            var templateMap = new Dictionary<string, string>();

            if (_inTrue != null)
            {
                trueTemplate = "${resultName} = ${inTrue};";
                templateMap["inTrue"] = _inTrue.ID;
            }

            if (_inFalse != null)
            {
                falseTemplate = "${resultName} = ${inFalse};";
                templateMap["inFalse"] = _inFalse.ID;
            }
            
            shaderCode = ShaderNodesUtil.Evaluate(shaderCode,new Dictionary<string, string>()
            {
                {"check", _inCheck.ID},
                {"inFalse", falseTemplate},
                {"inTrue", trueTemplate}
            });
            
            return ShaderNodesUtil.Evaluate(shaderCode,templateMap);
        }
    }
}