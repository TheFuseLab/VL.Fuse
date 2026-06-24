using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Fuse.compute;
using Fuse.function;
using Fuse.ShaderFX;
using Stride.Graphics;
using Stride.Rendering.Materials;
using Stride.Rendering.Materials.ComputeColors;
using Stride.Shaders;
using VL.Core;
using VL.Stride.Shaders.ShaderFX;
using Buffer = Stride.Graphics.Buffer;
using EnumerableExtensions = Stride.Core.Extensions.EnumerableExtensions;


namespace Fuse;

/// <summary>
/// Interface for components that need to prepare the shader graph before compilation.
/// </summary>
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
    public readonly HashSet<TNode> Result = new();

    public void Visit(AbstractShaderNode node, int recursionLevel)
    {
        if (node is TNode shaderNode) Result.Add(shaderNode);
    }
}

public class PropertyOfTypeVisitor<TPropertyType> : IShaderNodeVisitor
{
    public readonly List<TPropertyType> Result = new();

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
    private readonly string _propertyId;
    public readonly HashSet<TPropertyType> Result = new();

    public PropertyOfTypeAndIdVisitor(string theThePropertyId)
    {
        _propertyId = theThePropertyId;
    }

    public void Visit(AbstractShaderNode node, int recursionLevel)
    {
        if (node.Property.ContainsKey(_propertyId))
            EnumerableExtensions.ForEach<TPropertyType>(node.Property[_propertyId], i => Result.Add(i));
    }
}

public class PropertyIdsVisitor : IShaderNodeVisitor
{
    public readonly HashSet<string> Result = new();

    public void Visit(AbstractShaderNode node, int recursionLevel)
    {
        foreach (var kv in node.Property) Result.Add(kv.Key);
    }
}

public class PropertiesVisitor : IShaderNodeVisitor
{
    public readonly Dictionary<string, IList> Result = new();

    public void Visit(AbstractShaderNode node, int recursionLevel)
    {
        foreach (var kv in node.Property)
        {
            if (!Result.ContainsKey(kv.Key))
            {
                var list = new ArrayList();
                Result[kv.Key] = list;
            }

            foreach (var value in kv.Value) Result[kv.Key].Add(value);
        }
    }
}

public class PropertiesTypedVisitor<TProperty> : IShaderNodeVisitor
{
    public readonly Dictionary<string, List<TProperty>> Result = new();

    public void Visit(AbstractShaderNode node, int recursionLevel)
    {
        foreach (var kv in node.Property)
        {
            var values = kv.Value.OfType<TProperty>();
            if (values.IsEmpty()) continue;

            if (!Result.ContainsKey(kv.Key))
            {
                var list = new List<TProperty>();
                Result[kv.Key] = list;
            }

            foreach (var value in values) Result[kv.Key].Add(value);
        }
    }
}

public class FunctionMapVisitor : IShaderNodeVisitor
{
    public readonly Dictionary<string, string> Result = new();

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

        if (_hashInstances.TryAdd(node.HashCode, 1)) return;

        _hashInstances[node.HashCode]++;
        if (node.Name.EndsWith("_" + _hashInstances[node.HashCode])) return;
        node.Name += "_" + _hashInstances[node.HashCode];
        if (node is IGpuInput input) input.OnUpdateName();
    }
}

public class BuildSourceVisitor : IShaderNodeVisitor
{
    private HashSet<AbstractShaderNode> myHashes = new();
    private StringBuilder myStringBuilder = new();

    public void Visit(AbstractShaderNode node, int recursionLevel)
    {
        foreach (var kv in node.Property)
        {
        }
    }
}

/// <summary>
/// Result of a unified shader compilation pass that collects all properties,
/// validates IDs, and passes context in a single traversal.
/// </summary>
public class ShaderCompilationResult
{
    public List<string> Mixins { get; } = new();
    public List<IGpuInput> Inputs { get; } = new();
    public List<string> Compositions { get; } = new();
    public List<FieldDeclaration> Declarations { get; } = new();
    public List<string> Structs { get; } = new();
    public List<string> ConstantArrays { get; } = new();
    public List<string> Streams { get; } = new();
    public Dictionary<string, string> Functions { get; } = new();
}

/// <summary>
/// Unified visitor that collects all shader properties, validates IDs, and passes context
/// in a single graph traversal. This replaces 9 separate traversals with 1.
/// </summary>
/// <remarks>
/// Combines the functionality of:
/// - CheckIdsVisitor (ID validation)
/// - CheckContextVisitor (context passing)
/// - PropertyOfTypeAndIdVisitor for each property type
/// - FunctionMapVisitor
/// </remarks>
public class ShaderCompilationVisitor : IShaderNodeVisitor
{
    private readonly ShaderGeneratorContext _context;
    private readonly Dictionary<uint, uint> _hashInstances = new();

    public ShaderCompilationResult Result { get; } = new();

    public ShaderCompilationVisitor(ShaderGeneratorContext context)
    {
        _context = context;
    }

    public void Visit(AbstractShaderNode node, int recursionLevel)
    {
        // 1. Validate/fix node IDs (was CheckIdsVisitor)
        ValidateNodeId(node);

        // 2. Pass context to node (was CheckContextVisitor)
        node.OnPassContext(_context);

        // 3. Collect all properties in one pass
        CollectProperties(node);

        // 4. Collect functions (was FunctionMapVisitor)
        CollectFunctions(node);

        // Note: UseCount is computed separately in a PostOrderVisit pass
        // to ensure correct ordering (children processed before parents)
    }

    private void ValidateNodeId(AbstractShaderNode node)
    {
        if (node.HasFixedName) return;

        if (_hashInstances.TryAdd(node.HashCode, 1)) return;

        _hashInstances[node.HashCode]++;
        var suffix = "_" + _hashInstances[node.HashCode];
        if (node.Name.EndsWith(suffix)) return;
        node.Name += suffix;
        if (node is IGpuInput input) input.OnUpdateName();
    }

    private void CollectProperties(AbstractShaderNode node)
    {
        foreach (var kv in node.Property)
        {
            switch (kv.Key)
            {
                case "Mixins":
                    foreach (var item in kv.Value)
                        if (item is string s) Result.Mixins.Add(s);
                    break;
                case "Inputs":
                    foreach (var item in kv.Value)
                        if (item is IGpuInput input) Result.Inputs.Add(input);
                    break;
                case "Compositions":
                    foreach (var item in kv.Value)
                        if (item is string s) Result.Compositions.Add(s);
                    break;
                case "Declarations":
                    foreach (var item in kv.Value)
                        if (item is FieldDeclaration decl) Result.Declarations.Add(decl);
                    break;
                case "Structs":
                    foreach (var item in kv.Value)
                        if (item is string s) Result.Structs.Add(s);
                    break;
                case "ConstantArrays":
                    foreach (var item in kv.Value)
                        if (item is string s) Result.ConstantArrays.Add(s);
                    break;
                case "Streams":
                    foreach (var item in kv.Value)
                        if (item is string s) Result.Streams.Add(s);
                    break;
            }
        }
    }

    private void CollectFunctions(AbstractShaderNode node)
    {
        if (node.Functions == null) return;
        foreach (var kv in node.Functions)
        {
            if (!Result.Functions.ContainsKey(kv.Key))
                Result.Functions.Add(kv.Key, kv.Value);
        }
    }
}

/// <summary>
/// Resets the UseCount property of all nodes to 0 before a compilation pass.
/// </summary>
public class ResetUseCountVisitor : IShaderNodeVisitor
{
    public void Visit(AbstractShaderNode node, int recursionLevel)
    {
        node.UseCount = 0;
    }
}

/// <summary>
/// Counts how many times each node's output is used by incrementing UseCount
/// for each input reference. Run after ResetUseCountVisitor.
/// </summary>
/// <remarks>
/// After this visitor runs:
/// - UseCount == 0: Node output is unused (dead code candidate)
/// - UseCount == 1: Node output used once (inline candidate)
/// - UseCount > 1: Node output used multiple times (must emit variable)
/// </remarks>
public class UseCountVisitor : IShaderNodeVisitor
{
    public void Visit(AbstractShaderNode node, int recursionLevel)
    {
        foreach (var input in node.Ins)
        {
            if (input != null)
                input.UseCount++;
        }
    }
}

/// <summary>
/// Specifies how null inputs are handled during shader node processing.
/// </summary>
public enum HandleNullInputMode
{
    /// <summary>
    /// Replace null inputs with the node's default value.
    /// </summary>
    ReplaceWithDefault,

    /// <summary>
    /// Remove null inputs from the input list entirely.
    /// </summary>
    Remove,

    /// <summary>
    /// Mark the node as having null input, causing it to generate default source code.
    /// This is the default behavior.
    /// </summary>
    SetOff
}

/// <summary>
/// Specifies whether an expression should be inlined or assigned to a variable.
/// </summary>
/// <remarks>
/// Used by <see cref="AbstractShaderNode.ShouldInline"/> to decide code generation strategy.
/// </remarks>
public enum InlineDecision
{
    /// <summary>
    /// Inline the expression at the use site, avoiding a separate variable declaration.
    /// Best for simple, single-use expressions.
    /// </summary>
    Inline,

    /// <summary>
    /// Create a variable and assign the expression to it.
    /// Required for multi-use expressions or complex operations.
    /// </summary>
    CreateVariable
}

/// <summary>
/// Represents a viewer identifier for debugging and visualization purposes.
/// </summary>
public class ViewerID
{
    public ViewerID(string theID)
    {
        ID = theID;
    }

    public string ID { get; }
}

/// <summary>
/// Base class for all shader graph nodes in VL.Fuse.
/// </summary>
/// <remarks>
/// <para>
/// AbstractShaderNode is the foundation of the VL.Fuse shader generation system.
/// Each node represents an operation that generates SDSL (Stride Shading Language) code.
/// </para>
/// <para>
/// Nodes are connected in a directed acyclic graph (DAG) where:
/// <list type="bullet">
///   <item><description>The <see cref="Ins"/> list contains input connections</description></item>
///   <item><description><see cref="SourceTemplate"/> returns the SDSL code template with ${placeholders}</description></item>
///   <item><description><see cref="Property"/> tracks dependencies (mixins, inputs, declarations, etc.)</description></item>
/// </list>
/// </para>
/// <para>
/// To create a custom node, inherit from <see cref="ShaderNode{T}"/> or <see cref="ResultNode{T}"/>
/// and override <see cref="SourceTemplate"/> to provide the shader code.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class MyAddNode : ResultNode&lt;float&gt;
/// {
///     public MyAddNode(NodeContext ctx, ShaderNode&lt;float&gt; a, ShaderNode&lt;float&gt; b)
///         : base(ctx, "MyAdd")
///     {
///         SetInputs(new AbstractShaderNode[] { a, b });
///     }
///
///     protected override string ImplementationTemplate() => "${a} + ${b}";
/// }
/// </code>
/// </example>
public abstract class AbstractShaderNode : IComputeNode
{
    private const string DefaultShaderCode = "${resultType} ${resultName};";

    /// <summary>Property key for required shader mixins.</summary>
    protected const string Mixins = "Mixins";

    /// <summary>Property key for GPU input parameters (uniforms, buffers, textures).</summary>
    protected const string Inputs = "Inputs";

    /// <summary>Property key for shader compositions.</summary>
    protected const string Compositions = "Compositions";

    /// <summary>Property key for field declarations.</summary>
    protected const string Declarations = "Declarations";

    /// <summary>Property key for custom struct definitions.</summary>
    protected const string Structs = "Structs";

    /// <summary>Property key for constant array definitions.</summary>
    protected const string ConstantArrays = "ConstantArrays";

    /// <summary>Property key for shader stream definitions.</summary>
    protected const string Streams = "Streams";

    private readonly HashSet<ShaderGeneratorContext> _contexts = [];

    /// <summary>
    /// The VL node context that uniquely identifies this node instance.
    /// Used for generating unique variable names and tracking node identity.
    /// </summary>
    public readonly NodeContext NodeContext;

    /// <summary>
    /// List of listeners that are notified when the graph needs preparation.
    /// </summary>
    public readonly List<IPrepareGraph> PrepareGraphListener = new();

    private bool _hasNullInput;

    /// <summary>
    /// The list of input nodes connected to this node.
    /// Use <see cref="SetInputs"/> to modify this list properly.
    /// </summary>
    public List<AbstractShaderNode> Ins = new();

    /// <summary>
    /// Determines how null inputs are handled. Defaults to <see cref="HandleNullInputMode.SetOff"/>.
    /// </summary>
    protected HandleNullInputMode NullInputMode = HandleNullInputMode.SetOff;

    /// <summary>
    /// Initializes a new instance of the shader node.
    /// </summary>
    /// <param name="nodeContext">The VL node context for unique identification. Must not be null.</param>
    /// <param name="theId">The base name for this node, used in variable naming.</param>
    protected AbstractShaderNode(NodeContext nodeContext, string theId)
    {
        NodeContext = nodeContext;
        Name = theId;
        HashCode = ShaderNodesUtil.GetHashCode(nodeContext);
        HasFixedName = false;
    }

    public bool HasFixedName { get; set; }

    public int WriteCounter { get; set; }

    /// <summary>
    /// Tracks how many times this node's output is used by other nodes.
    /// Used for smart inlining decisions - nodes used only once can be inlined.
    /// Reset before each compilation pass via <see cref="ResetUseCountVisitor"/>.
    /// </summary>
    public int UseCount { get; set; }

    /// <summary>
    /// Determines whether this node's expression should be inlined or assigned to a variable.
    /// Override in derived classes to customize inlining behavior.
    /// </summary>
    /// <param name="maxDepth">Maximum nesting depth for inlined expressions (default 4).</param>
    /// <param name="maxLength">Maximum character length for inlined expressions (default 60).</param>
    /// <returns>
    /// <see cref="InlineDecision.Inline"/> if the expression can be inlined,
    /// <see cref="InlineDecision.CreateVariable"/> if a variable should be created.
    /// </returns>
    /// <remarks>
    /// <para>Default rules:</para>
    /// <list type="bullet">
    ///   <item><description>UseCount &gt; 1: Always create variable (avoid duplicate computation)</description></item>
    ///   <item><description>UseCount == 0: Unused node (dead code candidate)</description></item>
    ///   <item><description>UseCount == 1: Candidate for inlining based on complexity</description></item>
    /// </list>
    /// <para>Override this in node types like FunctionInvoke to always create variables for readability.</para>
    /// </remarks>
    public virtual InlineDecision ShouldInline(int maxDepth = 4, int maxLength = 60)
    {
        // Default: don't inline - it's safer
        // Derived classes can override to enable inlining for specific safe cases
        return InlineDecision.CreateVariable;
    }

    /// <summary>
    /// Gets a reference to this node's value - either the variable ID or an inline expression.
    /// Use this in templates instead of <see cref="ID"/> to enable expression inlining.
    /// </summary>
    /// <param name="depth">Current inlining depth to prevent excessive nesting (default 3).</param>
    /// <returns>
    /// The variable ID if the node should not be inlined, or the inline expression if it should.
    /// </returns>
    public virtual string GetReference(int depth = 3)
    {
        // Don't inline if depth exhausted or node shouldn't be inlined
        if (depth <= 0 || ShouldInline() == InlineDecision.CreateVariable)
            return ID;

        // Get the inline expression
        return GetInlineExpression(depth - 1);
    }

    /// <summary>
    /// Gets the inline expression for this node (the right-hand side of the assignment).
    /// Override in derived classes to provide the actual expression.
    /// </summary>
    /// <param name="depth">Remaining depth for nested inlining.</param>
    /// <returns>The expression wrapped in parentheses, or the ID if no expression available.</returns>
    protected virtual string GetInlineExpression(int depth)
    {
        // Base implementation returns ID - derived classes override with actual expression
        return ID;
    }

    public virtual string Name { get; set; }

    /// <summary>
    /// Optional user-friendly name for this node, used to generate more readable shader code.
    /// When set, the ID will use this name instead of the generic type-based name.
    /// </summary>
    /// <example>
    /// Setting SemanticName = "boxSize" will generate "boxSize_123456" instead of "input_123456"
    /// </example>
    public string SemanticName { get; set; }

    public abstract AbstractShaderNode AbstractDefault { get; }

    public uint HashCode { get; set; }

    public abstract string ID { get; }

    public virtual string DelegateID => ID;

    public virtual IDictionary<string, string> Functions
    {
        get => new Dictionary<string, string>();
        protected set => throw new NotImplementedException();
    }

    public string SourceCode => GenerateSource(Ins);

    public string ShaderCode { get; set; }

    public Dictionary<string, IList> Property { get; } = new();


    public virtual IEnumerable<IComputeNode> GetChildren(object context = null)
    {
        return Enumerable.Empty<ComputeNode>();
    }

    public virtual ShaderSource GenerateShaderSource(ShaderGeneratorContext context, MaterialComputeColorKeys baseKeys)
    {
        return null; //throw new NotImplementedException();
    }

    public void AddPrepareGraph(IPrepareGraph theDelegate)
    {
        PrepareGraphListener.Add(theDelegate);
    }

    public void RemovePrepareGraph(IPrepareGraph theDelegate)
    {
        PrepareGraphListener.Remove(theDelegate);
    }

    public abstract string TypeName();

    protected abstract string SourceTemplate();

    public abstract int Dimension();

    public void SetViewerID(string theID)
    {
        SetProperty("ViewerID", new ViewerID(theID));
    }

    public void CallPrepareGraph()
    {
        foreach (var listener in PrepareGraphListener) listener.PrepareGraph(this);

        foreach (var input in Ins) input.CallPrepareGraph();
    }

    public void SetInputs(IEnumerable<AbstractShaderNode> theIns)
    {
        if (Ins.SequenceEqual(theIns)) return;

        _hasNullInput = false;
        Ins = [];
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
        if (theInput != null) Ins.Add(theInput);
    }

    protected virtual Dictionary<string, string> CreateTemplateMap()
    {
        return new Dictionary<string, string>();
    }

    protected virtual string GenerateDefaultSource()
    {
        var resultType = TypeName();
        if (resultType == "struct")
            return "";

        return ShaderNodesUtil.Evaluate(DefaultShaderCode, new Dictionary<string, string>
        {
            { "resultName", ID },
            { "resultType", resultType },
            { "default", "" }
        });
    }

    private string GenerateSource(IEnumerable<AbstractShaderNode> theIns)
    {
        if (_hasNullInput) return GenerateDefaultSource();

        var sourceCode = SourceTemplate();
        return sourceCode.Trim() == "" ? "" : ShaderNodesUtil.Evaluate(sourceCode, CreateTemplateMap());
    }

    public virtual void OnPassContext(ShaderGeneratorContext nodeContext)
    {
    }

    public virtual void BuildChildrenSource(StringBuilder theSourceBuilder, HashSet<AbstractShaderNode> theHashes,
        string thePrepend)
    {
        //Console.WriteLine(Name);
        foreach (var input in Ins)
            input.BuildSource(theSourceBuilder, theHashes, thePrepend + ShaderNodesUtil.DebugIdent);
    }

    protected virtual void BuildSource(StringBuilder theSourceBuilder, HashSet<AbstractShaderNode> theHashes,
        string thePrepend)
    {
        if (ShaderNodesUtil.DebugShaderGeneration) Console.WriteLine(thePrepend + ID);

        if (!theHashes.Add(this)) return;

        BuildChildrenSource(theSourceBuilder, theHashes, thePrepend);

        var source = SourceCode;
        //Console.Out.WriteLine(Name + " : " + HashCode);
        if (string.IsNullOrWhiteSpace(source)) return;

        theSourceBuilder.Append("        ");
        theSourceBuilder.Append(source);
        theSourceBuilder.Append(Environment.NewLine);
    }

    // Caching for BuildSourceCode to avoid regenerating the same subtree
    private string _cachedBuildSourceCode;
    private int _cachedBuildSourceCodeVersion;
    private static int _currentCompilationVersion;

    /// <summary>
    /// Increments the compilation version, invalidating all BuildSourceCode caches.
    /// Call this at the start of a new shader compilation.
    /// </summary>
    public static void ResetBuildSourceCodeCache()
    {
        _currentCompilationVersion++;
    }

    public string BuildSourceCode()
    {
        // Return cached result if still valid for this compilation
        if (_cachedBuildSourceCode != null && _cachedBuildSourceCodeVersion == _currentCompilationVersion)
            return _cachedBuildSourceCode;

        var myStringBuilder = new StringBuilder();
        var myHashes = new HashSet<AbstractShaderNode>();

        BuildSource(myStringBuilder, myHashes, "");

        _cachedBuildSourceCode = myStringBuilder.ToString();
        _cachedBuildSourceCodeVersion = _currentCompilationVersion;
        return _cachedBuildSourceCode;
    }

    public void PreOrderVisit(IShaderNodeVisitor theVisitor, HashSet<AbstractShaderNode> theVisitedNodes,
        int recursionLevel = 0)
    {
        if (!theVisitedNodes.Add(this)) return;

        theVisitor.Visit(this, recursionLevel);
        foreach (var node in Ins) node?.PreOrderVisit(theVisitor, theVisitedNodes, recursionLevel + 1);
    }

    public void PreOrderVisit(IShaderNodeVisitor theVisitor)
    {
        PreOrderVisit(theVisitor, new HashSet<AbstractShaderNode>());
    }

    public void PostOrderVisit(IShaderNodeVisitor theVisitor, HashSet<AbstractShaderNode> theVisitedNodes,
        int recursionLevel = 0)
    {
        if (!theVisitedNodes.Add(this)) return;

        foreach (var node in Ins) node?.PostOrderVisit(theVisitor, theVisitedNodes, recursionLevel + 1);
        theVisitor.Visit(this, recursionLevel);
    }

    public void PrintVisitTree()
    {
        // Start visiting the nodes from the root node
        PreOrderVisit(new PrintShaderNodeVisitor(), []);
    }

    public void CheckHashCodes()
    {
        PreOrderVisit(new CheckIdsVisitor(), []);
    }

    public void CheckContext(ShaderGeneratorContext theContext)
    {
        if (!_contexts.Add(theContext)) return;

        var visitor = new CheckContextVisitor(theContext);
        PreOrderVisit(visitor);
    }

    /// <summary>
    /// Performs a unified compilation pass that collects all properties, validates IDs,
    /// and passes context in a single graph traversal.
    /// </summary>
    /// <remarks>
    /// This replaces separate calls to CheckHashCodes(), CheckContext(), and multiple
    /// property collection methods (MixinList, DeclarationList, etc.) with a single traversal.
    /// Use this for better performance when you need multiple property types.
    /// </remarks>
    /// <param name="context">The shader generator context.</param>
    /// <returns>A result containing all collected properties and functions.</returns>
    public ShaderCompilationResult CompileProperties(ShaderGeneratorContext context)
    {
        _contexts.Add(context);
        var visitor = new ShaderCompilationVisitor(context);
        PreOrderVisit(visitor);

        // Compute UseCount in a separate pass
        // First reset all counts, then count uses
        PreOrderVisit(new ResetUseCountVisitor());
        PreOrderVisit(new UseCountVisitor());

        return visitor.Result;
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

    public Dictionary<string, List<TProperty>> PropertiesForTree<TProperty>(
        Dictionary<string, List<TProperty>> theProperties = null)
    {
        var visitor = new PropertiesTypedVisitor<TProperty>();
        PreOrderVisit(visitor);
        return visitor.Result;
    }

    // ReSharper disable once MemberCanBeProtected.Global
    public void AddProperty(string thePropertyId, object theProperty)
    {
        if (!Property.ContainsKey(thePropertyId)) Property[thePropertyId] = new ArrayList();

        Property[thePropertyId].Add(theProperty);
    }

    public void SetProperty(string thePropertyId, object theProperty)
    {
        Property[thePropertyId] = new ArrayList { theProperty };
    }

    public void RemoveProperty(string thePropertyId)
    {
        Property.Remove(thePropertyId);
    }

    // ReSharper disable once MemberCanBeProtected.Global
    public void AddProperties(string thePropertyId, IList theProperties)
    {
        if (!Property.ContainsKey(thePropertyId)) Property[thePropertyId] = new ArrayList();

        EnumerableExtensions.ForEach<object>(theProperties, r => Property[thePropertyId].Add(r));
    }

    public List<TPropertyType> GetProperties<TPropertyType>()
    {
        List<TPropertyType> result = new();

        Property.ForEach(kv =>
        {
            var values = kv.Value.OfType<TPropertyType>();
            var tProperties = values as TPropertyType[] ?? values.ToArray();
            if (tProperties.IsEmpty()) return;
            tProperties.ForEach(v => result.Add(v));
        });

        return result;
    }

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

    public Dictionary<string, string> FunctionMap()
    {
        var visitor = new FunctionMapVisitor();
        PreOrderVisit(visitor);
        return visitor.Result;
    }
}

/// <summary>
/// Generic typed shader node that represents a value of type <typeparamref name="T"/> on the GPU.
/// </summary>
/// <typeparam name="T">The C# type that maps to a GPU type (e.g., float, Vector3, Matrix).</typeparam>
/// <remarks>
/// <para>
/// ShaderNode&lt;T&gt; provides type safety for shader operations. The generic type parameter
/// maps to GPU types via <see cref="TypeHelpers"/>:
/// </para>
/// <list type="bullet">
///   <item><description><c>float</c> → <c>float</c></description></item>
///   <item><description><c>Vector2</c> → <c>float2</c></description></item>
///   <item><description><c>Vector3</c> → <c>float3</c></description></item>
///   <item><description><c>Vector4</c> → <c>float4</c></description></item>
///   <item><description><c>Matrix</c> → <c>float4x4</c></description></item>
///   <item><description><c>int</c> → <c>int</c></description></item>
///   <item><description><c>bool</c> → <c>bool</c></description></item>
/// </list>
/// <para>
/// The node's unique ID is generated as <c>{Name}_{HashCode}</c> to ensure variable uniqueness
/// in the generated shader code.
/// </para>
/// </remarks>
[MonadicTypeFilter(typeof(ShaderNodeMonadicTypeFilter))]
public class ShaderNode<T> : AbstractShaderNode, IComputeValue<T>, IMonadicValue<T>
{
    /// <summary>
    /// Initializes a new shader node with the specified context and identifier.
    /// </summary>
    /// <param name="nodeContext">The VL node context for unique identification.</param>
    /// <param name="theId">The base name for this node.</param>
    /// <param name="theDefault">Optional default value node when inputs are null.</param>
    /// <param name="theCreateDefault">If true and theDefault is null, creates a default value of 0.</param>
    public ShaderNode(NodeContext nodeContext, string theId, ShaderNode<T> theDefault = null,
        bool theCreateDefault = true) : base(nodeContext, theId)
    {
        Default = theDefault ?? (theCreateDefault ? ConstantHelper.FromFloat<T>(0) : null);
    }

    // ReSharper disable once CollectionNeverQueried.Global
    // ReSharper disable once MemberCanBeProtected.Global
    public List<AbstractShaderNode> OptionalOutputs { get; protected set; }

    public ShaderNode<T> Default { get; set; }

    public override AbstractShaderNode AbstractDefault => Default;

    public string TypeOverride { get; set; }

    // Cached ID to avoid repeated string concatenation
    private string _cachedId;
    private string _cachedIdName;
    private string _cachedIdSemantic;

    public override string ID
    {
        get
        {
            // Recompute if Name or SemanticName changed
            var effectiveName = !string.IsNullOrEmpty(SemanticName) ? SemanticName : Name;
            if (_cachedId == null || _cachedIdName != Name || _cachedIdSemantic != SemanticName)
            {
                _cachedIdName = Name;
                _cachedIdSemantic = SemanticName;
                _cachedId = $"{effectiveName}_{HashCode}";
            }
            return _cachedId;
        }
    }

    public override ShaderSource GenerateShaderSource(ShaderGeneratorContext context, MaterialComputeColorKeys baseKeys)
    {
        AbstractToShaderFX<T> toShaderFx;
        if (this is IComputeVoid)
            toShaderFx = new ToComputeFx<T>(this);
        else
            toShaderFx = new ToShaderFX<T>(this);

        var source = toShaderFx.GenerateShaderSource(context, baseKeys);
        ShaderCode = toShaderFx.ShaderCode;
        return source;
    }

    static IMonadicValue<T> IMonadicValue<T>.Create(NodeContext nodeContext, T value)
    {
        var m = (IMonadicValue<T>)Create_NonGeneric(nodeContext);
        return m.SetValue(value);

        static AbstractShaderNode Create_NonGeneric(NodeContext nodeContext)
        {
            // Can't call the constructor directly due to value type constraint
            if (typeof(T).IsValueType)
                return (AbstractShaderNode)Activator.CreateInstance(typeof(ValueInput<>).MakeGenericType(typeof(T)),
                    nodeContext);

            // Read: if (T is Buffer)
            if (typeof(Buffer).IsAssignableFrom(typeof(T)))
            {
                // Can't call the constructor directly due to value type constraint
                var builderType = typeof(DelegatingBufferInput<>);
                return (AbstractShaderNode)Activator.CreateInstance(builderType.MakeGenericType(typeof(T)),
                    nodeContext);
            }

            if (typeof(T) == typeof(Texture))
                return new DelegatingTextureInput(nodeContext);

            if (typeof(T) == typeof(SamplerState))
                return new SamplerInput(nodeContext);

            if (typeof(T) == typeof(GpuStruct))
                return new DynamicStruct<GpuStruct>(nodeContext, new Dictionary<string, AbstractShaderNode>(), null);

            if (typeof(T) == typeof(GpuVoid))
                return new EmptyVoid(nodeContext);

            /*
            if (typeof(T) == typeof(Buffer))
                return new BufferGpuValueBuilder<T>() as IMonadBuilder<T, GpuValue<T>>;
            */
            throw new NotImplementedException(typeof(T).FullName + "Not Implemented");
        }
    }

    bool IMonadicValue.HasValue => HasValue();

    bool IMonadicValue.AcceptsValue => true;

    T IMonadicValue<T>.Value => GetValue();

    IMonadicValue<T> IMonadicValue<T>.SetValue(T value)
    {
        return SetValue(value);
    }

    protected override Dictionary<string, string> CreateTemplateMap()
    {
        return new Dictionary<string, string>
        {
            { "resultName", ID },
            { "resultType", TypeName() },
            { "default", Default == null ? "" : Default.ID },
            { "arguments", ShaderNodesUtil.BuildArguments(Ins) }
        };
    }

    public override string TypeName()
    {
        return typeof(T) == typeof(GpuStruct) || typeof(T) == typeof(Buffer)
            ? TypeOverride
            : TypeHelpers.GetGpuType<T>();
    }

    protected override string SourceTemplate()
    {
        return "";
    }

    public override int Dimension()
    {
        return TypeHelpers.GetDimension<T>();
    }

    protected virtual T GetValue()
    {
        return default;
    }

    protected virtual ShaderNode<T> SetValue(T value)
    {
        return this;
    }

    protected virtual bool HasValue()
    {
        return false;
    }
}

/// <summary>
/// Base class for shader nodes that produce a result value via an expression.
/// </summary>
/// <typeparam name="T">The result type of the node.</typeparam>
/// <remarks>
/// <para>
/// ResultNode automatically generates the assignment statement:
/// <c>${resultType} ${resultName} = ${implementation};</c>
/// </para>
/// <para>
/// Derived classes only need to override <see cref="ImplementationTemplate"/> to provide
/// the expression that computes the result.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class Multiply&lt;T&gt; : ResultNode&lt;T&gt; where T : struct
/// {
///     public Multiply(NodeContext ctx, ShaderNode&lt;T&gt; a, ShaderNode&lt;T&gt; b)
///         : base(ctx, "Multiply")
///     {
///         SetInputs(new AbstractShaderNode[] { a, b });
///     }
///
///     protected override string ImplementationTemplate() => "${a} * ${b}";
/// }
/// </code>
/// </example>
public abstract class ResultNode<T> : ShaderNode<T>
{
    /// <summary>
    /// Initializes a new result node.
    /// </summary>
    /// <param name="nodeContext">The VL node context.</param>
    /// <param name="theId">The base name for this node.</param>
    /// <param name="theDefault">Optional default value.</param>
    /// <param name="theCreateDefault">Whether to create a default value if none provided.</param>
    protected ResultNode(NodeContext nodeContext, string theId, ShaderNode<T> theDefault = null,
        bool theCreateDefault = true) : base(nodeContext, theId, theDefault, theCreateDefault)
    {
    }

    /// <inheritdoc />
    protected override string SourceTemplate()
    {
        // Skip variable declaration for inlined nodes
        if (ShouldInline() == InlineDecision.Inline)
            return "";

        return ShaderNodesUtil.Evaluate(
            "${resultType} ${resultName} = ${implementation};",
            new Dictionary<string, string>
            {
                { "implementation", ImplementationTemplate() }
            });
    }

    /// <inheritdoc />
    protected override string GetInlineExpression(int depth)
    {
        // Return the implementation template wrapped in parentheses for safety
        var impl = ImplementationTemplate();
        // If already parenthesized or simple identifier, don't double-wrap
        if (impl.StartsWith("(") && impl.EndsWith(")"))
            return impl;
        return $"({impl})";
    }

    protected abstract string ImplementationTemplate();
}

public class ComputeNode<T> : ShaderNode<T>, IComputeVoid
{
    public ComputeNode(NodeContext nodeContext, string theId,
        ShaderNode<T> theDefault = null) : base(nodeContext, theId, theDefault)
    {
    }
}
