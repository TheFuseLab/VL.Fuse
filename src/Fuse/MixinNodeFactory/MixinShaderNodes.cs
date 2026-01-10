using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using Fuse.function;
using Stride.Core.Mathematics;
using Stride.Graphics;
using VL.Core;
using VL.Core.CompilerServices;
using Buffer = Stride.Graphics.Buffer;

namespace Fuse.MixinNodeFactory;

/// <summary>
/// VL Node Factory that generates nodes from SDSL mixin files with @export annotated functions.
/// Register this in VL using: &lt;NodeFactoryDependency Location="Fuse.MixinNodeFactory.MixinShaderNodes" /&gt;
/// </summary>
public static class MixinShaderNodes
{
    static MixinShaderNodes()
    {
        MixinNodeFactoryLogger.Log("MixinShaderNodes static constructor called");
    }

    /// <summary>
    /// Initialize the node factory. Called by VL runtime.
    /// </summary>
    public static NodeBuilding.FactoryImpl Init(
        IVLNodeDescriptionFactory factory)
    {
        MixinNodeFactoryLogger.Log("Init called with IVLNodeDescriptionFactory");

        return NodeBuilding.NewFactoryImpl(
            nodes: ImmutableArray<IVLNodeDescription>.Empty,
            forPath: path =>
            {
                MixinNodeFactoryLogger.Log($"forPath called with: {path}");

                return f =>
                {
                    MixinNodeFactoryLogger.Log($"Factory delegate called for path: {path}");

                    // Find all 'shaders' directories within the project path
                    var shaderDirectories = FindShaderDirectories(path);

                    if (shaderDirectories.Count == 0)
                    {
                        MixinNodeFactoryLogger.Log($"No shaders directories found in: {path}");
                        return NodeBuilding.NewFactoryImpl(ImmutableArray<IVLNodeDescription>.Empty);
                    }

                    MixinNodeFactoryLogger.Log($"Found {shaderDirectories.Count} shaders directories");

                    // Get nodes from all shaders folders
                    try
                    {
                        var allNodes = new List<IVLNodeDescription>();
                        var allWatchers = new List<IObservable<object>>();

                        foreach (var shadersPath in shaderDirectories)
                        {
                            MixinNodeFactoryLogger.Log($"Scanning: {shadersPath}");
                            var nodes = GetNodeDescriptions(f, shadersPath).ToList();
                            MixinNodeFactoryLogger.Log($"Found {nodes.Count} nodes in {shadersPath}");
                            allNodes.AddRange(nodes);
                            allWatchers.Add(WatchShaderDirectory(shadersPath));
                        }

                        MixinNodeFactoryLogger.Log($"Total nodes found: {allNodes.Count}");

                        // Combine all file watchers
                        var invalidated = allWatchers.Count > 0
                            ? Observable.Merge(allWatchers)
                            : Observable.Empty<object>();

                        return NodeBuilding.NewFactoryImpl(
                            nodes: allNodes.ToImmutableArray(),
                            invalidated: invalidated,
                            export: exportContext =>
                            {
                                // Export shader files when packaging
                                var assetsPath = Path.Combine(path, "Assets", "Effects");
                                Directory.CreateDirectory(assetsPath);

                                foreach (var shadersPath in shaderDirectories)
                                {
                                    foreach (var file in Directory.GetFiles(shadersPath, "*.sdsl"))
                                    {
                                        var destFile = Path.Combine(assetsPath, Path.GetFileName(file));
                                        if (!File.Exists(destFile))
                                            File.Copy(file, destFile);
                                    }
                                }
                            });
                    }
                    catch (Exception ex)
                    {
                        MixinNodeFactoryLogger.Log($"Error scanning shaders: {ex}");
                        return NodeBuilding.NewFactoryImpl(ImmutableArray<IVLNodeDescription>.Empty);
                    }
                };
            });
    }

    /// <summary>
    /// Finds all directories named "shaders" within the given path AND sibling paths.
    /// This ensures project-local shaders are found regardless of which forPath is called.
    /// </summary>
    private static List<string> FindShaderDirectories(string basePath)
    {
        var result = new List<string>();

        if (!Directory.Exists(basePath))
            return result;

        try
        {
            // Determine the root to scan - go up to find the project root
            // Look for common project indicators (*.vl files, .git, etc.)
            var scanRoot = FindProjectRoot(basePath) ?? basePath;
            MixinNodeFactoryLogger.Log($"Scanning from root: {scanRoot} (called with: {basePath})");

            // Check for direct 'shaders' folder first
            var directShaders = Path.Combine(scanRoot, "shaders");
            if (Directory.Exists(directShaders))
            {
                result.Add(directShaders);
            }

            // Skip common folders that shouldn't be scanned
            var skipFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "bin", "obj", "node_modules", ".git", ".vs", "packages", "PatchTests"
            };

            // Scan all subdirectories for 'shaders' folders
            foreach (var dir in Directory.EnumerateDirectories(scanRoot, "shaders", SearchOption.AllDirectories))
            {
                // Check if any parent folder should be skipped
                var relativePath = dir.Substring(scanRoot.Length);
                var shouldSkip = skipFolders.Any(skip =>
                    relativePath.Contains(Path.DirectorySeparatorChar + skip + Path.DirectorySeparatorChar) ||
                    relativePath.StartsWith(Path.DirectorySeparatorChar + skip));

                if (!shouldSkip && !result.Contains(dir))
                {
                    result.Add(dir);
                }
            }
        }
        catch (Exception ex)
        {
            MixinNodeFactoryLogger.Log($"Error finding shader directories: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Finds the project root by looking for VL files or .git folder.
    /// </summary>
    private static string FindProjectRoot(string path)
    {
        var current = path;
        string lastWithVlFiles = null;

        while (!string.IsNullOrEmpty(current))
        {
            // Check if this directory has .vl files (indicates a VL project)
            try
            {
                if (Directory.GetFiles(current, "*.vl", SearchOption.TopDirectoryOnly).Length > 0)
                {
                    lastWithVlFiles = current;
                }

                // If we find a .git folder, this is likely the repo root
                if (Directory.Exists(Path.Combine(current, ".git")))
                {
                    return current;
                }
            }
            catch
            {
                // Ignore access errors
            }

            var parent = Directory.GetParent(current);
            if (parent == null || parent.FullName == current)
                break;
            current = parent.FullName;
        }

        // Return the last directory with VL files, or the original path
        return lastWithVlFiles ?? path;
    }

    private static IObservable<object> WatchShaderDirectory(string shadersPath)
    {
        if (!Directory.Exists(shadersPath))
            return Observable.Empty<object>();

        return Observable.Create<object>(observer =>
        {
            var watcher = new FileSystemWatcher(shadersPath, "*.sdsl")
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime
            };

            void OnChanged(object sender, FileSystemEventArgs e) => observer.OnNext(e);
            void OnRenamed(object sender, RenamedEventArgs e) => observer.OnNext(e);

            watcher.Created += OnChanged;
            watcher.Changed += OnChanged;
            watcher.Deleted += OnChanged;
            watcher.Renamed += OnRenamed;

            watcher.EnableRaisingEvents = true;

            return () =>
            {
                watcher.EnableRaisingEvents = false;
                watcher.Created -= OnChanged;
                watcher.Changed -= OnChanged;
                watcher.Deleted -= OnChanged;
                watcher.Renamed -= OnRenamed;
                watcher.Dispose();
            };
        }).Throttle(TimeSpan.FromMilliseconds(300));
    }

    private static IEnumerable<IVLNodeDescription> GetNodeDescriptions(
        IVLNodeDescriptionFactory factory,
        string shadersPath)
    {
        List<MixinFunctionInfo> functions;
        try
        {
            functions = MixinNodeFactory.ScanDirectory(shadersPath, recursive: true);
        }
        catch
        {
            yield break;
        }

        foreach (var func in functions)
        {
            IVLNodeDescription node;
            try
            {
                node = CreateNodeDescription(factory, func);
            }
            catch (Exception ex)
            {
                MixinNodeFactoryLogger.Log($"ERROR creating node {func.Name}: {ex.Message}");
                continue;
            }

            yield return node;
        }
    }

    private static IVLNodeDescription CreateNodeDescription(
        IVLNodeDescriptionFactory factory,
        MixinFunctionInfo func)
    {
        var category = MixinNodeFactory.GetCategory(func);
        var name = func.Name;
        var summary = func.Metadata.Summary ?? $"Mixin function from {func.MixinName}";

        MixinNodeFactoryLogger.Log($"Creating node: {name} in category: {category}");

        return factory.NewNodeDescription(
            name: name,
            category: category,
            fragmented: false,
            invalidated: Observable.Empty<object>(),
            init: buildContext => CreateNodeImplementation(buildContext, func, summary));
    }

    private static NodeBuilding.NodeImplementation CreateNodeImplementation(
        NodeBuilding.NodeDescriptionBuildContext ctx,
        MixinFunctionInfo func,
        string summary)
    {
        // Build input pins from function parameters (excluding pure out params)
        // In and InOut get input pins, Out does not
        var inputs = new List<IVLPinDescription>();
        var inputParamIndices = new List<int>(); // Track which parameter each input corresponds to

        for (int i = 0; i < func.Parameters.Count; i++)
        {
            var param = func.Parameters[i];
            if (param.Modifier != InputModifier.Out)
            {
                var pinType = GetPinType(param);
                var pinSummary = BuildPinSummary(param, func.Metadata);
                inputs.Add(ctx.Pin(param.Name, pinType, null, pinSummary));
                inputParamIndices.Add(i);
            }
        }

        // Build output pins: return value + out/inout parameters
        var outputs = new List<IVLPinDescription>();
        var hasReturnValue = func.ClrReturnType != typeof(compute.GpuVoid);

        // Main output (return value)
        if (hasReturnValue)
        {
            var outputType = GetShaderNodeType(func.ClrReturnType);
            outputs.Add(ctx.Pin("Output", outputType, null, "Function result"));
        }

        // Out and InOut parameters as additional outputs
        // Track which parameters are outputs for later mapping
        var outputParamIndices = new List<int>();
        for (int i = 0; i < func.Parameters.Count; i++)
        {
            var param = func.Parameters[i];
            if (param.Modifier == InputModifier.Out || param.Modifier == InputModifier.InOut)
            {
                var pinType = GetPinType(param);
                var pinSummary = func.Metadata.ParamDescriptions.GetValueOrDefault(param.Name, "");
                outputs.Add(ctx.Pin(param.Name, pinType, null, pinSummary));
                outputParamIndices.Add(i);
            }
        }

        return ctx.Node(
            inputs: inputs,
            outputs: outputs,
            summary: summary,
            newNode: instanceCtx => CreateNodeInstance(
                instanceCtx, func, inputParamIndices, outputParamIndices, hasReturnValue));
    }

    private static IVLNode CreateNodeInstance(
        NodeBuilding.NodeInstanceBuildContext instanceCtx,
        MixinFunctionInfo func,
        List<int> inputParamIndices,
        List<int> outputParamIndices,
        bool hasReturnValue)
    {
        var inputCount = inputParamIndices.Count;
        // Use object[] to store both scalar (AbstractShaderNode) and array (IEnumerable<AbstractShaderNode>) inputs
        var inputValues = new object[inputCount];
        AbstractShaderNode resultNode = null;
        var nodeContext = instanceCtx.NodeContext;

        // Pre-create default constants for all parameters (once, not per-frame)
        // This handles both Out parameters and unconnected In/InOut parameters
        // Uses the parameter's default value if available, otherwise 0
        var paramDefaults = new Dictionary<int, AbstractShaderNode>();
        for (int i = 0; i < func.Parameters.Count; i++)
        {
            var param = func.Parameters[i];
            paramDefaults[i] = CreateDefaultForParameter(nodeContext, param);
        }

        // Create input pins - use different types for scalar vs array parameters
        var inputPins = new List<IVLPin>();
        for (int i = 0; i < inputCount; i++)
        {
            var index = i;
            var paramIndex = inputParamIndices[i];
            var param = func.Parameters[paramIndex];

            // All parameters (including arrays) accept AbstractShaderNode
            // Array<T> is a subtype of AbstractShaderNode
            inputPins.Add(instanceCtx.Input<AbstractShaderNode>(
                value =>
                {
                    if (!ReferenceEquals(inputValues[index], value))
                    {
                        inputValues[index] = value;
                        resultNode = null;
                    }
                },
                null));
        }

        // Build the arguments list matching function parameter order
        // For In/InOut: use connected input, or default if null
        // For Out: use pre-created default constant
        // Arrays are passed directly as Array<T> nodes
        List<AbstractShaderNode> BuildArguments()
        {
            var arguments = new List<AbstractShaderNode>();
            var inputIndex = 0;

            for (int i = 0; i < func.Parameters.Count; i++)
            {
                var param = func.Parameters[i];
                if (param.Modifier == InputModifier.Out)
                {
                    // Out parameter: always use default constant
                    arguments.Add(paramDefaults[i]);
                }
                else
                {
                    // In, InOut, or Array: use connected input, or default if null
                    var inputNode = inputIndex < inputValues.Length
                        ? inputValues[inputIndex] as AbstractShaderNode
                        : null;
                    inputIndex++;
                    arguments.Add(inputNode ?? paramDefaults[i]);
                }
            }
            return arguments;
        }

        // Compute the result lazily
        AbstractShaderNode GetResult()
        {
            if (resultNode == null)
            {
                var arguments = BuildArguments();
                // Arguments always have valid values (connected inputs or defaults)
                resultNode = MixinNodeFactory.CreateMixinFunctionDynamic(
                    nodeContext, func, arguments);
            }
            return resultNode;
        }

        // Create output pins
        var outputPins = new List<IVLPin>();

        // First output is the return value (if not void)
        if (hasReturnValue)
        {
            outputPins.Add(instanceCtx.Output<AbstractShaderNode>(() => GetResult()));
        }

        // Additional outputs come from OptionalOutputs list
        // Access via reflection since OptionalOutputs is on ShaderNode<T>, not AbstractShaderNode
        for (int i = 0; i < outputParamIndices.Count; i++)
        {
            var outputIndex = i;
            outputPins.Add(instanceCtx.Output<AbstractShaderNode>(() =>
            {
                var result = GetResult();
                if (result == null) return null;

                // Get OptionalOutputs via reflection
                var prop = result.GetType().GetProperty("OptionalOutputs");
                if (prop?.GetValue(result) is List<AbstractShaderNode> optionalOutputs &&
                    outputIndex < optionalOutputs.Count)
                {
                    return optionalOutputs[outputIndex];
                }
                return null;
            }));
        }

        return instanceCtx.Node(
            inputs: inputPins,
            outputs: outputPins,
            update: default,
            dispose: () =>
            {
                resultNode = null;
                Array.Clear(inputValues, 0, inputValues.Length);
            });
    }

    private static Type GetShaderNodeType(Type clrType)
    {
        if (clrType == null) return typeof(ShaderNode<float>);

        // Map CLR types to ShaderNode<T> types
        if (clrType == typeof(float)) return typeof(ShaderNode<float>);
        if (clrType == typeof(Vector2)) return typeof(ShaderNode<Vector2>);
        if (clrType == typeof(Vector3)) return typeof(ShaderNode<Vector3>);
        if (clrType == typeof(Vector4)) return typeof(ShaderNode<Vector4>);
        if (clrType == typeof(int)) return typeof(ShaderNode<int>);
        if (clrType == typeof(Int2)) return typeof(ShaderNode<Int2>);
        if (clrType == typeof(Int3)) return typeof(ShaderNode<Int3>);
        if (clrType == typeof(Int4)) return typeof(ShaderNode<Int4>);
        if (clrType == typeof(uint)) return typeof(ShaderNode<uint>);
        if (clrType == typeof(bool)) return typeof(ShaderNode<bool>);
        if (clrType == typeof(Matrix)) return typeof(ShaderNode<Matrix>);
        if (clrType == typeof(Texture)) return typeof(ShaderNode<Texture>);
        if (clrType == typeof(SamplerState)) return typeof(AbstractShaderNode);
        if (clrType == typeof(Buffer)) return typeof(ShaderNode<Buffer>);

        return typeof(ShaderNode<float>);
    }

    /// <summary>
    /// Builds the pin summary/description, including default value if present.
    /// </summary>
    private static string BuildPinSummary(MixinParameterInfo param, MixinMetadata metadata)
    {
        var description = metadata.ParamDescriptions.GetValueOrDefault(param.Name, "");

        // Add default value info if available
        if (param.HasInlineDefault && !string.IsNullOrEmpty(param.InlineDefaultString))
        {
            var defaultInfo = $"Default: {param.InlineDefaultString}";
            if (string.IsNullOrEmpty(description))
                return defaultInfo;
            return $"{description} ({defaultInfo})";
        }
        else if (param.DefaultValue != null)
        {
            var defaultInfo = $"Default: {FormatDefaultValue(param.DefaultValue)}";
            if (string.IsNullOrEmpty(description))
                return defaultInfo;
            return $"{description} ({defaultInfo})";
        }

        return description;
    }

    /// <summary>
    /// Formats a default value for display.
    /// </summary>
    private static string FormatDefaultValue(object value)
    {
        return value switch
        {
            float f => f.ToString("G", System.Globalization.CultureInfo.InvariantCulture),
            Vector2 v2 => $"({v2.X}, {v2.Y})",
            Vector3 v3 => $"({v3.X}, {v3.Y}, {v3.Z})",
            Vector4 v4 => $"({v4.X}, {v4.Y}, {v4.Z}, {v4.W})",
            int i => i.ToString(),
            bool b => b.ToString().ToLower(),
            _ => value.ToString() ?? ""
        };
    }

    /// <summary>
    /// Gets the VL pin type for a parameter, handling special types like Buffer and arrays.
    /// </summary>
    private static Type GetPinType(MixinParameterInfo param)
    {
        // Array types use ShaderNode<GpuArray<T>>
        if (param.IsArray)
        {
            var gpuArrayType = typeof(GpuArray<>).MakeGenericType(param.ClrType);
            return typeof(ShaderNode<>).MakeGenericType(gpuArrayType);
        }

        // Buffer types should use BufferInput<T> where T is the element type
        if (param.ClrType == typeof(Buffer) && param.BufferElementType != null)
        {
            return typeof(BufferInput<>).MakeGenericType(param.BufferElementType);
        }

        // SamplerState uses SamplerInput
        if (param.ClrType == typeof(SamplerState))
        {
            return typeof(SamplerInput);
        }

        // For other types, use the standard ShaderNode<T> mapping
        return GetShaderNodeType(param.ClrType);
    }

    /// <summary>
    /// Creates a default shader node for a parameter based on its type.
    /// Handles special types like SamplerState and Buffer that need specific input nodes.
    /// </summary>
    private static AbstractShaderNode CreateDefaultForParameter(NodeContext nodeContext, MixinParameterInfo param)
    {
        // SamplerState needs a SamplerInput node
        if (param.ClrType == typeof(SamplerState))
        {
            return new SamplerInput(nodeContext);
        }

        // Buffer types need a BufferInput<T> node
        if (param.ClrType == typeof(Buffer) && param.BufferElementType != null)
        {
            return CreateBufferInput(nodeContext, param.BufferElementType, param.IsReadWrite);
        }

        // For other types, use the constant helper with default value
        return ConstantHelper.AbstractFromObject(param.ClrType, param.DefaultValue);
    }

    /// <summary>
    /// Creates a BufferInput&lt;T&gt; for the given element type using reflection.
    /// </summary>
    private static AbstractShaderNode CreateBufferInput(NodeContext nodeContext, Type elementType, bool isReadWrite)
    {
        // Create a default constant for the element type (used as the type descriptor)
        var typeNode = ConstantHelper.AbstractFromFloat(elementType, 0f);

        // Create BufferTypeTracker<T> with appropriate buffer type
        var bufferType = isReadWrite ? BufferType.RW : BufferType.Normal;
        var trackerType = typeof(BufferTypeTracker<>).MakeGenericType(elementType);
        var tracker = Activator.CreateInstance(trackerType, typeNode, bufferType);

        // Create BufferInput<T>
        var bufferInputType = typeof(BufferInput<>).MakeGenericType(elementType);
        var bufferInput = Activator.CreateInstance(bufferInputType, nodeContext, tracker, typeNode);

        return (AbstractShaderNode)bufferInput;
    }
}
