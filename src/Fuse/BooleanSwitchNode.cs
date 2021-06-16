using System;
using System.Collections.Generic;
using System.Text;
using Fuse.compute;
using Stride.Core.Extensions;

namespace Fuse
{
    public class BooleanSwitchNode<T> : ShaderNode<T>
    {
        private readonly GpuValue<bool> _inCheck;
        private readonly GpuValue<T> _inFalse;
        private readonly GpuValue<T> _inTrue;
        public BooleanSwitchNode(GpuValue<bool> inCheck, GpuValue<T> inFalse, GpuValue<T> inTrue, ConstantValue<T> theDefault = null) : base( "expression", theDefault)
        {

            var ins = new List<AbstractGpuValue>(){inCheck};
            _inCheck = inCheck;
            _inFalse = inFalse;
            _inTrue = inTrue;
            
            if(_inFalse != null)ins.Add(_inFalse);
            if(_inTrue != null)ins.Add(_inTrue);
            if(_inFalse == null && _inTrue == null)ins.Add(null);
            
            Setup(ins);
        }

        private string BuildCaseSource(AbstractGpuValue theCase,StringBuilder theSourceBuilder, HashSet<int> theHashes)
        {
            if (theCase == null) return "";
            if (!_inTrue.ParentNode.SourceCode.IsNullOrEmpty())
            {
                theCase.ParentNode.BuildChildrenSource(theSourceBuilder, theHashes);
                return theCase.ParentNode.SourceCode;
            }
            
            theCase.ParentNode.Children.ForEach(
            child =>
            {
                if (!(child is AbstractShaderNode)) return;
               
                ((AbstractShaderNode)child).BuildChildrenSource(theSourceBuilder, theHashes);
            });

            var childrenString = new StringBuilder();
            theCase.ParentNode.BuildChildrenSource(childrenString, theHashes);
            return childrenString.ToString();
        }
        
        protected internal override void BuildSource(StringBuilder theSourceBuilder, HashSet<int> theHashes)
        {
            if (typeof(T) != typeof(GpuVoid))base.BuildSource(theSourceBuilder, theHashes);

            if (_inCheck == null) return;
            if (_inTrue == null && _inFalse == null) return;
            
            _inCheck.ParentNode.BuildSource(theSourceBuilder, theHashes);

            if (!theHashes.Add(GetHashCode())) return;

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