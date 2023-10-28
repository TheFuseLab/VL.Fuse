using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Fuse.function;
using Fuse.ShaderFX;
using Stride.Rendering.Materials;
using Stride.Rendering.Materials.ComputeColors;
using Stride.Shaders;
using VL.Core;
using VL.Stride.Shaders.ShaderFX;
using Buffer = Stride.Graphics.Buffer;


namespace Fuse
{

    /*

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

*/

    public interface IPrepareGraph
    {
        public void PrepareGraph(AbstractShaderNode theNode);
    }
    
    public interface IShaderNodeVisitor
    {
        void Visit(AbstractShaderNode node, int recursionLevel);
    }

    public class ChildrenOfTypeVisitor<TNode> : IShaderNodeVisitor where TNode : class
    {
        public readonly HashSet<TNode> Result = new ();
        
        public void Visit(AbstractShaderNode node, int recursionLevel)
        {
            if (node is TNode shaderNode)
            {
                Result.Add(shaderNode);
            }
        }
    }

    public class PropertyOfTypeVisitor<TPropertyType> : IShaderNodeVisitor 
    {
        public readonly List<TPropertyType> Result = new ();
        
        public void Visit(AbstractShaderNode node, int recursionLevel)
        {
            node.Property.ForEach(kv =>
            {
                var values = kv.Value.OfType<TPropertyType>();
                var tProperties = values as TPropertyType[] ?? values.ToArray();
                if (tProperties.IsEmpty()) return;
                tProperties.ForEach(v => Result.Add(v));
            });
        }
    }
    
    public class PropertyOfTypeAndIdVisitor<TPropertyType> : IShaderNodeVisitor 
    {
        public readonly HashSet<TPropertyType> Result = new ();

        private readonly string _propertyId;
        public PropertyOfTypeAndIdVisitor(string theThePropertyId)
        {
            _propertyId = theThePropertyId;
        }
        
        public void Visit(AbstractShaderNode node, int recursionLevel)
        {
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            if (node.Property.ContainsKey(_propertyId))
            {
                Stride.Core.Extensions.EnumerableExtensions.ForEach<TPropertyType>(node.Property[_propertyId], i => Result.Add(i));
                
            }
        }
    }
    
    public class PropertyIdsVisitor : IShaderNodeVisitor
    {
        public readonly HashSet<string> Result = new ();
        
        public void Visit(AbstractShaderNode node, int recursionLevel)
        {
            foreach (var kv in node.Property)
            {
                Result.Add(kv.Key);
            }
        }
    }
    
    public class PropertiesVisitor : IShaderNodeVisitor
    {
        public readonly Dictionary<string, IList> Result = new ();
        
        public void Visit(AbstractShaderNode node, int recursionLevel)
        {
            foreach (var kv in node.Property)
            {
                if (!Result.ContainsKey(kv.Key))
                {
                    var list = new ArrayList();
                    Result[kv.Key] = list;
                }

                foreach (var value in kv.Value)
                {
                    Result[kv.Key].Add(value);
                }
            }
        }
    }
    
    public class PropertiesTypedVisitor<TProperty> : IShaderNodeVisitor
    {
        public readonly Dictionary<string, List<TProperty>> Result = new ();
        
        public void Visit(AbstractShaderNode node, int recursionLevel)
        {
            foreach (var kv in node.Property)
            {
                var values = kv.Value.OfType<TProperty>();
                if (values.IsEmpty()) return;
                
                if (!Result.ContainsKey(kv.Key))
                {
                    var list = new List<TProperty>();
                    Result[kv.Key] = list;
                }

                foreach (var value in values)
                {
                    Result[kv.Key].Add(value);
                }
            }
        }
    }
    
    public class FunctionMapVisitor : IShaderNodeVisitor
    {
        public readonly Dictionary<string,string> Result = new ();
        
        public void Visit(AbstractShaderNode node, int recursionLevel)
        {
            if (node.Functions == null) return;
            foreach (var kv in node.Functions)
            {
                if (Result.ContainsKey(kv.Key)) continue;
                Result.Add(kv.Key, kv.Value);
            }
        }
    }
    
    public class PrintShaderNodeVisitor : IShaderNodeVisitor
    {
        public void Visit(AbstractShaderNode node, int recursionLevel)
        {
            if (node == null) return;

            // Print node with the prepend string
            Console.WriteLine(new string('.', recursionLevel) + " " + node.ID + node.GetHashCode());
        }
    }

    public class CheckContextVisitor : IShaderNodeVisitor
    {
        private readonly ShaderGeneratorContext _context;

        public CheckContextVisitor(ShaderGeneratorContext theContext)
        {
            _context = theContext;
        }
        
        public void Visit(AbstractShaderNode node, int recursionLevel)
        {
            node.OnPassContext(_context);
        }
    }

    //this is a visitor that checks if the node has a unique name
    public class CheckIdsVisitor : IShaderNodeVisitor
    {

        private readonly Dictionary<uint, uint> _hashInstances = new();

        public void Visit(AbstractShaderNode node, int recursionLevel)
        {
            
            if (node.HasFixedName) return;
            
            if (_hashInstances.ContainsKey(node.HashCode))
            {
                _hashInstances[node.HashCode]++;
                if (node.Name.EndsWith("_" + _hashInstances[node.HashCode])) return;
                node.Name += "_" +_hashInstances[node.HashCode];
                if (node is IGpuInput input)
                {
                    input.OnUpdateName();
                }
            }
            else
            {
                _hashInstances[node.HashCode] = 1;
            }
        }
    }
    
    public class BuildSourceVisitor : IShaderNodeVisitor
    {
        StringBuilder myStringBuilder = new StringBuilder();
        HashSet<AbstractShaderNode> myHashes = new HashSet<AbstractShaderNode>();
        
        public void Visit(AbstractShaderNode node, int recursionLevel)
        {
            foreach (var kv in node.Property)
            {
            }
        }
    }

    public enum HandleNullInputMode
    {
        ReplaceWithDefault,
        Remove,
        SetOff
    }

    public class ViewerID
    {
        public string ID { get;  }

        public ViewerID(string theID)
        {
            ID = theID;
        }
    }

    public abstract class AbstractShaderNode : IComputeNode
    {
        public List<AbstractShaderNode> Ins = new();
        // Used for out parameters to trigger code generation
        
        public readonly List<IPrepareGraph> PrepareGraphListener = new();

        public readonly NodeContext NodeContext;

        public bool HasFixedName { get; set;  }

        public int WriteCounter { get; set; }

        public void AddPrepareGraph(IPrepareGraph theDelegate)
        {
            PrepareGraphListener.Add(theDelegate);
        }
        
        public void RemovePrepareGraph(IPrepareGraph theDelegate)
        {
            PrepareGraphListener.Remove(theDelegate);
        }
        
        
        public virtual string Name{ get;  set; }

        public abstract AbstractShaderNode AbstractDefault { get; }

        public uint HashCode { get; set; }

        public abstract string ID { get; }

        public virtual string DelegateID => ID;

        public abstract string TypeName(); 

        protected abstract string SourceTemplate();

        public abstract int Dimension();

        protected AbstractShaderNode(NodeContext nodeContext, string theId)
        {
            NodeContext = nodeContext;
            Name = theId;
            HashCode = ShaderNodesUtil.GetHashCode(nodeContext);
            HasFixedName = false;
        }
        
        public void SetViewerID(string theID)
        {
            SetProperty("ViewerID", new ViewerID(theID));
        }
        
        public virtual IDictionary<string,string> Functions
        {
            get => new Dictionary<string, string>();
            protected set => throw new NotImplementedException();
        }

        private const string DefaultShaderCode = "${resultType} ${resultName};";

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

        public void SetInputs(IEnumerable<AbstractShaderNode> theIns)
        {
            if (Ins.SequenceEqual(theIns)) return;
            
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
            return ShaderNodesUtil.Evaluate(DefaultShaderCode, new Dictionary<string, string>
            {
                {"resultName", ID},
                {"resultType", TypeName()},
                {"default", ""}
            });
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

        public virtual void OnPassContext(ShaderGeneratorContext nodeContext)
        {
            
        }

        public virtual void BuildChildrenSource( StringBuilder theSourceBuilder, HashSet<AbstractShaderNode> theHashes, string thePrepend)
        {
            //Console.WriteLine(Name);
            foreach (var input in Ins)
            {
                input.BuildSource( theSourceBuilder, theHashes, thePrepend + ShaderNodesUtil.DebugIdent);
            }
        }

        protected virtual void BuildSource(StringBuilder theSourceBuilder, HashSet<AbstractShaderNode> theHashes, string thePrepend)
        {
            if (ShaderNodesUtil.DebugShaderGeneration) Console.WriteLine(thePrepend + ID);
            
            if (!theHashes.Add(this))return;
            
            BuildChildrenSource(theSourceBuilder, theHashes, thePrepend);
            
            var source = SourceCode;
            //Console.Out.WriteLine(Name + " : " + HashCode);
            if (string.IsNullOrWhiteSpace(source)) return;
            
            theSourceBuilder.Append("        ");
            theSourceBuilder.Append(source);
            theSourceBuilder.Append(Environment.NewLine);
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
            var myHashes = new HashSet<AbstractShaderNode>();

            BuildSource( myStringBuilder, myHashes,"");
            return myStringBuilder.ToString();
        }

        public void PreOrderVisit(IShaderNodeVisitor theVisitor, HashSet<AbstractShaderNode> theVisitedNodes, int recursionLevel = 0)
        {
            if (!theVisitedNodes.Add(this)) return;

            theVisitor.Visit(this, recursionLevel);
            foreach (var node in Ins)
            {
                node?.PreOrderVisit(theVisitor, theVisitedNodes, recursionLevel + 1);
            }
        }

        public void PreOrderVisit(IShaderNodeVisitor theVisitor)
        {
            PreOrderVisit(theVisitor, new HashSet<AbstractShaderNode>());
        }

        public void PostOrderVisit(IShaderNodeVisitor theVisitor, HashSet<AbstractShaderNode> theVisitedNodes, int recursionLevel = 0)
        {
            if (!theVisitedNodes.Add(this))return;
            
            foreach (var node in Ins)
            {
                node?.PostOrderVisit(theVisitor, theVisitedNodes);
            }
            theVisitor.Visit(this, recursionLevel);
        }

        public void PrintVisitTree()
        {
            // Start visiting the nodes from the root node
            PreOrderVisit(new PrintShaderNodeVisitor(), new HashSet<AbstractShaderNode>());
        }
        
        public void CheckHashCodes()
        {
            PreOrderVisit(new CheckIdsVisitor(), new HashSet<AbstractShaderNode>());
        }

        private readonly HashSet<ShaderGeneratorContext> _contexts = new();

        public void CheckContext(ShaderGeneratorContext theContext)
        {

            if (!_contexts.Add(theContext)) return;

            var visitor = new CheckContextVisitor(theContext);
            PreOrderVisit(visitor);
        }

        public List<TNode> ChildrenOfType<TNode>() where TNode : class
        {
            var visitor = new ChildrenOfTypeVisitor<TNode>();
            PreOrderVisit(visitor);
            return visitor.Result.ToList();
        }
        
        public List<IFunctionParameter> FunctionParameters()
        {
            return ChildrenOfType<IFunctionParameter>();
        }
        
        public List<TPropertyType> PropertyForTree<TPropertyType>(string theThePropertyId)
        {
            var visitor = new PropertyOfTypeAndIdVisitor<TPropertyType>(theThePropertyId);
            PreOrderVisit(visitor);
            return visitor.Result.ToList();
        }
        
        public List<TPropertyType> PropertiesForTreeList<TPropertyType>()
        {
            var visitor = new PropertyOfTypeVisitor<TPropertyType>();
            PreOrderVisit(visitor);
            return visitor.Result.ToList();
        }

        public List<string> PropertyIdsForTree()
        {
            var visitor = new PropertyIdsVisitor();
            PreOrderVisit(visitor);
            return visitor.Result.ToList();
        }
        
        public Dictionary<string, IList> PropertiesForTree()
        {
            var visitor = new PropertiesVisitor();
            PreOrderVisit(visitor);
            return visitor.Result;
        }
        
        public Dictionary<string, List<TProperty>> PropertiesForTree<TProperty>(Dictionary<string, List<TProperty>> theProperties = null)
        {
            var visitor = new PropertiesTypedVisitor<TProperty>();
            PreOrderVisit(visitor);
            return visitor.Result;
        }

        public Dictionary<string, IList> Property { get; } = new();

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
            var visitor = new FunctionMapVisitor();
            PreOrderVisit(visitor);
            return visitor.Result;
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
            Default = theDefault ?? (theCreateDefault ? ConstantHelper.FromFloat<T>( 0) : null);
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
            return typeof(T) == typeof(GpuStruct) ||typeof(T)  == typeof(Buffer) ? TypeOverride : TypeHelpers.GetGpuType<T>();
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

    public abstract class ResultNode<T> : ShaderNode<T>
    {
        protected ResultNode(NodeContext nodeContext, string theId, ShaderNode<T> theDefault = null, bool theCreateDefault = true) : base(nodeContext, theId, theDefault, theCreateDefault)
        {
        }
        
        protected override string SourceTemplate()
        {
            return ShaderNodesUtil.Evaluate("${resultType} ${resultName} = ${implementation};",new Dictionary<string, string> {
                {"implementation", ImplementationTemplate()}
            });
        }

        protected abstract string ImplementationTemplate();

    }

    public class ComputeNode<T> : ShaderNode<T> , IComputeVoid
    {
        public ComputeNode(NodeContext nodeContext, string theId, 
            ShaderNode<T> theDefault = null) : base(nodeContext, theId, theDefault)
        {
        }
    }
}