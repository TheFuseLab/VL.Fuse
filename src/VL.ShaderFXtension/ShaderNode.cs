﻿using System;
using System.Collections.Generic;
 using System.Diagnostics;
 using System.Text;
using Stride.Core.Extensions;
using Stride.Core.Mathematics;
using VL.Lib.Collections;

 namespace VL.ShaderFXtension
{
    public interface IShaderNode: Trees.IReadOnlyTreeNode
    {

        string ID { get;  }
    }
    
    public abstract class AbstractShaderNode : IShaderNode
    {
        protected string _sourceCode;
        protected OrderedDictionary<string, AbstractGpuValue> Ins;


        protected AbstractShaderNode(string theId)
        {
            ID = theId;
        }

        

        public Dictionary<string, AbstractGPUReference> References { get; set; } = new Dictionary<string, AbstractGPUReference>();

        public virtual string Declaration => "";
        public virtual IDictionary<string,string> Functions => new Dictionary<string, string>();

        public virtual IEnumerable<string> MixIns => new List<string>();
        
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

        private void BuildSource(StringBuilder theSourceBuilder, HashSet<int> theHashes)
        {
            Children.ForEach(child =>
            {
                if (!(child is AbstractShaderNode input)) return;
               
                ((AbstractShaderNode)child).BuildSource(theSourceBuilder, theHashes);
            });
            if (!_sourceCode.IsNullOrEmpty() && theHashes.Add(GetHashCode()))
            {
                theSourceBuilder.Append("        " + _sourceCode + Environment.NewLine);
            }
        }
       
        public string SourceCode()
        {
            var myStringBuilder = new StringBuilder();
            var myHashes = new HashSet<int>();

            BuildSource(myStringBuilder, myHashes);
            /*
            Trees.ReadOnlyTreeNode.Traverse(this, n =>
            {
                if (!(n is ShaderNode input)) return Trees.TraverseAllChilds;
                if (!_sourceCode.IsNullOrEmpty() && myHashes.Add(input.GetHashCode()))
                {
                    myStringBuilder.Insert(0,"        " + input._sourceCode + Environment.NewLine);
                }

                return Trees.TraverseAllChilds;
            }, out _, out _);
            */
            return myStringBuilder.ToString();
        }

        public string Declarations()
        {
            var result = new HashSet<AbstractShaderNode>();
            var myDeclarations = new StringBuilder();
            Trees.ReadOnlyTreeNode.Flatten(this).ForEach(n =>
            {
                if (!(n is AbstractShaderNode input)) return;
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
            return System.Linq.Enumerable.ToList(result);
        }

        public string BuildMixIns()
        {
            var result = new HashSet<string>();
            Trees.ReadOnlyTreeNode.Flatten(this).ForEach(n =>
            {
                if(n is AbstractShaderNode input)result.AddRange(input.MixIns);
            });
            
            var myBuilder = new StringBuilder();
            result.ForEach(mixin => myBuilder.Append(","+mixin));
            return myBuilder.ToString();
            
        }

        public string BuildFunctions(){
       
            var result = new HashSet<string>();
            var myBuilder = new StringBuilder();
            Trees.ReadOnlyTreeNode.Flatten(this).ForEach(n =>
            {
                if (!(n is AbstractShaderNode input)) return;
                
                input.Functions?.ForEach(kv =>
                {
                    if(result.Add(kv.Key))myBuilder.Append(kv.Value);
                });
                
            });
            return myBuilder.ToString();
        }

        public IReadOnlyList<Trees.IReadOnlyTreeNode> Children
        {
            get
            {
                var result = new List<Trees.IReadOnlyTreeNode>();
                
                Ins.ForEach(input =>
                {
                    if(input.Value?.ParentNode != null)result.Add(input.Value.ParentNode);
                });
                return result;
            }
        }

        
    }
    
    public class ShaderNode<T> : AbstractShaderNode
    {
        public  GpuValue<T> Output { get; protected set; }
        public  ConstantValue<T> Default { get; protected set; }

        protected ShaderNode(string theId, ConstantValue<T> theDefault = null,string outputName = "result") : base(theId)
        {
            Default = theDefault ?? new ConstantValue<T>(0);
            Output = new GpuValue<T>(outputName);
        }
        
        protected void Setup(string theSourceCode, OrderedDictionary<string, AbstractGpuValue> theIns)
        {
            _sourceCode = theSourceCode;
            Ins = theIns;
            Ins.ForEach(input =>
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
           
            Output.ParentNode = this;
        }
    }
}