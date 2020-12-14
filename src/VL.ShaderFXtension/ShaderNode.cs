using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Stride.Core.Extensions;
using Stride.Core.Mathematics;
using VL.Lib.Collections;
using VL.Stride.Shaders.ShaderFX;

namespace VL.ShaderFXtension
{
    public abstract class ShaderNode : IShaderNode
    {
        private string _sourceCode;
        protected OrderedDictionary<string, AbstractGpuValue> _ins;
        private OrderedDictionary<string, AbstractGpuValue> _outs;


        protected ShaderNode(string theId)
        {
            ID = theId;
        }

        protected void Setup(string theSourceCode, OrderedDictionary<string, AbstractGpuValue> theIns, OrderedDictionary<string, AbstractGpuValue> theOuts)
        {
            _sourceCode = theSourceCode;
            _ins = theIns;
            _ins.ForEach(input =>
            {
                switch (input.Value)
                {
                    case ConstantValue<float> _:
                        References.Add(input.Key, new GPUReference<float>(this, input.Key));
                        break;
                    case ConstantValue<Vector2> _:
                        References.Add(input.Key, new GPUReference<Vector2>(this, input.Key));
                        break;
                    case ConstantValue<Vector3> _:
                        References.Add(input.Key, new GPUReference<Vector3>(this, input.Key));
                        break;
                    case ConstantValue<Vector4> _:
                        References.Add(input.Key, new GPUReference<Vector4>(this, input.Key));
                        break;
                }
            });
            _outs = theOuts;
            _outs.ForEach(output =>
            {
                output.Value.ParentNode = this;
            });
        }

        public Dictionary<string, AbstractGPUReference> References { get; set; } = new Dictionary<string, AbstractGPUReference>();

        public virtual string Declaration => "";
        
        public virtual string Functions => "";

        public virtual IEnumerable<string> MixIns => new List<string>();

        public void BuildSource(StringBuilder theStringBuilder, HashSet<int> theHashs)
        {
            _ins.ForEach(input =>
            {
                if (input.Value?.ParentNode == null) return;
                input.Value.ParentNode.BuildSource(theStringBuilder, theHashs);
            });
            if (!_sourceCode.IsNullOrEmpty() && theHashs.Add(GetHashCode()))
            {
                theStringBuilder.AppendLine("        " + _sourceCode);
            }
        }

        public virtual string ReferenceCall(Dictionary<string,AbstractGpuValue> theReplacements)
        {
            return "";
        }
        
        public virtual string ReferenceCallArguments(Dictionary<string,AbstractGpuValue> theReplacements)
        {
            return "";
        }
        public virtual string ReferenceArguments(Dictionary<string,AbstractGpuValue> theReplacements)
        {
            return "";
        }
        public virtual string ReferenceSignature(Dictionary<string,AbstractGpuValue> theReplacements)
        {
            return "";
        }
        
        public string SourceCode()
        {
            var myStringBuilder = new StringBuilder();
            BuildSource(myStringBuilder, new HashSet<int>());
            
            return myStringBuilder.ToString();
        }

        public void BuildDeclarations(Dictionary<int,string> theCBuffer)
        {
            _ins.ForEach(input =>
            {
                if (input.Value?.ParentNode == null) return;
                input.Value.ParentNode.BuildDeclarations(theCBuffer);
            });
            if(!Declaration.IsNullOrEmpty() && !theCBuffer.ContainsKey(GetHashCode()))theCBuffer.Add(GetHashCode(),Declaration);
        }
        
        public string Declarations()
        {
            var myDeclarationBuilder = new Dictionary<int, string>();
            BuildDeclarations(myDeclarationBuilder);
            
            var myDeclarations = new StringBuilder();
            myDeclarationBuilder.ForEach(decl => myDeclarations.AppendLine(decl.Value));
            return myDeclarations.ToString();
        }
        
        public void GetInputs(HashSet<IGPUInput> theInputs)
        {
            _ins.ForEach(input =>
            {
                if (input.Value?.ParentNode == null) return;
                input.Value.ParentNode.GetInputs(theInputs);
            });
            if(this is IGPUInput)theInputs.Add((IGPUInput)this);
        }

        public List<IGPUInput> Inputs()
        {
            var result = new HashSet<IGPUInput>();
            GetInputs(result);
            return result.ToList();
        } 
        
        public void GetMixins(HashSet<string> theMixins)
        {
            _ins.ForEach(input =>
            {
                if (input.Value?.ParentNode == null) return;
                input.Value.ParentNode.GetMixins(theMixins);
            });
            theMixins.AddRange(MixIns);
        }

        public string ID { get; }

        public string Mixins()
        {
            var result = new HashSet<string>();
            GetMixins(result);
            var myBuilder = new StringBuilder();
            result.ForEach(mixin => myBuilder.Append(","+mixin));
            return myBuilder.ToString();
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