using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using Fuse.ShaderFX;
using Stride.Rendering.Materials;
using Stride.Rendering.Materials.ComputeColors;
using Stride.Shaders;
using VL.Core;
using VL.Lib.Collections;
using VL.Stride.Shaders.ShaderFX;


namespace Fuse
{
    public interface IAtomicComputeNode: Trees.IReadOnlyTreeNode, IComputeNode
    {

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



    public interface IChangeGraph
    {
        public void ChangeGraph(AbstractShaderNode theNode);
    }

    public interface ICompileGraph
    {
        public void CompileGraph(AbstractShaderNode theNode);
    }

    public interface IPrepareGraph
    {
        public void PrepareGraph(AbstractShaderNode theNode);
    }

    public enum HandleNullInputMode
    {
        ReplaceWithDefault,
        Remove,
        SetOff
    }

    
    public abstract class AbstractShaderNode : IComputeNode
    {
        public List<AbstractShaderNode> Ins = new();
        // Used for out parameters to trigger code generation

        public readonly List<AbstractShaderNode> Outs = new();
        
        public readonly List<IChangeGraph> ChangeGraphListener = new();
        public readonly List<IPrepareGraph> PrepareGraphListener = new();
        public readonly List<ICompileGraph> CompileGraphListener = new();

        public readonly NodeContext NodeContext;

        public void AddChangeGraph(IChangeGraph theDelegate)
        {
            ChangeGraphListener.Add(theDelegate);
        }
        
        public void RemoveChangeGraph(IChangeGraph theDelegate)
        {
            ChangeGraphListener.Remove(theDelegate);
        }
        
        public void AddPrepareGraph(IPrepareGraph theDelegate)
        {
            PrepareGraphListener.Add(theDelegate);
        }
        
        public void RemovePrepareGraph(IPrepareGraph theDelegate)
        {
            PrepareGraphListener.Remove(theDelegate);
        }
        
        public void AddCompileGraph(ICompileGraph theDelegate)
        {
            CompileGraphListener.Add(theDelegate);
        }
        
        public void RemoveCompileGraph(ICompileGraph theDelegate)
        {
            CompileGraphListener.Remove(theDelegate);
        }
        
        
        public  string Name{ get;  set; }

        public abstract AbstractShaderNode AbstractDefault { get; }

        public readonly uint HashCode;
        
        public abstract string ID { get; }

        public abstract string TypeName(); 

        protected abstract string SourceTemplate();

        public abstract int Dimension();

        public readonly ShaderTree Tree;

        protected AbstractShaderNode(NodeContext nodeContext, string theId)
        {
            NodeContext = nodeContext;
            Name = theId;
            HashCode = ShaderNodesUtil.GetHashCode(nodeContext);

            Tree = new ShaderTree(this);
        }
        
        public virtual IDictionary<string,string> Functions
        {
            get => new Dictionary<string, string>();
            protected set => throw new NotImplementedException();
        }

        public IReadOnlyList<Trees.IReadOnlyTreeNode> Children => Tree.Children;

        private const string DefaultShaderCode = "${resultType} ${resultName} = ${default};";

        
        public void CallChangeEvent()
        {
            foreach (var listener in new List<IChangeGraph>(ChangeGraphListener))
            {
                listener.ChangeGraph(this);
            }
            
            foreach (var output in new List<AbstractShaderNode>(Outs))
            {
                output.CallChangeEvent();
            }
        }

        public void CallCompileGraph()
        {
            foreach (var listener in new List<ICompileGraph>(CompileGraphListener))
            {
                listener.CompileGraph(this);
            }
            
            foreach (var input in Ins)
            {
                input.CallCompileGraph();
            }
        }

        public void CallPrepareGraph()
        {
            foreach (var listener in PrepareGraphListener)
            {
                listener.PrepareGraph(this);
            }
            
            foreach (var input in Ins)
            {
                input.CallPrepareGraph();
            }
        }

        protected HandleNullInputMode NullInputMode = HandleNullInputMode.SetOff;
        
        private bool _hasNullInput;

        protected void SetInputs(IEnumerable<AbstractShaderNode> theIns, bool theCallChangeEvent = true)
        {
            if (Ins.SequenceEqual(theIns)) return;

            foreach (var input in Ins)
            {
                input.Outs.Remove(this);
            }

            _hasNullInput = false;
            Ins = new List<AbstractShaderNode>();
            theIns.ForEach(input =>
            {
                switch (NullInputMode)
                {
                    case HandleNullInputMode.SetOff:
                        if (input != null) Ins.Add(input);
                        else _hasNullInput = true;
                        break;
                    case HandleNullInputMode.ReplaceWithDefault:
                        if (input == null) Ins.Add(AbstractDefault);
                        break;
                    case HandleNullInputMode.Remove:
                        if (input != null) Ins.Add(input);
                        break;
                }
                
            });
            
            foreach (var input in Ins)
            {
                input.Outs.Add(this);
            }
            
            if(theCallChangeEvent)CallChangeEvent();
        }

        public void AddInput(AbstractShaderNode theInput)
        {
            if(theInput != null)Ins.Add(theInput);
        }
        
        public string SourceCode => GenerateSource( Ins);

        public string ShaderCode { get; set; }

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
            
            if (_hasNullInput)
            {
                return GenerateDefaultSource();
            }

            var sourceCode = SourceTemplate();
            return sourceCode.Trim() == "" ? "" : ShaderNodesUtil.Evaluate(sourceCode, CreateTemplateMap());
        }

        protected virtual void OnPassContext(ShaderGeneratorContext nodeContext)
        {
            
        }

        private readonly HashSet<ShaderGeneratorContext> _contexts = new HashSet<ShaderGeneratorContext>();

        public virtual void CheckContext(ShaderGeneratorContext nodeContext)
        {
            
            Ins.ForEach(input =>
            {
                if (input == null) return;
                input.CheckContext(nodeContext);
            });

            if (!_contexts.Add(nodeContext)) return;
            OnPassContext(nodeContext);
        }

        public virtual void BuildChildrenSource( StringBuilder theSourceBuilder, HashSet<uint> theHashes)
        {
            //Console.WriteLine(Name);
            foreach (var child in Children)
            {
                if (child is not ShaderTree input)
                {
                    return;
                }
               
                input.Node.BuildSource( theSourceBuilder, theHashes);
            }
        }

        protected internal virtual void BuildSource(StringBuilder theSourceBuilder, HashSet<uint> theHashes)
        {
            BuildChildrenSource(theSourceBuilder, theHashes);

            if (!theHashes.Add(HashCode))return;
            
            var source = SourceCode;
            //Console.Out.WriteLine(Name + " : " + HashCode);
            if (!string.IsNullOrWhiteSpace(source))
            {
                theSourceBuilder.Append("        " + source + Environment.NewLine);
            }
        }
        
        

        public virtual IEnumerable<IComputeNode> GetChildren(object context = null)
        {
            return Enumerable.Empty<ComputeNode>();
        }

        public virtual ShaderSource GenerateShaderSource(ShaderGeneratorContext context, MaterialComputeColorKeys baseKeys)
        {
            return null; //throw new NotImplementedException();
        }
       
        public string BuildSourceCode()
        {
            var myStringBuilder = new StringBuilder();
            var myHashes = new HashSet<uint>();

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
        
        public List<TPropertyType> PropertyForTree<TPropertyType>(string theThePropertyId)
        {
            var result = new HashSet<TPropertyType>();
            Trees.ReadOnlyTreeNode.Flatten(Tree).ForEach(n =>
            {
                if (!(n is ShaderTree tree) || !tree.Node.Property.ContainsKey(theThePropertyId)) return;
                Stride.Core.Extensions.EnumerableExtensions.ForEach<TPropertyType>(tree.Node.Property[theThePropertyId], i => result.Add(i));

            });
            return result.ToList();
        }

        public List<string> PropertyIdsForTree()
        {
            var result = new HashSet<string>();
            Trees.ReadOnlyTreeNode.Flatten(Tree).ForEach(n =>
            {
                if (!(n is ShaderTree tree)) return;
                tree.Node.Property.ForEach(kv =>
                {
                    if (result.Add(kv.Key)) return;
                });
            });
            return result.ToList();
        }
        
        public Dictionary<string, IList> PropertiesForTree()
        {
            var result = new Dictionary<string, IList>();
            Trees.ReadOnlyTreeNode.Flatten(Tree).ForEach(n =>
            {
                if (!(n is ShaderTree tree)) return;
                tree.Node.Property.ForEach(kv =>
                {
                    if (result.ContainsKey(kv.Key)) return;
                    
                    var list = new ArrayList();
                    PropertyForTree<object>(kv.Key).ForEach(i => list.Add(i));
                    result[kv.Key] = list;
                });
            });
            return result;
        }
        
        public Dictionary<string, List<TProperty>> PropertiesForTree<TProperty>(Dictionary<string, List<TProperty>> theProperties = null)
        {
            theProperties ??= new Dictionary<string, List<TProperty>>();
            Trees.ReadOnlyTreeNode.Flatten(Tree).ForEach(n =>
            {
                if (!(n is ShaderTree tree)) return;
                tree.Node.Property.ForEach(kv =>
                {
                    var values = kv.Value.OfType<TProperty>();
                    if (values.IsEmpty()) return;
                    if (!theProperties.ContainsKey(kv.Key))
                    {
                        theProperties[kv.Key] = new List<TProperty>();
                    }
                    values.ForEach(v => theProperties[kv.Key].Add(v));
                });
            });
            return theProperties;
        }

        public Dictionary<string, IList> Property { get; } = new Dictionary<string, IList>();

        // ReSharper disable once MemberCanBeProtected.Global
        public void AddProperty(string thePropertyId, object theProperty)
        {
            if (!Property.ContainsKey(thePropertyId))
            {
                Property[thePropertyId] = new ArrayList();
            }

            Property[thePropertyId].Add(theProperty);
        }

        public void SetProperty(string thePropertyId, object theProperty)
        {
            Property[thePropertyId] = new ArrayList{theProperty};
        }

        public void RemoveProperty(string thePropertyId)
        {
            Property.Remove(thePropertyId);
        }

        // ReSharper disable once MemberCanBeProtected.Global
        public void AddProperties(string thePropertyId, IList theProperties)
        {
            if (!Property.ContainsKey(thePropertyId))
            {
                Property[thePropertyId] = new ArrayList();
            }

            Stride.Core.Extensions.EnumerableExtensions.ForEach<object>(theProperties, r => Property[thePropertyId].Add(r));

        }

        protected const string Mixins = "Mixins";
        protected const string Inputs = "Inputs";
        protected const string Compositions = "Compositions";
        protected const string Declarations = "Declarations";
        protected const string Structs = "Structs";
        protected const string ConstantArrays = "ConstantArrays";
        protected const string Streams = "Streams";

        public List<string> MixinList()
        {
            return PropertyForTree<string>(Mixins);
        }
        
        public List<IGpuInput> InputList()
        {
            return PropertyForTree<IGpuInput>(Inputs);
        }
        
        public List<string> CompositionList()
        {
            return PropertyForTree<string>(Compositions);
        }
        
        public List<FieldDeclaration> DeclarationList()
        {
            return PropertyForTree<FieldDeclaration>(Declarations);
        }
        
        public List<string> StructList()
        {
            return PropertyForTree<string>(Structs);
        }
        
        public List<string> ConstantArrayList()
        {
            return PropertyForTree<string>(ConstantArrays);
        }
        
        public List<string> StreamList()
        {
            return PropertyForTree<string>(Streams);
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
        // ReSharper disable once CollectionNeverQueried.Global
        // ReSharper disable once MemberCanBeProtected.Global
        public List<AbstractShaderNode> OptionalOutputs { get; protected set; }

        public  ShaderNode<T> Default { get; set; }

        public override AbstractShaderNode AbstractDefault => Default;

        public ShaderNode(NodeContext nodeContext, string theId, ShaderNode<T> theDefault = null, bool theCreateDefault = true) : base(nodeContext, theId)
        {
            Default = theDefault ?? (theCreateDefault ? ConstantHelper.FromFloat<T>(new NodeSubContextFactory(NodeContext).NextSubContext(), 0) : null);
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
        
        public override ShaderSource GenerateShaderSource(ShaderGeneratorContext context, MaterialComputeColorKeys baseKeys)
        {
            AbstractToShaderFX<T> toShaderFx;
            if (this is IComputeVoid)
            {
                toShaderFx = new ToComputeFx<T>(this);
            }
            else
            {
                toShaderFx = new ToShaderFX<T>(this);
            }
            
            var source = toShaderFx.GenerateShaderSource(context, baseKeys);
            ShaderCode = toShaderFx.ShaderCode;
            return source;
        }
        
        public string TypeOverride { get; set; }
        
        public override string TypeName()
        {
            return typeof(T) == typeof(GpuStruct) ? TypeOverride : TypeHelpers.GetGpuType<T>();
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

    public class ComputeNode<T> : ShaderNode<T> , IComputeVoid
    {
        public ComputeNode(NodeContext nodeContext, string theId, 
            ShaderNode<T> theDefault = null) : base(nodeContext, theId, theDefault)
        {
        }
    }
}