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
        public virtual List<string> Structs => new List<string>();
        public virtual List<IGpuInput> Inputs => new List<IGpuInput>();
        public string ID { get; }
        
        public string SourceCode => GenerateSource(Ins, CustomTemplateValues);

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
        
        private delegate List<Type> GetInfoElement<Type>(AbstractShaderNode theInput);
        
        private  List<Type> GetInfo<Type>(GetInfoElement<Type> theDelegate)
        {
            var result = new HashSet<Type>();
            Trees.ReadOnlyTreeNode.Flatten(this).ForEach(n =>
            {
                if(n is AbstractShaderNode input)result.AddRange(theDelegate(input));
            });
            return result.ToList();
        }

        public List<string> MixinList()
        {
            return GetInfo<string>(input => input.MixIns);
        }
        
        public List<IGpuInput> InputList()
        {
            return GetInfo<IGpuInput>(input => input.Inputs);
        }
        
        public List<string> DeclarationList()
        {
            return GetInfo<string>(input => input.Declarations);
        }
        
        public List<string> StructList()
        {
            return GetInfo<string>(input => input.Structs);
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
        
        protected virtual string GenerateDefaultSource()
        {
            return ShaderNodesUtil.Evaluate(DefaultShaderCode, CreateTemplateMap());
        }

        protected string GenerateSource(IEnumerable<AbstractGpuValue> theIns, IDictionary<string, string> theCustomValues = null)
        {
            
            if (ShaderNodesUtil.HasNullValue(theIns))
            {
                return GenerateDefaultSource();
            }

            var sourceCode = SourceTemplate();
            if (sourceCode.Trim() == "") return "";

            var templateMap = CreateTemplateMap();
            theCustomValues?.ForEach(kv => templateMap.Add(kv.Key, kv.Value));

            return ShaderNodesUtil.Evaluate(sourceCode, templateMap);
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
                {"resultType", Output != null ? Output.TypeName() : TypeHelpers.GetGpuTypeForType<T>()},
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