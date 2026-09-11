using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Stride.Rendering.Materials;
using Stride.Rendering.Materials.ComputeColors;
using Stride.Shaders;
using VL.Stride.Shaders.ShaderFX;
using Buffer = Stride.Graphics.Buffer;

namespace Fuse.ShaderFX;

public abstract class AbstractStage
{
    public readonly string Key;

    public AbstractShaderNode StageNode;

    public AbstractStage(string theKey, AbstractShaderNode theShaderNode)
    {
        Key = theKey;
        StageNode = theShaderNode;
        Ticket = 0;
    }

    public int Ticket { get; private set; }

    public virtual void AppendInputs(Dictionary<string, string> theTemplateMap)
    {
    }

    public abstract string Source();

    public void ChangeGraph(AbstractShaderNode theNode)
    {
        Ticket++;
    }
}

public abstract class AbstractToShaderFX<T> : IComputeValue<T>
{
    private readonly HashSet<string> _compositions = [];
    private readonly HashSet<string> _constantArrays = [];

    private readonly Dictionary<string, string> _customTemplate;

    private readonly HashSet<string> _declarations = [];
    private readonly Dictionary<string, string> _functionMap = new();
    private readonly HashSet<string> _groupDeclarations = [];

    private readonly bool _isCompute;
    private readonly HashSet<string> _mixins = [];

    private readonly string _sourceTemplate;

    private readonly List<AbstractStage> _stages;

    private readonly Stopwatch _stopwatch = new();
    private readonly HashSet<string> _streams = [];
    private readonly HashSet<string> _structs = [];

    protected AbstractToShaderFX(
        List<AbstractStage> theStages,
        Dictionary<string, string> theCustomTemplate,
        bool theIsCompute,
        string theSource)
    {
        _customTemplate = theCustomTemplate;
        _isCompute = theIsCompute;

        _stages = theStages;
        _sourceTemplate = theSource;

        _stages = theStages;
        Inputs = new Dictionary<string, AbstractShaderNode>();
        var stageCodeMap = new Dictionary<string, string>();
        foreach (var stage in _stages)
        {
            if (stage?.StageNode == null) continue;
            Inputs.Add(stage.Key, stage.StageNode);
            stageCodeMap.Add("stage" + stage.Key, stage.Source());
        }

        _sourceTemplate = ShaderNodesUtil.Evaluate(_sourceTemplate, stageCodeMap);
    }

    public string ShaderCode { get; private set; }

    public string ShaderName { get; private set; }

    public Dictionary<string, AbstractShaderNode> Inputs { get; }

    // private ParameterCollection _parameters;

    /// <summary>
    ///     Gets the children.
    /// </summary>
    /// <param name="context">The context to get the children.</param>
    /// <returns>The list of children.</returns>
    public virtual IEnumerable<IComputeNode> GetChildren(object context = null)
    {
        return Enumerable.Empty<ComputeNode>();
    }

    public ShaderSource GenerateShaderSource(ShaderGeneratorContext theContext, MaterialComputeColorKeys baseKeys)
    {
        //MeasureProfiler.StartCollectingData();
        _stopwatch.Reset();

        // Reset caches for new compilation
        AbstractShaderNode.ResetBuildSourceCodeCache();

        _stopwatch.Start();
        var watch = new Stopwatch();
        watch.Start();
        if (ShaderNodesUtil.TimeShaderGeneration) Console.WriteLine($"-> Start Generating Shader {ShaderName}");
        var sourceStream = new Dictionary<string, (string source, string stream)>();
        var streamDefinesBuilder = new StringBuilder();


        foreach (var kv in Inputs)
        {
            var shaderInput = kv.Value;
            // Use unified compilation that does CheckHashCodes + CheckContext + property collection in one pass
            HandleShader(_isCompute, theContext, shaderInput, kv.Key, out var source, out var stream, out var streamDefines);
            sourceStream.Add(kv.Key, (source, stream));
            streamDefinesBuilder.AppendLine(streamDefines);
        }

        _stopwatch.Restart();
        var templateMap = BuildTemplateMap();
        templateMap["streamDeclaration"] = streamDefinesBuilder.ToString();
        CustomTemplates().ForEach(kv => { templateMap.Add(kv.Key, kv.Value); });

        sourceStream.ForEach(kv =>
        {
            templateMap.Add("source" + kv.Key, kv.Value.source);
            templateMap.Add("streams" + kv.Key, kv.Value.stream);
        });
        ShaderCode = ShaderNodesUtil.Evaluate(_sourceTemplate, templateMap);
        // ReSharper disable once VirtualMemberCallInConstructor
        ShaderCode = CheckCode(ShaderCode);
        ShaderCode = ShaderNodesUtil.FormatShaderCode(ShaderCode);
        ShaderName = "Shader_" + System.Math.Abs(ShaderCode.GetStableHashCode());
        //ShaderName = "Shader_" + ShaderNodesUtil.GetHashCode(_input.NodeContext);
        ShaderCode =
            ShaderNodesUtil.Evaluate(ShaderCode, new Dictionary<string, string> { { "shaderID", ShaderName } });

        ShaderCode =
            ShaderNodesUtil.Evaluate(ShaderCode, m => m.Groups["key"].Value.StartsWith("stage") ? "" : m.Value);

        if (ShaderNodesUtil.ValidateGeneratedShaderSource &&
            !ShaderNodesUtil.ValidateGeneratedShaderCode(ShaderCode, out var validationReason))
        {
            Logging.FuseLogger.Warning($"Generated shader {ShaderName} failed validation: {validationReason}");
            ShaderNodesUtil.DumpShaderSource(ShaderName, ShaderCode, "invalid");
            if (ShaderNodesUtil.ThrowOnInvalidGeneratedShader)
            {
                var validationException =
                    new InvalidOperationException($"Generated shader {ShaderName} failed validation: {validationReason}");
                ShaderNodesUtil.DumpShaderException(ShaderName, "validation", validationException);
                throw validationException;
            }
        }

        var shaderPhase = _isCompute ? "compute" : "draw";
        var sourcePath = "shaders\\" + ShaderName + ".sdsl";
        ShaderNodesUtil.DumpShaderSource(ShaderName, ShaderCode, shaderPhase);
        ShaderNodesUtil.DumpShaderCompileAttempt(ShaderName, ShaderCode, shaderPhase, sourcePath);

        foreach (var kv in Inputs) kv.Value.ShaderCode = ShaderCode;
        if (ShaderNodesUtil.TimeShaderGeneration)
            Console.WriteLine($"-> Evaluate: {_stopwatch.ElapsedMilliseconds} ms");

        _stopwatch.Restart();
        ShaderNodesUtil.AddShaderSource(ShaderName, ShaderCode, sourcePath);
        if (ShaderNodesUtil.TimeShaderGeneration)
            Console.WriteLine($"-> AddShaderSource: {_stopwatch.ElapsedMilliseconds} ms");
        // _parameters = theContext.Parameters;

        // _input.InputList().ForEach(input => input.AddParameters(_parameters));
        var result = new ShaderClassSource(ShaderName);
        _stopwatch.Stop();

        if (ShaderNodesUtil.TimeShaderGeneration)
            Console.WriteLine($"-> Execution Time: {watch.ElapsedMilliseconds} ms for Shader {ShaderName}");
        //MeasureProfiler.SaveData(ShaderName);
        return result;
    }

    public void AppendInputs(Dictionary<string, string> theTemplateMap)
    {
        foreach (var stage in _stages) stage?.AppendInputs(theTemplateMap);
    }

    public Dictionary<string, string> AppendTemplateValues(Dictionary<string, string> theTemplateMap)
    {
        theTemplateMap["shaderType"] = TypeHelpers.GetSignature<T>();
        theTemplateMap["resultType"] = TypeHelpers.GetGpuType<T>();

        AppendInputs(theTemplateMap);

        return theTemplateMap;
    }

    private Dictionary<string, string> CustomTemplates()
    {
        return AppendTemplateValues(_customTemplate);
    }

    private Dictionary<string, string> BuildTemplateMap()
    {
        var declarationBuilder = new StringBuilder();
        _declarations.ForEach(declaration => declarationBuilder.AppendLine(declaration));

        var groupDeclarationBuilder = new StringBuilder();
        _groupDeclarations.ForEach(declaration => groupDeclarationBuilder.AppendLine(declaration));

        var structBuilder = new StringBuilder();
        _structs.ForEach(gpuStruct => structBuilder.AppendLine(gpuStruct));

        var constantArrayBuilder = new StringBuilder();
        _constantArrays.ForEach(array => constantArrayBuilder.AppendLine(array));

        var streamBuilder = new StringBuilder();
        _streams.ForEach(stream => streamBuilder.AppendLine(stream));

        var mixinBuilder = new StringBuilder();
        _mixins.ForEach(mixin => mixinBuilder.Append(", ").Append(mixin));

        var compositionBuilder = new StringBuilder();
        _compositions.ForEach(composition => compositionBuilder.AppendLine(composition));

        var functionBuilder = new StringBuilder();
        _functionMap?.ForEach(kv => functionBuilder.AppendLine(kv.Value));


        return new Dictionary<string, string>
        {
            { "mixins", mixinBuilder.ToString() },
            { "declarations", declarationBuilder.ToString() },
            { "compositions", compositionBuilder.ToString() },
            { "structs", structBuilder.ToString() },
            { "constantArrays", constantArrayBuilder.ToString() },
            { "streams", streamBuilder.ToString() },
            { "functions", functionBuilder.ToString() },
            { "groupDeclarations", groupDeclarationBuilder.ToString() }
        };
    }

    private void HandleDeclaration(FieldDeclaration theDeclaration, bool theIsComputeShader)
    {
        if (theDeclaration.IsResource)
            _groupDeclarations.Add(theDeclaration.GetDeclaration(theIsComputeShader));
        else
            _declarations.Add(theDeclaration.GetDeclaration(theIsComputeShader));
    }

    private void HandleFunction(KeyValuePair<string, string> theKeyFunction)
    {
        if (!_functionMap.ContainsKey(theKeyFunction.Key)) _functionMap.Add(theKeyFunction.Key, theKeyFunction.Value);
    }

    private void HandleShader(bool theIsComputeShader, ShaderGeneratorContext theContext,
        AbstractShaderNode theShaderInput, string theKey,
        out string theSource, out string theStreams, out string theDefinedStreams)
    {
        var handleShaderWatch = new Stopwatch();
        handleShaderWatch.Start();
        if (ShaderNodesUtil.TimeShaderGeneration) Console.WriteLine($"  -> HandleShader: {theKey}");
        var streamBuilder = new StringBuilder();
        var streamDeclareBuilder = new StringBuilder();

        // Single unified traversal that collects all properties, validates IDs, and passes context
        // This replaces 9 separate graph traversals with 1
        _stopwatch.Restart();
        var compiled = theShaderInput.CompileProperties(theContext);
        if (ShaderNodesUtil.TimeShaderGeneration)
            Console.WriteLine($"     CompileProperties (unified): {_stopwatch.ElapsedMilliseconds} ms");

        // Must run before the declarations are collected below: it rewrites the duplicates' IDs,
        // and the FieldDeclaration objects in compiled.Declarations are the very ones it mutates.
        CollapseDuplicateBufferInputs(compiled.Inputs);

        // Process collected properties
        _stopwatch.Restart();
        compiled.Declarations.ForEach(declaration => HandleDeclaration(declaration, theIsComputeShader));
        compiled.Structs.ForEach(value => _structs.Add(value));
        compiled.ConstantArrays.ForEach(value => _constantArrays.Add(value));
        compiled.Streams.ForEach(value => _streams.Add(value));
        compiled.Mixins.ForEach(value => _mixins.Add(value));
        compiled.Functions.ForEach(HandleFunction);
        if (ShaderNodesUtil.TimeShaderGeneration)
            Console.WriteLine($"     Process properties: {_stopwatch.ElapsedMilliseconds} ms");

        _stopwatch.Restart();
        streamBuilder.AppendLine("        streams. = " + theKey + theShaderInput.ID + ";");
        streamDeclareBuilder.AppendLine("    stream " + TypeHelpers.GetGpuType(theShaderInput) + " " + theKey + ";");

        theSource = theShaderInput.BuildSourceCode();
        theStreams = streamBuilder.ToString();
        theDefinedStreams = streamDeclareBuilder.ToString();
        if (ShaderNodesUtil.TimeShaderGeneration)
            Console.WriteLine($"     Finish Stage: {handleShaderWatch.ElapsedMilliseconds} ms");
    }

    /// <summary>
    /// Makes every shader input that wraps the same GPU buffer with the same declaration share one
    /// ID, so the buffer is declared once per shader instead of once per place it is consumed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A raw <c>Stride.Graphics.Buffer</c> is re-wrapped into a fresh <c>BufferInput</c> every time
    /// it reaches the shader graph - once per <c>BufferIn</c> node and once per implicit monadic
    /// conversion. Each wrapper takes its ID from its own node context, so a buffer that is both
    /// written and read inside one compute shader is emitted as two independent declarations and
    /// bound to two resource slots.
    /// </para>
    /// <para>
    /// D3D11 rejects that: binding one resource to two UAV slots raises
    /// <c>DEVICE_CSSETUNORDEREDACCESSVIEWS_HAZARD</c> and one of the two slots is forced to NULL. If
    /// the slot that loses is the write, the buffer silently stops being updated while everything
    /// else in the same dispatch keeps working - which is exactly what the Gaussian splat decode hit
    /// (see RC-03.PRELOADER-PERFORMANCE-AND-COLOR-BUFFER.md 6.4a): the colour buffer's write binding
    /// was force-NULLed on every frame, so splats kept the previous clip's colours.
    /// </para>
    /// <para>
    /// Scope is deliberately narrow. Inputs are only merged when they wrap the *same buffer object*
    /// and generate the *same declaration text*; a read-only and a read-write view of one buffer are
    /// left alone, because merging those would silently change one of them. The map is rebuilt from
    /// scratch on every generation and the graph traversal order is deterministic, so the winner is
    /// stable across regenerations and the collapsed ID is already in place in the very first
    /// generated shader - this adds no regeneration of its own.
    /// </para>
    /// </remarks>
    private void CollapseDuplicateBufferInputs(IEnumerable<IGpuInput> theInputs)
    {
        Dictionary<(Buffer, string), AbstractShaderNode> owners = null;

        foreach (var input in theInputs)
        {
            if (input is not IBufferInputIdentity identity) continue;
            if (input is not AbstractShaderNode node) continue;

            var buffer = identity.BufferValue;
            if (buffer == null) continue;

            owners ??= new Dictionary<(Buffer, string), AbstractShaderNode>();

            var key = (buffer, identity.DeclarationTypeName);
            if (!owners.TryGetValue(key, out var owner))
            {
                owners[key] = node;
                continue;
            }

            if (ReferenceEquals(owner, node)) continue;
            if (node.ID == owner.ID) continue; // already sharing the owner's identity

            node.HashCode = owner.HashCode;
            node.Name = owner.Name;
            // Keeps ValidateNodeId from disambiguating the now-identical hash back apart on the
            // next traversal.
            node.HasFixedName = true;
            node.InvalidateId();
            // Rebuilds the ParameterKey and the FieldDeclaration from the shared ID. The declaration
            // text now matches the owner's, so the declaration HashSet folds them into one.
            input.OnUpdateName();
        }
    }

    private string CheckCode(string theCode)
    {
        return _isCompute ? theCode : theCode.Replace("RWStructuredBuffer", "StructuredBuffer");
    }
}
