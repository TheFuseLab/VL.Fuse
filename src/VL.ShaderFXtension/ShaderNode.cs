using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        public virtual string Function => "";

        public virtual IEnumerable<string> MixIn => new List<string>();
        
        public string ID { get; }

        

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
            var myHashes = new HashSet<int>();
            ShaderNode  myOut = null;
            Trees.ITraverseCommand  myOut2 = null;
            Trees.ReadOnlyTreeNode.Traverse(this, n =>
            {
                if (!(n is ShaderNode input)) return Trees.TraverseAllChilds;
                if (!_sourceCode.IsNullOrEmpty() && myHashes.Add(input.GetHashCode()))
                {
                    myStringBuilder.Insert(0,"        " + input._sourceCode + System.Environment.NewLine);
                }

                return Trees.TraverseAllChilds;
            }, out myOut, out myOut2);
            
            return myStringBuilder.ToString();
        }

        public string Declarations()
        {
            var result = new HashSet<ShaderNode>();
            var myDeclarations = new StringBuilder();
            Trees.ReadOnlyTreeNode.Flatten(this).ForEach(n =>
            {
                if (!(n is ShaderNode input)) return;
                if (!input.Declaration.IsNullOrEmpty() && result.Add(input))
                    myDeclarations.AppendLine(input.Declaration);
            });
            return myDeclarations.ToString();
        }

        public List<IGPUInput> Inputs()
        {
            var result = new HashSet<IGPUInput>();
            Trees.ReadOnlyTreeNode.Flatten(this).ForEach(n =>
            {
                if(n is IGPUInput input)result.Add(input);
            });
            return result.ToList();
        }

        public string MixIns()
        {
            var result = new HashSet<string>();
            Trees.ReadOnlyTreeNode.Flatten(this).ForEach(n =>
            {
                if(n is ShaderNode input)result.AddRange(input.MixIn);
            });
            
            var myBuilder = new StringBuilder();
            result.ForEach(mixin => myBuilder.Append(","+mixin));
            return myBuilder.ToString();
            
        }

        public string Functions(){
       
            var result = new HashSet<string>();
            var myBuilder = new StringBuilder();
            Trees.ReadOnlyTreeNode.Flatten(this).ForEach(n =>
            {
                if(n is ShaderNode input && result.Add(input.Function))myBuilder.Append(input.Function);
            });
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