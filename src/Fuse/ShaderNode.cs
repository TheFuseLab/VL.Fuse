using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Stride.Core.Extensions;
using VL.Lib.Collections;

namespace Fuse
{
    public interface IShaderNode: Trees.IReadOnlyTreeNode
    {

        string ID { get;  }
    }
    
    public abstract class AbstractShaderNode : IShaderNode
    {
        protected IEnumerable<AbstractGpuValue> Ins;

        protected abstract string SourceTemplate();

        protected IDictionary<string, string> CustomTemplateValues;


        protected AbstractShaderNode(string theId)
        {
            ID = theId;
        }

        public virtual IDictionary<string,string> Functions => new Dictionary<string, string>();

        public virtual List<string> MixIns => new List<string>();
        public virtual List<string> Declarations => new List<string>();
        public virtual List<IGpuInput> Inputs => new List<IGpuInput>();
        public string ID { get; }
        
        public string SourceCode => GenerateSource(SourceTemplate(), Ins, CustomTemplateValues);

        private void BuildSource(StringBuilder theSourceBuilder, HashSet<int> theHashes)
        {
            Children.ForEach(child =>
            {
                if (!(child is AbstractShaderNode input)) return;
               
                ((AbstractShaderNode)child).BuildSource(theSourceBuilder, theHashes);
            });
            if (!SourceCode.IsNullOrEmpty() && theHashes.Add(GetHashCode()))
            {
                theSourceBuilder.Append("        " + SourceCode + Environment.NewLine);
            }
        }
       
        public string BuildSourceCode()
        {
            var myStringBuilder = new StringBuilder();
            var myHashes = new HashSet<int>();

            BuildSource(myStringBuilder, myHashes);
            
            return myStringBuilder.ToString();
        }
        
        public List<IDelegateParameter> Delegates()
        {
            var result = new HashSet<IDelegateParameter>();
            Trees.ReadOnlyTreeNode.Flatten(this).ForEach(n =>
            {
                if(n is IDelegateParameter input)result.Add(input);
            });
            return result.ToList();
        }

        public List<string> MixinList()
        {
            var result = new List<string>();
            Trees.ReadOnlyTreeNode.Flatten(this).ForEach(n =>
            {
                if(n is AbstractShaderNode input)result.AddRange(input.MixIns);
            });
            return result;
        }
        
        public List<IGpuInput> InputList()
        {
            var result = new HashSet<IGpuInput>();
            Trees.ReadOnlyTreeNode.Flatten(this).ForEach(n =>
            {
                if(n is AbstractShaderNode input)result.AddRange(input.Inputs);
            });
            return result.ToList();
        }
        
        public List<string> DeclarationList()
        {
            
            var result = new List<string>();
            Trees.ReadOnlyTreeNode.Flatten(this).ForEach(n =>
            {
                if(n is AbstractShaderNode input)result.AddRange(input.Declarations);
            });
            return result;
        }
        
        public Dictionary<string,string> FunctionMap(){
       
            var result = new Dictionary<string,string>();
            Trees.ReadOnlyTreeNode.Flatten(this).ForEach(n =>
            {
                if (!(n is AbstractShaderNode input)) return;
                
                input.Functions?.ForEach(kv =>
                {
                    if (result.ContainsKey(kv.Key)) return;
                    
                    result.Add(kv.Key, kv.Value);
                });
                
            });
            return result;
        }

        public IReadOnlyList<Trees.IReadOnlyTreeNode> Children
        {
            get
            {
                var result = new List<Trees.IReadOnlyTreeNode>();
                
                Ins.ForEach(input =>
                {
                    if(input?.ParentNode != null)result.Add(input.ParentNode);
                });
                return result;
            }
        }
        
        
        protected const string DefaultShaderCode = "${resultType} ${resultName} = ${default};";
        
        protected virtual Dictionary<string, string> CreateTemplateMap()
        {
            return new Dictionary<string, string>();
        }
        
        private string GenerateDefaultSource()
        {
            return ShaderTemplateEvaluator.Evaluate(DefaultShaderCode, CreateTemplateMap());
        }

        protected string GenerateSource(string theSourceCode, IEnumerable<AbstractGpuValue> theIns, IDictionary<string, string> theCustomValues = null)
        {
            if (theSourceCode.Trim() == "") return "";
            if (ShaderNodesUtil.HasNullValue(theIns))
            {
                return GenerateDefaultSource();
            }

            var templateMap = CreateTemplateMap();
            theCustomValues?.ForEach(kv => templateMap.Add(kv.Key, kv.Value));

            return ShaderTemplateEvaluator.Evaluate(theSourceCode, templateMap);
        }
        
    }
    
    public abstract class ShaderNode<T> : AbstractShaderNode
    {
        public  GpuValue<T> Output { get; protected set; }
        public  ConstantValue<T> Default { get; protected set; }

        protected ShaderNode(string theId, ConstantValue<T> theDefault = null,string outputName = "result") : base(theId)
        {
            Default = theDefault ?? ConstantHelper.FromFloat<T>(0);
            Output = new GpuValue<T>(outputName);
        }

        protected override Dictionary<string, string> CreateTemplateMap()
        {
            return new Dictionary<string, string>
            {
                {"resultName", Output.ID},
                {"resultType", Output != null ? Output.TypeName() : TypeHelpers.GetNameForType<T>().ToLower()},
                {"default", Default == null ? "": Default.ID},
                {"arguments", ShaderNodesUtil.BuildArguments(Ins)}
            };
        }

        protected void Setup(IEnumerable<AbstractGpuValue> theIns, IDictionary<string, string> theCustomValues = null)
        {
            CustomTemplateValues = theCustomValues;
            Ins = theIns.ToList();
            Output.ParentNode = this;
        }
    }
}