using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Stride.Core.Extensions;
using VL.Lib.Collections;

namespace VL.ShaderFXtension
{
    public abstract class ShaderNode : IShaderNode
    {
        private string _sourceCode;
        private Dictionary<string, AbstractGPUValue> _ins;
        private Dictionary<string, AbstractGPUValue> _outs;

        protected ShaderNode()
        {
            
        }

        protected void Setup(string theSourceCode, Dictionary<string, AbstractGPUValue> theIns, Dictionary<string, AbstractGPUValue> theOuts)
        {
            _sourceCode = theSourceCode;
            _ins = theIns;
            _outs = theOuts;
            _outs.ForEach(output =>
            {
                output.Value.ParentNode = this;
            });
        }

        public virtual string Declaration => "";

        public string SourceCode()
        {
            var myStringBuilder = new StringBuilder();
            BuildSource(myStringBuilder);
            return myStringBuilder.ToString();
        }
        
        public string Declarations()
        {
            var myDeclarationBuilder = new Dictionary<int, string>();
            BuildDeclarations(myDeclarationBuilder);
            
            var myDeclarations = new StringBuilder();
            myDeclarationBuilder.ForEach(decl => myDeclarations.AppendLine(decl.Value));
            return myDeclarations.ToString();
        }

        public void BuildSource(StringBuilder theStringBuilder)
        {
            _ins.ForEach(input =>
            {
                if (input.Value == null || input.Value.ParentNode == null) return;
                input.Value.ParentNode.BuildSource(theStringBuilder);
            });
            if(!_sourceCode.IsNullOrEmpty())theStringBuilder.AppendLine(_sourceCode);
        }
        
        public void BuildDeclarations(Dictionary<int,string> theCBuffer)
        {
            _ins.ForEach(input =>
            {
                if (input.Value.ParentNode == null) return;
                input.Value.ParentNode.BuildDeclarations(theCBuffer);
            });
            if(!Declaration.IsNullOrEmpty() && !theCBuffer.ContainsKey(GetHashCode()))theCBuffer.Add(GetHashCode(),Declaration);
        }

        public IReadOnlyList<Trees.IReadOnlyTreeNode> Children
        {
            get
            {
                var result = new List<Trees.IReadOnlyTreeNode>();
                _ins.ForEach(input =>
                {
                    if(input.Value.ParentNode != null)result.Add(input.Value.ParentNode);
                });
                return result;
            }
        }
    }
}