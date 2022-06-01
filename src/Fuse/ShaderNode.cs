using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Fuse.ShaderFX;
using Stride.Rendering.Materials;
using Stride.Shaders;
using VL.Core;
using VL.Lib.Collections;
using VL.Stride.Shaders.ShaderFX;


namespace Fuse
{
    public interface IAtomicComputeNode: Trees.IReadOnlyTreeNode, IComputeNode
    {

    }

    public class FieldDeclaration
    {
        private readonly string _computeShaderDeclaration;
        private readonly string _declaration;

        public FieldDeclaration(bool isResource, string computeShaderDeclaration, string declaration)
        {
            IsResource = isResource;
            _computeShaderDeclaration = computeShaderDeclaration;
            _declaration = declaration;
        }

        public FieldDeclaration(string declaration)
        {
            _computeShaderDeclaration = declaration;
            _declaration = declaration;
        }

        public string GetDeclaration(bool theIsComputeShader)
        {
            return theIsComputeShader && _computeShaderDeclaration != null ? _computeShaderDeclaration : _declaration;
        }

        public override string ToString()
        {
            return _declaration;
        }

        public bool IsResource { get; }
    }

    public class ShaderTree : Trees.IReadOnlyTreeNode
    {

        public readonly AbstractShaderNode Node;
        public ShaderTree(AbstractShaderNode theNode)
        {
            Node = theNode;
        }
        
        public IReadOnlyList<Trees.IReadOnlyTreeNode> Children
        {
            get
            {
                var result = new List<Trees.IReadOnlyTreeNode>();
                
                Node.Ins.ForEach(input =>
                {
                    if(input != null)result.Add(input.Tree);
                });
                return result;
            }
        }
    }
    
    public abstract class AbstractShaderNode : IComputeNode
    {
        public List<AbstractShaderNode> Ins = new List<AbstractShaderNode>();
        
        public  string Name{ get;  set; }

        public int HashCode;
        
        public abstract string ID { get; }

        public abstract string TypeName(); 

        protected abstract string SourceTemplate();

        public abstract int Dimension();

        public readonly ShaderTree Tree;

        protected AbstractShaderNode(string theId)
        {
            Name = theId;
            HashCode = -1;

            Tree = new ShaderTree(this);
        }
        
        public virtual IDictionary<string,string> Functions => new Dictionary<string, string>();

        public IReadOnlyList<Trees.IReadOnlyTreeNode> Children => Tree.Children;

        private const string DefaultShaderCode = "${resultType} ${resultName} = ${default};";
        
        public string SourceCode => GenerateSource( Ins);
        
        protected virtual Dictionary<string, string> CreateTemplateMap()
        {
            return new Dictionary<string, string>();
        }
        
        protected virtual string GenerateDefaultSource()
        {
            return ShaderNodesUtil.Evaluate(DefaultShaderCode, CreateTemplateMap());
        }

        private string GenerateSource(IEnumerable<AbstractShaderNode> theIns)
        {
            
            if (ShaderNodesUtil.HasNullValue(theIns))
            {
                return GenerateDefaultSource();
            }

            var sourceCode = SourceTemplate();
            return sourceCode.Trim() == "" ? "" : ShaderNodesUtil.Evaluate(sourceCode, CreateTemplateMap());
        }

        public virtual void OnGenerateCode(ShaderGeneratorContext theContext)
        {
            
        }

        public virtual void SetHashCodes(ShaderGeneratorContext theContext)
        {
            HashCode = theContext.GetAndIncIDCount();
            
            Ins.ForEach(input =>
            {
                if (input == null) return;
                
                input.SetHashCodes(theContext);
            });

            OnGenerateCode(theContext);
        }

        public void BuildChildrenSource( StringBuilder theSourceBuilder, HashSet<int> theHashes)
        {
            Children.ForEach(child =>
            {
                if (!(child is ShaderTree input)) return;
               
                input.Node.BuildSource( theSourceBuilder, theHashes);
            });
        }

        protected internal virtual void BuildSource(StringBuilder theSourceBuilder, HashSet<int> theHashes)
        {
            BuildChildrenSource(theSourceBuilder, theHashes);

            var source = SourceCode;
            if (!string.IsNullOrWhiteSpace(source) && theHashes.Add(HashCode))
            {
                theSourceBuilder.Append("        " + source + Environment.NewLine);
            }
        }
        
        

        public IEnumerable<IComputeNode> GetChildren(object context = null)
        {
            return null;//Children;
        }

        public ShaderSource GenerateShaderSource(ShaderGeneratorContext context, MaterialComputeColorKeys baseKeys)
        {
            return null; //throw new NotImplementedException();
        }
       
        public string BuildSourceCode()
        {
            var myStringBuilder = new StringBuilder();
            var myHashes = new HashSet<int>();

            BuildSource( myStringBuilder, myHashes);
            
            return myStringBuilder.ToString();
        }

        public List<TNode> ChildrenOfType<TNode>()
        {
            var result = new HashSet<TNode>();
            Trees.ReadOnlyTreeNode.Flatten(Tree).ForEach(n =>
            {
                if (n is ShaderTree {Node: TNode input})
                {
                    result.Add(input);
                }
            });
            return result.ToList();
        }
        
        public List<IFunctionParameter> Delegates()
        {
            return ChildrenOfType<IFunctionParameter>();
        }
        
        private delegate List<T> GetInfoElement<T>(AbstractShaderNode theInput);
        
        private  List<T> GetInfo<T>(GetInfoElement<T> theDelegate)
        {
            var result = new HashSet<T>();
            Trees.ReadOnlyTreeNode.Flatten(Tree).ForEach(n =>
            {
                if(n is ShaderTree tree)result.AddRange(theDelegate(tree.Node));
            });
            return result.ToList();
        }
        
        public List<TResourceType> ResourceForTree<TResourceType>(string theResourceId)
        {
            var result = new HashSet<TResourceType>();
            Trees.ReadOnlyTreeNode.Flatten(Tree).ForEach(n =>
            {
                if (!(n is ShaderTree tree) || !tree.Node.Resources.ContainsKey(theResourceId)) return;
                Stride.Core.Extensions.EnumerableExtensions.ForEach<TResourceType>(tree.Node.Resources[theResourceId], i => result.Add(i));

            });
            return result.ToList();
        }

        public List<string> ResourceIdsForTree()
        {
            var result = new HashSet<string>();
            Trees.ReadOnlyTreeNode.Flatten(Tree).ForEach(n =>
            {
                if (!(n is ShaderTree tree)) return;
                tree.Node.Resources.ForEach(kv =>
                {
                    if (result.Add(kv.Key)) return;
                });
            });
            return result.ToList();
        }
        
        public Dictionary<string, IList> ResourcesForTree()
        {
            var result = new Dictionary<string, IList>();
            Trees.ReadOnlyTreeNode.Flatten(Tree).ForEach(n =>
            {
                if (!(n is ShaderTree tree)) return;
                tree.Node.Resources.ForEach(kv =>
                {
                    if (result.ContainsKey(kv.Key)) return;
                    
                    var list = new ArrayList();
                    ResourceForTree<object>(kv.Key).ForEach(i => list.Add(i));
                    result[kv.Key] = list;
                });
            });
            return result;
        }
        
        public Dictionary<string, List<TResource>> ResourcesForTree<TResource>()
        {
            var result = new Dictionary<string, List<TResource>>();
            Trees.ReadOnlyTreeNode.Flatten(Tree).ForEach(n =>
            {
                if (!(n is ShaderTree tree)) return;
                tree.Node.Resources.ForEach(kv =>
                {
                    var values = kv.Value.OfType<TResource>();
                    if (values.IsEmpty()) return;
                    if (!result.ContainsKey(kv.Key))
                    {
                        result[kv.Key] = new List<TResource>();
                    }
                    values.ForEach(v => result[kv.Key].Add(v));
                });
            });
            return result;
        }

        public Dictionary<string, IList> Resources { get; } = new Dictionary<string, IList>();

        public void AddResource(string theResourceId, object theResource)
        {
            if (!Resources.ContainsKey(theResourceId))
            {
                Resources[theResourceId] = new ArrayList();
            }

            Resources[theResourceId].Add(theResource);
        }
        
        public void AddResources(string theResourceId, IList theResources)
        {
            if (!Resources.ContainsKey(theResourceId))
            {
                Resources[theResourceId] = new ArrayList();
            }

            Stride.Core.Extensions.EnumerableExtensions.ForEach<object>(theResources, r => Resources[theResourceId].Add(r));

        }

        protected const string Mixins = "Mixins";
        protected const string Inputs = "Inputs";
        protected const string Compositions = "Compositions";
        protected const string Declarations = "Declarations";
        protected const string Structs = "Structs";

        public List<string> MixinList()
        {
            return ResourceForTree<string>(Mixins);
        }
        
        public List<IGpuInput> InputList()
        {
            return ResourceForTree<IGpuInput>(Inputs);
        }
        
        public List<string> CompositionList()
        {
            return ResourceForTree<string>(Compositions);
        }
        
        public List<FieldDeclaration> DeclarationList()
        {
            return ResourceForTree<FieldDeclaration>(Declarations);
        }
        
        public List<string> StructList()
        {
            return ResourceForTree<string>(Structs);
        }
        
        public Dictionary<string,string> FunctionMap(){
       
            var result = new Dictionary<string,string>();
            Trees.ReadOnlyTreeNode.Flatten(Tree).ForEach(n =>
            {
                if (!(n is ShaderTree tree)) return;
                
                tree.Node.Functions?.ForEach(kv =>
                {
                    if (result.ContainsKey(kv.Key)) return;
                    
                    result.Add(kv.Key, kv.Value);
                });
                
            });
            return result;
        }
    }
    
    [Monadic(typeof(ShaderNodeMonadicFactory<>))]
    public class ShaderNode<T> : AbstractShaderNode , IComputeValue<T>
    {

        public List<AbstractShaderNode> OptionalOutputs { get; protected set; }

        private readonly ShaderNode<T> _default;
        public  ShaderNode<T> Default => _default ?? ConstantHelper.FromFloat<T>(0);

        public ShaderNode(string theId, ShaderNode<T> theDefault = null) : base(theId)
        {
            _default = theDefault;
        }

        protected override Dictionary<string, string> CreateTemplateMap()
        {
            return new Dictionary<string, string>
            {
                {"resultName", ID},
                {"resultType", TypeName()},
                {"default", Default == null ? "": Default.ID},
                {"arguments", ShaderNodesUtil.BuildArguments(Ins)}
            };
        }

        protected void SetInputs(IEnumerable<AbstractShaderNode> theIns)
        {
            Ins = new List<AbstractShaderNode>();
                
            theIns.ForEach(input =>
            {
                if(input != null)Ins.Add(input);
            });
        }
        
        public string TypeOverride { get; set; }
        
        public override string TypeName()
        {
            if (typeof(T) == typeof(GpuStruct))
            {
                return TypeOverride;
            }
            return TypeHelpers.GetGpuTypeForType<T>();
        }

        protected override string SourceTemplate()
        {
            return "";
        }

        public override int Dimension()
        {
            return TypeHelpers.GetDimension<T>();
        }

        public override string ID => Name + "_" + HashCode;
    }
}