using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Fuse.ShaderFX;
using Stride.Core;
using Stride.Core.IO;
using Stride.Engine;
using Stride.Core.Mathematics;
using Stride.Rendering;
using Stride.Rendering.Materials;
using Stride.Shaders.Compiler;
using Stride.Shaders.Parser;
using VL.Core;
using VL.Lib.Basics.Resources;
using VL.Stride;
using VL.Stride.Rendering;
using VL.Stride.Rendering.ComputeEffect;
using VL.Stride.Shaders.ShaderFX;

namespace Fuse;

public static class DictionaryExtensions
{
    public static void ForEach<TKey, TValue>(this Dictionary<TKey, TValue> dict,
        Action<KeyValuePair<TKey, TValue>> action)
    {
        foreach (var item in dict)
            action(item);
    }
}

public static class EnumerableExtensionForEach
{
    public static void ForEach<T>(this IEnumerable<T> list, Action<T> block)
    {
        foreach (var item in list) block(item);
    }
}

public static class ProfilePathShort
{
    public static string Short(string path)
    {
        var lead = path.StartsWith("/");
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        string Seg(string s)
        {
            var words = Regex.Split(s, @"(?<!^)(?=[A-Z])");
            if (words.Length == 0) return s;
            var sb = new StringBuilder();
            sb.Append(words[0].Substring(0, System.Math.Min(3, words[0].Length)));
            for (int i = 1; i < words.Length; i++)
                sb.Append(words[i].Substring(0, System.Math.Min(2, words[i].Length)));
            return sb.ToString().Substring(0, System.Math.Min(10, sb.Length));
        }

        var result = string.Join("/", parts.Select(Seg));
        return lead ? "/" + result : result;
    }
}

public static class ShaderNodesUtil
{
    public static bool DebugShaderGeneration = false;
    public static bool DebugVisit = false;
    public static bool TimeShaderGeneration = false;
    public static bool TraceShaderSource { get; set; } =
        string.Equals(Environment.GetEnvironmentVariable("FUSE_TRACE_SHADER_SOURCE"), "1", StringComparison.Ordinal);
    public static string ShaderDumpDirectory { get; set; } =
        Environment.GetEnvironmentVariable("FUSE_SHADER_DUMP_DIR");
    public static bool ValidateGeneratedShaderSource { get; set; } = false;
    public static bool ThrowOnInvalidGeneratedShader { get; set; } = false;

    public static string DebugIdent = ". ";

    // Pre-compiled regex patterns for performance (avoid recompilation on every call)
    private static readonly Regex PlaceholderRegex = new(
        @"\$\{(?<key>[^}]+)\}",
        RegexOptions.Compiled
    );

    private static readonly Regex IdPlaceholderRegex = new(
        @"\$\{#(?<key>[^}]+)\}",
        RegexOptions.Compiled
    );

    private static readonly Regex CleanVlClassNameRegex = new(
        "_.*",
        RegexOptions.Compiled
    );

    private static readonly PropertyKey<int> VarIDCounterKey =
        new("Fuse.FuseIDCounter", typeof(int), DefaultValueMetadata.Static(0, true));
    private static readonly ConditionalWeakTable<object, ShaderSourceRegistrationCache> ShaderSourceRegistrations = new();
    private static readonly string StandaloneShaderSourceRootPath =
        $"/fuse-standalone-shader-sources-{Guid.NewGuid():N}";
    private static readonly object StandaloneShaderSourceManagerLock = new();
    private static object StandaloneShaderSourceManager;

    public static int Id2;

    [ThreadStatic] private static NodeContext _sCurrentParentContext;

    public static void SetConsoleOut(string path)
    {
        var filestream = new FileStream(path, FileMode.Create);
        var streamWriter = new StreamWriter(filestream);
        streamWriter.AutoFlush = true;
        Console.SetOut(streamWriter);
        Console.SetError(streamWriter);
    }
    /*
    public static bool IsFunctionNode(AbstractShaderNode theNode)
    {
        return theNode.Outs.Select(output => output.GetType()).Any(objectType => objectType.IsGenericType && objectType.GetGenericTypeDefinition() == typeof(Invoke<>));
    }*/

    public static string BuildArguments(IEnumerable<AbstractShaderNode> inputs)
    {
        var stringBuilder = new StringBuilder();
        inputs.ForEach(input =>
        {
            if (input == null) return;
            // Use GetReference() for inlining support
            stringBuilder.Append(input.GetReference());
            stringBuilder.Append(", ");
        });
        if (stringBuilder.Length > 2) stringBuilder.Remove(stringBuilder.Length - 2, 2);
        return stringBuilder.ToString();
    }

    public static bool HasNullValue<T>(IEnumerable<T> theSequence)
    {
        return theSequence.Any(value => value == null);
    }

    public static string IndentCode(string theCode)
    {
        var lines = theCode.Split(
            new[] { Environment.NewLine },
            StringSplitOptions.None
        );

        var myStringBuilder = new StringBuilder();
        lines.ForEach(line => myStringBuilder.AppendLine("    " + line));
        return myStringBuilder.ToString();
    }

    public static string FirstLetterToUpper(string str)
    {
        if (str == null)
            return null;

        if (str.Length > 1)
            return char.ToUpper(str[0]) + str[1..];

        return str.ToUpper();
    }

    public static string FirstLetterToLower(string str)
    {
        if (str == null)
            return null;

        if (str.Length > 1)
            return char.ToLower(str[0]) + str[1..];

        return str.ToLower();
    }

    public static string Evaluate(string theShaderTemplate, IDictionary<string, string> theKeys)
    {
        return PlaceholderRegex.Replace(
            theShaderTemplate,
            m => theKeys.TryGetValue(m.Groups["key"].Value, out var value) ? value : m.Value
        );
    }

    public static string Evaluate(string theShaderTemplate, MatchEvaluator theEvaluator)
    {
        return PlaceholderRegex.Replace(theShaderTemplate, theEvaluator);
    }

    public static string EvaluateIDs(string theShaderTemplate)
    {
        var id = 0;
        var idMap = new Dictionary<string, string>();
        return IdPlaceholderRegex.Replace(
            theShaderTemplate,
            m =>
            {
                var key = m.Groups["key"].Value;
                if (!idMap.TryGetValue(key, out var existingId))
                {
                    existingId = id.ToString();
                    idMap[key] = existingId;
                    id++;
                }

                return existingId;
            });
    }

    public static string CleanVlClassName(string theVlClassName)
    {
        return CleanVlClassNameRegex.Replace(theVlClassName, "");
    }

    public static string FixName(string theName)
    {
        return theName.Replace(".", "").Replace(" ", "");
    }

    public static string FormatShaderCode(string shaderCode)
    {
        var formattedCode = new StringBuilder();
        var indentLevel = 0;
        const string
            indentString = "    "; // You can adjust this to your preferred indentation (e.g., two spaces or a tab)
        var previousLineWasBlank = false;

        using (var reader = new StringReader(shaderCode))
        {
            while (reader.ReadLine() is { } line)
            {
                line = line.Trim();

                if (line.Length == 0) // Current line is blank
                {
                    if (!previousLineWasBlank) // Add a blank line only if the previous line wasn't blank
                    {
                        formattedCode.AppendLine();
                        previousLineWasBlank = true;
                    }

                    continue;
                }

                previousLineWasBlank = false; // Reset the flag since the current line is not blank

                // Decrease indent level if line contains a closing brace
                if (line.Contains('}'))
                {
                    indentLevel--;
                    indentLevel = System.Math.Max(indentLevel, 0); // Ensure indentLevel is never negative
                }

                // Append indented line to the formatted code
                formattedCode.AppendLine($"{new string(indentString[0], indentLevel * indentString.Length)}{line}");

                // Increase indent level if line contains an opening brace
                if (line.Contains('{'))
                    indentLevel++;
            }
        }

        return formattedCode.ToString();
    }

    public static void DumpShaderSource(string shaderName, string shaderCode, string phase = "generated")
    {
        if (!TraceShaderSource)
            return;

        try
        {
            var baseDirectory = ResolveShaderDumpDirectory();

            var safeShaderName = string.Concat(shaderName.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            var filePath = Path.Combine(baseDirectory, $"{timestamp}_{safeShaderName}_{phase}.sdsl");
            File.WriteAllText(filePath, shaderCode ?? string.Empty);
            Console.WriteLine($"[FUSE:SHADERTRACE] {shaderName} ({phase}) -> {filePath}");
        }
        catch (Exception ex)
        {
            Logging.FuseLogger.Warning($"Failed to dump shader source for {shaderName}: {ex.Message}");
        }
    }

    public static void DumpShaderException(string shaderName, string phase, Exception ex, string sourcePath = null)
    {
        try
        {
            var baseDirectory = ResolveShaderDumpDirectory();
            var safeShaderName = string.Concat((shaderName ?? "UnknownShader")
                .Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            var filePath = Path.Combine(baseDirectory, $"{timestamp}_{safeShaderName}_{phase}_exception.log");

            var message = new StringBuilder();
            message.AppendLine($"Timestamp: {DateTime.Now:O}");
            message.AppendLine($"Shader: {shaderName}");
            message.AppendLine($"Phase: {phase}");
            if (!string.IsNullOrWhiteSpace(sourcePath))
                message.AppendLine($"SourcePath: {sourcePath}");
            message.AppendLine($"Exception: {ex.GetType().FullName}");
            message.AppendLine($"Message: {ex.Message}");
            message.AppendLine("StackTrace:");
            message.AppendLine(ex.StackTrace ?? "<none>");

            File.WriteAllText(filePath, message.ToString());
            Console.WriteLine($"[FUSE:SHADERTRACE] exception log -> {filePath}");
        }
        catch (Exception logException)
        {
            Logging.FuseLogger.Warning(
                $"Failed to write shader exception log for {shaderName} ({phase}): {logException.Message}");
        }
    }

    public static void DumpShaderCompileAttempt(string shaderName, string shaderCode, string phase, string sourcePath = null)
    {
        if (!TraceShaderSource)
            return;

        try
        {
            var baseDirectory = ResolveShaderDumpDirectory();
            var safeShaderName = string.Concat((shaderName ?? "UnknownShader")
                .Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            var filePath = Path.Combine(baseDirectory, $"{timestamp}_{safeShaderName}_{phase}_compile-attempt.log");

            var lines = (shaderCode ?? string.Empty).Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            var firstNonEmpty = lines.FirstOrDefault(l => !string.IsNullOrWhiteSpace(l))?.Trim() ?? "<none>";
            var openBraces = (shaderCode ?? string.Empty).Count(c => c == '{');
            var closeBraces = (shaderCode ?? string.Empty).Count(c => c == '}');
            var unresolvedPlaceholders = PlaceholderRegex.Matches(shaderCode ?? string.Empty).Count;

            var mixins = new List<string>();
            var headerMatch = Regex.Match(firstNonEmpty, @"^shader\s+\w+\s*:\s*(?<mixins>.+?)(\s*\{)?$");
            if (headerMatch.Success)
            {
                mixins.AddRange(headerMatch.Groups["mixins"].Value
                    .Split(',')
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrWhiteSpace(s)));
            }

            var message = new StringBuilder();
            message.AppendLine($"Timestamp: {DateTime.Now:O}");
            message.AppendLine($"Shader: {shaderName}");
            message.AppendLine($"Phase: {phase}");
            if (!string.IsNullOrWhiteSpace(sourcePath))
                message.AppendLine($"SourcePath: {sourcePath}");
            message.AppendLine($"Header: {firstNonEmpty}");
            message.AppendLine($"CodeLength: {(shaderCode ?? string.Empty).Length}");
            message.AppendLine($"OpenBraces: {openBraces}");
            message.AppendLine($"CloseBraces: {closeBraces}");
            message.AppendLine($"UnresolvedPlaceholders: {unresolvedPlaceholders}");
            message.AppendLine("Mixins:");
            if (mixins.Count == 0)
                message.AppendLine("  <none>");
            else
                mixins.ForEach(m => message.AppendLine($"  - {m}"));

            File.WriteAllText(filePath, message.ToString());
            Console.WriteLine($"[FUSE:SHADERTRACE] compile attempt -> {filePath}");
        }
        catch (Exception ex)
        {
            Logging.FuseLogger.Warning($"Failed to write compile attempt log for {shaderName}: {ex.Message}");
        }
    }

    public static void DumpShaderDiagnostics(ShaderDiagnosticContext diagnosticContext, bool force = false)
    {
        if (!force && !TraceShaderSource)
            return;

        if (diagnosticContext == null)
            return;

        try
        {
            var baseDirectory = ResolveShaderDumpDirectory();
            var safeShaderName = string.Concat((diagnosticContext.ShaderName ?? "UnknownShader")
                .Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            var phase = string.IsNullOrWhiteSpace(diagnosticContext.Phase)
                ? "unknown"
                : diagnosticContext.Phase;
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            var filePath = Path.Combine(baseDirectory, $"{timestamp}_{safeShaderName}_{phase}_diagnostics.log");

            File.WriteAllText(filePath, diagnosticContext.ToDiagnosticLog());
            Console.WriteLine($"[FUSE:SHADERTRACE] diagnostics -> {filePath}");
        }
        catch (Exception ex)
        {
            Logging.FuseLogger.Warning(
                $"Failed to write shader diagnostics for {diagnosticContext.ShaderName}: {ex.Message}");
        }
    }

    private static string ResolveShaderDumpDirectory()
    {
        var baseDirectory = ShaderDumpDirectory;
        if (string.IsNullOrWhiteSpace(baseDirectory))
            baseDirectory = Path.Combine(Path.GetTempPath(), "FuseShaderDump");
        Directory.CreateDirectory(baseDirectory);
        return baseDirectory;
    }

    public static bool ValidateGeneratedShaderCode(string shaderCode, out string reason)
    {
        reason = "";
        if (string.IsNullOrWhiteSpace(shaderCode))
        {
            reason = "Shader code is empty.";
            return false;
        }

        if (PlaceholderRegex.IsMatch(shaderCode))
        {
            reason = "Shader code still contains unresolved ${...} placeholders.";
            return false;
        }

        var lines = shaderCode.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        var firstNonEmptyIndex = Array.FindIndex(lines, l => !string.IsNullOrWhiteSpace(l));
        if (firstNonEmptyIndex < 0)
        {
            reason = "Shader code has no non-empty lines.";
            return false;
        }

        var header = lines[firstNonEmptyIndex].Trim();
        if (!Regex.IsMatch(header, @"^shader\s+\w+\s*:\s*.+"))
        {
            reason = $"Invalid shader header: '{header}'.";
            return false;
        }

        if (header.Contains(",,", StringComparison.Ordinal) || header.Contains(": ,", StringComparison.Ordinal))
        {
            reason = $"Suspicious shader header (empty mixin entry): '{header}'.";
            return false;
        }

        if (!header.TrimEnd().EndsWith("{", StringComparison.Ordinal))
        {
            var secondNonEmptyIndex = -1;
            for (var i = firstNonEmptyIndex + 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                secondNonEmptyIndex = i;
                break;
            }

            if (secondNonEmptyIndex < 0 || lines[secondNonEmptyIndex].Trim() != "{")
            {
                reason = "Missing opening '{' after shader header.";
                return false;
            }
        }

        var openBraces = shaderCode.Count(c => c == '{');
        var closeBraces = shaderCode.Count(c => c == '}');
        if (openBraces != closeBraces)
        {
            reason = $"Unbalanced braces: open={openBraces}, close={closeBraces}.";
            return false;
        }

        // Detect unresolved placeholder struct usage such as:
        // struct GpuStruct{}; GpuStruct x; x.SomeMember
        var placeholderStructDecl = Regex.IsMatch(shaderCode, @"\bstruct\s+GpuStruct\s*\{\s*\}\s*;");
        if (placeholderStructDecl)
        {
            var varMatches = Regex.Matches(shaderCode, @"\bGpuStruct\s+(?<var>\w+)\s*;");
            foreach (Match varMatch in varMatches)
            {
                var varName = varMatch.Groups["var"].Value;
                if (string.IsNullOrWhiteSpace(varName))
                    continue;

                if (Regex.IsMatch(shaderCode, $@"\b{Regex.Escape(varName)}\s*\.\s*\w+"))
                {
                    reason = $"Member access on unresolved placeholder struct 'GpuStruct' via variable '{varName}'.";
                    return false;
                }
            }
        }

        return true;
    }


    public static void AddShaderSource(string type, string sourceCode, string sourcePath)
    {
        try
        {
            if (!TryGetCurrentShaderSourceManager(out var sourceManager)
                && !TryGetStandaloneShaderSourceManager(out sourceManager))
                return;

            TryRegisterShaderSource(sourceManager, type, sourceCode, sourcePath);
        }
        catch (Exception ex)
        {
            DumpShaderException(type, "addshadersource", ex, sourcePath);
            Logging.FuseLogger.Warning($"AddShaderSource failed for {type} ({sourcePath}): {ex.Message}");
        }
    }

    private static bool TryGetCurrentShaderSourceManager(out object sourceManager)
    {
        sourceManager = null;

        if (!TryGetCurrentGame(out var game))
            return false;

        var compiler = GetEffectCompiler(game.EffectSystem?.Compiler);
        return TryGetShaderSourceManager(compiler, out sourceManager);
    }

    private static bool TryGetCurrentGame(out Game game)
    {
        game = null;

        IResourceProvider<Game> gameProvider;
        try
        {
            gameProvider = AppHost.Current.Services.GetService(typeof(IResourceProvider<Game>)) as IResourceProvider<Game>;
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        var gameHandle = gameProvider?.GetHandle();
        game = gameHandle?.Resource;
        return game != null;
    }

    private static bool TryGetStandaloneShaderSourceManager(out object sourceManager)
    {
        lock (StandaloneShaderSourceManagerLock)
        {
            if (StandaloneShaderSourceManager == null)
                StandaloneShaderSourceManager = CreateStandaloneShaderSourceManager();

            sourceManager = StandaloneShaderSourceManager;
        }

        return sourceManager != null;
    }

    private static object CreateStandaloneShaderSourceManager()
    {
        var fileProvider = new FileSystemProvider(StandaloneShaderSourceRootPath, Environment.CurrentDirectory);
        var compiler = new EffectCompiler(fileProvider);
        return TryGetShaderSourceManager(compiler, out var sourceManager)
            ? sourceManager
            : null;
    }

    private static EffectCompiler GetEffectCompiler(IEffectCompiler compiler)
    {
        if (compiler is EffectCompiler effectCompiler)
            return effectCompiler;

        if (compiler is EffectCompilerCache effectCompilerCache)
            return typeof(EffectCompilerChain)
                .GetProperty("Compiler", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(effectCompilerCache) as EffectCompiler;

        return null;
    }

    private static bool TryGetShaderSourceManager(EffectCompiler compiler, out object sourceManager)
    {
        sourceManager = null;
        if (compiler == null)
            return false;

        var getParserMethod =
            typeof(EffectCompiler).GetMethod("GetMixinParser", BindingFlags.Instance | BindingFlags.NonPublic);
        if (getParserMethod == null)
            return false;

        if (!(getParserMethod.Invoke(compiler, null) is ShaderMixinParser parser))
            return false;

        sourceManager = parser.SourceManager;
        return sourceManager != null;
    }

    private static bool TryRegisterShaderSource(
        object sourceManager,
        string type,
        string sourceCode,
        string sourcePath)
    {
        if (sourceManager == null)
            return false;

        if (IsShaderSourceRegistered(sourceManager, type, sourceCode, sourcePath))
        {
            if (TimeShaderGeneration)
                Console.WriteLine($"-> AddShaderSource skipped: {type}");
            return false;
        }

        var addShaderSource = sourceManager.GetType().GetMethod(
            "AddShaderSource",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            [typeof(string), typeof(string), typeof(string)],
            null);
        if (addShaderSource == null)
            return false;

        addShaderSource.Invoke(sourceManager, [type, sourceCode, sourcePath]);
        MarkShaderSourceRegistered(sourceManager, type, sourceCode, sourcePath);
        return true;
    }

    private static bool IsShaderSourceRegistered(
        object sourceManager,
        string type,
        string sourceCode,
        string sourcePath)
    {
        if (sourceManager == null)
            return false;

        return ShaderSourceRegistrations.GetOrCreateValue(sourceManager)
            .Contains(type, sourcePath, sourceCode);
    }

    private static void MarkShaderSourceRegistered(
        object sourceManager,
        string type,
        string sourceCode,
        string sourcePath)
    {
        if (sourceManager == null)
            return;

        ShaderSourceRegistrations.GetOrCreateValue(sourceManager)
            .AddOrUpdate(type, sourcePath, sourceCode);
    }

    private sealed class ShaderSourceRegistrationCache
    {
        private readonly object _lock = new();
        private readonly Dictionary<ShaderSourceRegistrationKey, string> _sources = new();

        public bool Contains(string type, string sourcePath, string sourceCode)
        {
            var key = new ShaderSourceRegistrationKey(type ?? "", sourcePath ?? "");
            lock (_lock)
            {
                return _sources.TryGetValue(key, out var registeredSourceCode)
                    && string.Equals(registeredSourceCode, sourceCode ?? "", StringComparison.Ordinal);
            }
        }

        public void AddOrUpdate(string type, string sourcePath, string sourceCode)
        {
            var key = new ShaderSourceRegistrationKey(type ?? "", sourcePath ?? "");
            lock (_lock)
            {
                _sources[key] = sourceCode ?? "";
            }
        }
    }

    private readonly record struct ShaderSourceRegistrationKey(string Type, string SourcePath);

    // ReSharper disable once UnusedMember.Global
    // accessed from vl
    public static VLComputeEffectShader RegisterComputeShader<T>(ToComputeFx<T> theComputeFx,
        string effectName = "FuseComputeGraph")
    {
        try
        {
            var game = AppHost.Current.Services.GetGameProvider().GetHandle().Resource;
            if (game == null) return null;

            var shaderGraph = ShaderGraph.BuildFinalShaderGraph(theComputeFx);
            var result = ShaderGraph.ComposeComputeShader(game.GraphicsDevice, game.Services, shaderGraph);
            result.Name = effectName;
            return result;
        }
        catch (Exception ex)
        {
            var diagnosticContext = theComputeFx?.LastDiagnosticContext;
            var shaderName = diagnosticContext?.ShaderName ?? theComputeFx?.ShaderName ?? effectName;
            DumpShaderException(shaderName, "composecomputeshader", ex, diagnosticContext?.SourcePath);
            DumpShaderDiagnostics(diagnosticContext, force: true);
            Logging.FuseLogger.Error($"RegisterComputeShader failed for {shaderName}", ex);
            throw;
        }
    }


    public static DynamicEffectInstance RegisterDrawShader(ToDrawFX theDrawShader)
    {
        var watch = new Stopwatch();

        watch.Start();
        var game = AppHost.Current.Services.GetGameProvider().GetHandle().Resource;
        if (game == null) return null;

        var effectImageShader = new DynamicDrawEffectInstance("ShaderFXGraphEffect");
        var method =
            typeof(ShaderGraph).GetMethod("NewShaderGeneratorContext", BindingFlags.Static | BindingFlags.NonPublic);
        var context = method?.Invoke(null,
            new object[] { game.GraphicsDevice, effectImageShader.Parameters, effectImageShader.Subscriptions });

        //var context = ShaderGraph.NewShaderGeneratorContext(game.GraphicsDevice, effectImageShader.Parameters, effectImageShader.Subscriptions);
        var key = new MaterialComputeColorKeys(MaterialKeys.DiffuseMap, MaterialKeys.DiffuseValue,
            Stride.Core.Mathematics.Color.White);
        theDrawShader.GenerateShaderSource((ShaderGeneratorContext)context, key);
        effectImageShader.EffectName = theDrawShader.ShaderName;
        if (DebugShaderGeneration)
            Console.WriteLine($"Register Time: {watch.ElapsedMilliseconds} ms for Shader {theDrawShader.ShaderName}");
        return effectImageShader;
    }

    // ReSharper disable once UnusedMember.Global
    // accessed from vl
    public static ToShaderFX<T> RegisterShaderFX<T>(ShaderNode<T> theGpuValue, bool isCompute = true) where T : struct
    {
        return new ToShaderFX<T>(theGpuValue);
    }

    public static void ListInputs(List<IGpuInput> theInputs, AbstractShaderNode theGpuValue)
    {
        if (theGpuValue == null) return;

        theInputs.AddRange(theGpuValue.InputList());
    }

    public static Dictionary<string, List<TProperty>> PropertiesForTree<TProperty>(AbstractShaderNode theNode)
    {
        return theNode.PropertiesForTree<TProperty>();
    }

    public static List<TProperty> PropertiesForTreeList<TProperty>(AbstractShaderNode theNode, string theThePropertyId)
    {
        return theNode.PropertyForTree<TProperty>(theThePropertyId);
    }

    public static List<TProperty> PropertiesForTreeList<TProperty>(AbstractShaderNode theNode)
    {
        return theNode.PropertiesForTreeList<TProperty>();
    }


    internal static int GetAndIncIDCount(this ShaderGeneratorContext context)
    {
        var result = context.Tags.Get(VarIDCounterKey);
        context.Tags.Set(VarIDCounterKey, result + 1);
        return result;
    }

    public static int GetAndIncIDCount3()
    {
        return Id2++;
    }

    /**
     * To be checked if hashes are persistent and individual
     */
    public static int GetStableHashCode(this string str)
    {
        unchecked
        {
            var hash1 = 5381;
            var hash2 = hash1;

            for (var i = 0; i < str.Length && str[i] != '\0'; i += 2)
            {
                hash1 = ((hash1 << 5) + hash1) ^ str[i];
                if (i == str.Length - 1 || str[i + 1] == '\0')
                    break;
                hash2 = ((hash2 << 5) + hash2) ^ str[i + 1];
            }

            return hash1 + hash2 * 1566083941;
        }
    }

    public static Matrix GetParentTransform(RenderDrawContext renderDrawContext)
    {
        return renderDrawContext.RenderContext.Tags.Get(EntityRendererRenderFeature.CurrentParentTransformation);
    }

    // Use this with a Using region. For example
    // using (SetParentContext(myContext))
    // {
    //    InvokeUpstream(..)
    // }
    public static NodeContextOverride SetParentContext(NodeContext nodeContext)
    {
        return new NodeContextOverride(nodeContext);
    }

    public static uint GetHashCode(NodeContext nodeContext)
    {
        if (nodeContext == null)
            return (uint)GetStableHashCode("NullNodeContext");

        var s = nodeContext.Path.ToString();
        /*    if (_sCurrentParentContext != null)
               s += _sCurrentParentContext.Path.ToString();
*/
        unchecked
        {
            return (uint)GetStableHashCode(s);
        }
    }

    private class DynamicDrawEffectInstance : DynamicEffectInstance
    {
        public readonly CompositeDisposable Subscriptions = new();

        public DynamicDrawEffectInstance(string effectName, ParameterCollection parameters = null) : base(effectName,
            parameters)
        {
        }

        protected override void Destroy()
        {
            Subscriptions.Dispose();
            base.Destroy();
        }
    }

    public readonly struct NodeContextOverride : IDisposable
    {
        private readonly NodeContext _previous;

        internal NodeContextOverride(NodeContext nodeContext)
        {
            _previous = Interlocked.Exchange(ref _sCurrentParentContext, nodeContext);
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref _sCurrentParentContext, _previous);
        }
    }
}

public class NodeSubContextFactory
{
    private readonly NodeContext _context;

    private readonly int _startIndex;

    private int _subContextId;

    public NodeSubContextFactory(NodeContext nodeContext, int theStartIndex = 0)
    {
        _context = nodeContext;
        _startIndex = theStartIndex;
    }

    public void Reset()
    {
        _subContextId = _startIndex;
    }

    public NodeContext NextSubContext()
    {
        if (_context == null) return NodeContext.CurrentRoot;

        var result = _context.CreateSubContext(new UniqueId("Fuse", _subContextId.ToString()));
        _subContextId++;
        return result;
    }
}
