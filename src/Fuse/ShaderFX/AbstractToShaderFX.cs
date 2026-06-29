using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Stride.Rendering.Materials;
using Stride.Rendering.Materials.ComputeColors;
using Stride.Shaders;
using VL.Stride.Shaders.ShaderFX;

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
    private readonly OrderedUniqueCollection<string> _compositions = [];
    private readonly OrderedUniqueCollection<string> _constantArrays = [];

    private readonly Dictionary<string, string> _customTemplate;

    private readonly OrderedUniqueCollection<string> _declarations = [];
    private readonly Dictionary<string, string> _functionMap = new();
    private readonly OrderedUniqueCollection<string> _groupDeclarations = [];

    private readonly bool _isCompute;
    private readonly OrderedUniqueCollection<string> _mixins = [];

    private readonly string _sourceTemplate;

    private readonly List<AbstractStage> _stages;

    private readonly Stopwatch _stopwatch = new();
    private readonly OrderedUniqueCollection<string> _streams = [];
    private readonly OrderedUniqueCollection<string> _structs = [];

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

    public ShaderDiagnosticContext LastDiagnosticContext { get; private set; }

    public bool RegisterGeneratedShaderSource { get; set; } = true;

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
        var stageDiagnostics = new List<ShaderStageCompilationDiagnostic>();
        var timings = new List<ShaderTimingDiagnostic>();


        foreach (var kv in Inputs)
        {
            var shaderInput = kv.Value;
            // Use unified compilation that does CheckHashCodes + CheckContext + property collection in one pass
            HandleShader(_isCompute, theContext, shaderInput, kv.Key, out var source, out var stream, out var streamDefines,
                out var compiled, timings);
            sourceStream.Add(kv.Key, (source, stream));
            streamDefinesBuilder.AppendLine(streamDefines);
            stageDiagnostics.Add(new ShaderStageCompilationDiagnostic(
                ShaderStageDiagnostic.FromStage(kv.Key, shaderInput),
                compiled));
        }

        _stopwatch.Restart();
        var templateWatch = Stopwatch.StartNew();
        var templateMap = BuildTemplateMap();
        templateMap["streamDeclaration"] = streamDefinesBuilder.ToString();
        CustomTemplates().ForEach(kv => { templateMap.Add(kv.Key, kv.Value); });

        sourceStream.ForEach(kv =>
        {
            templateMap.Add("source" + kv.Key, kv.Value.source);
            templateMap.Add("streams" + kv.Key, kv.Value.stream);
        });
        timings.Add(new ShaderTimingDiagnostic("BuildTemplateMap", templateWatch.Elapsed.TotalMilliseconds));

        var evaluateWatch = Stopwatch.StartNew();
        ShaderCode = ShaderNodesUtil.Evaluate(_sourceTemplate, templateMap);
        timings.Add(new ShaderTimingDiagnostic("EvaluateTemplate", evaluateWatch.Elapsed.TotalMilliseconds));

        var checkCodeWatch = Stopwatch.StartNew();
        // ReSharper disable once VirtualMemberCallInConstructor
        ShaderCode = CheckCode(ShaderCode);
        timings.Add(new ShaderTimingDiagnostic("CheckCode", checkCodeWatch.Elapsed.TotalMilliseconds));

        var formatWatch = Stopwatch.StartNew();
        ShaderCode = ShaderNodesUtil.FormatShaderCode(ShaderCode);
        timings.Add(new ShaderTimingDiagnostic("FormatShaderCode", formatWatch.Elapsed.TotalMilliseconds));

        var nameWatch = Stopwatch.StartNew();
        ShaderName = "Shader_" + System.Math.Abs(ShaderCode.GetStableHashCode());
        timings.Add(new ShaderTimingDiagnostic("CreateShaderName", nameWatch.Elapsed.TotalMilliseconds));
        //ShaderName = "Shader_" + ShaderNodesUtil.GetHashCode(_input.NodeContext);

        var shaderIdWatch = Stopwatch.StartNew();
        ShaderCode =
            ShaderNodesUtil.Evaluate(ShaderCode, new Dictionary<string, string> { { "shaderID", ShaderName } });
        timings.Add(new ShaderTimingDiagnostic("ApplyShaderId", shaderIdWatch.Elapsed.TotalMilliseconds));

        var cleanupWatch = Stopwatch.StartNew();
        ShaderCode =
            ShaderNodesUtil.Evaluate(ShaderCode, m => m.Groups["key"].Value.StartsWith("stage") ? "" : m.Value);
        timings.Add(new ShaderTimingDiagnostic("RemoveUnresolvedStagePlaceholders", cleanupWatch.Elapsed.TotalMilliseconds));

        var diagnosticWarnings = new List<string>();
        if (ShaderNodesUtil.ValidateGeneratedShaderSource &&
            !ShaderNodesUtil.ValidateGeneratedShaderCode(ShaderCode, out var validationReason))
        {
            diagnosticWarnings.Add("Generated shader failed validation: " + validationReason);
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

        foreach (var kv in Inputs) kv.Value.ShaderCode = ShaderCode;
        if (ShaderNodesUtil.TimeShaderGeneration)
            Console.WriteLine($"-> Evaluate: {_stopwatch.ElapsedMilliseconds} ms");

        _stopwatch.Restart();
        var addSourceWatch = Stopwatch.StartNew();
        if (RegisterGeneratedShaderSource)
            ShaderNodesUtil.AddShaderSource(ShaderName, ShaderCode, sourcePath);
        timings.Add(new ShaderTimingDiagnostic(
            RegisterGeneratedShaderSource ? "AddShaderSource" : "AddShaderSourceSkipped",
            addSourceWatch.Elapsed.TotalMilliseconds));
        timings.Add(new ShaderTimingDiagnostic("GenerateShaderSourceTotal", watch.Elapsed.TotalMilliseconds));
        if (ShaderNodesUtil.TimeShaderGeneration)
            Console.WriteLine($"-> AddShaderSource: {_stopwatch.ElapsedMilliseconds} ms");

        LastDiagnosticContext = ShaderDiagnosticContext.Create(
            ShaderName,
            shaderPhase,
            sourcePath,
            _isCompute,
            ShaderCode,
            stageDiagnostics,
            diagnosticWarnings,
            timings);
        ShaderNodesUtil.DumpShaderSource(ShaderName, ShaderCode, shaderPhase);
        ShaderNodesUtil.DumpShaderCompileAttempt(ShaderName, ShaderCode, shaderPhase, sourcePath);
        ShaderNodesUtil.DumpShaderDiagnostics(LastDiagnosticContext);
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
        out string theSource, out string theStreams, out string theDefinedStreams,
        out ShaderCompilationResult compiled,
        List<ShaderTimingDiagnostic> timings)
    {
        var handleShaderWatch = new Stopwatch();
        handleShaderWatch.Start();
        if (ShaderNodesUtil.TimeShaderGeneration) Console.WriteLine($"  -> HandleShader: {theKey}");
        var streamBuilder = new StringBuilder();
        var streamDeclareBuilder = new StringBuilder();

        // Single unified traversal that collects all properties, validates IDs, and passes context
        // This replaces 9 separate graph traversals with 1
        _stopwatch.Restart();
        var compilePropertiesWatch = Stopwatch.StartNew();
        compiled = theShaderInput.CompileProperties(theContext);
        timings.Add(new ShaderTimingDiagnostic($"Stage:{theKey}:CompileProperties", compilePropertiesWatch.Elapsed.TotalMilliseconds));
        if (ShaderNodesUtil.TimeShaderGeneration)
            Console.WriteLine($"     CompileProperties (unified): {_stopwatch.ElapsedMilliseconds} ms");

        // Process collected properties
        _stopwatch.Restart();
        var processPropertiesWatch = Stopwatch.StartNew();
        compiled.Declarations.ForEach(declaration => HandleDeclaration(declaration, theIsComputeShader));
        compiled.Structs.ForEach(value => _structs.Add(value));
        compiled.ConstantArrays.ForEach(value => _constantArrays.Add(value));
        compiled.Streams.ForEach(value => _streams.Add(value));
        compiled.Mixins.ForEach(value => _mixins.Add(value));
        compiled.Functions.ForEach(HandleFunction);
        timings.Add(new ShaderTimingDiagnostic($"Stage:{theKey}:ProcessProperties", processPropertiesWatch.Elapsed.TotalMilliseconds));
        if (ShaderNodesUtil.TimeShaderGeneration)
            Console.WriteLine($"     Process properties: {_stopwatch.ElapsedMilliseconds} ms");

        _stopwatch.Restart();
        streamBuilder.AppendLine("        streams. = " + theKey + theShaderInput.ID + ";");
        streamDeclareBuilder.AppendLine("    stream " + TypeHelpers.GetGpuType(theShaderInput) + " " + theKey + ";");

        var buildSourceWatch = Stopwatch.StartNew();
        theSource = theShaderInput.BuildSourceCode();
        timings.Add(new ShaderTimingDiagnostic($"Stage:{theKey}:BuildSourceCode", buildSourceWatch.Elapsed.TotalMilliseconds));
        theStreams = streamBuilder.ToString();
        theDefinedStreams = streamDeclareBuilder.ToString();
        timings.Add(new ShaderTimingDiagnostic($"Stage:{theKey}:Total", handleShaderWatch.Elapsed.TotalMilliseconds));
        if (ShaderNodesUtil.TimeShaderGeneration)
            Console.WriteLine($"     Finish Stage: {handleShaderWatch.ElapsedMilliseconds} ms");
    }

    private string CheckCode(string theCode)
    {
        return _isCompute ? theCode : theCode.Replace("RWStructuredBuffer", "StructuredBuffer");
    }
}
