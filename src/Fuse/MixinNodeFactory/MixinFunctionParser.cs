using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Fuse.function;

namespace Fuse.MixinNodeFactory;

/// <summary>
/// Parses SDSL shader files to extract mixin names and function signatures.
/// </summary>
public class MixinFunctionParser
{
    private readonly MixinCommentParser _commentParser = new();

    // Pattern to match shader declaration: shader Name : BaseShader, BaseShader2 { ... }
    private static readonly Regex ShaderPattern = new(
        @"shader\s+(\w+)\s*(?::\s*([\w\s,]+))?\s*\{",
        RegexOptions.Compiled);

    // Pattern to match function declaration with optional modifiers
    // Captures: return type, function name, parameters
    private static readonly Regex FunctionPattern = new(
        @"(?:^|\n)\s*(?:stage\s+)?(?:override\s+)?(\w+(?:<[^>]+>)?)\s+(\w+)\s*\(\s*([^)]*)\s*\)",
        RegexOptions.Compiled);

    // Pattern to match a single parameter: [modifier] type name[arraysize] [= default]
    private static readonly Regex ParameterPattern = new(
        @"(?:(?<modifier>in|out|inout)\s+)?(?<type>\w+(?:<[^>]+>)?)\s+(?<name>\w+)(?:\[(?<arraysize>\d+)\])?(?:\s*=\s*(?<default>[^,)]+))?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Parses an SDSL file and extracts all exported functions.
    /// </summary>
    /// <param name="filePath">Path to the .sdsl file.</param>
    /// <returns>List of exported functions.</returns>
    public List<MixinFunctionInfo> ParseFile(string filePath)
    {
        if (!File.Exists(filePath))
            return new List<MixinFunctionInfo>();

        var content = File.ReadAllText(filePath);
        return ParseContent(content, filePath);
    }

    /// <summary>
    /// Parses SDSL content and extracts all exported functions.
    /// </summary>
    /// <param name="content">The SDSL shader content.</param>
    /// <param name="filePath">The source file path (for metadata).</param>
    /// <returns>List of exported functions.</returns>
    public List<MixinFunctionInfo> ParseContent(string content, string filePath = "")
    {
        var functions = new List<MixinFunctionInfo>();

        // Extract shader/mixin name
        var shaderMatch = ShaderPattern.Match(content);
        if (!shaderMatch.Success)
            return functions;

        var mixinName = shaderMatch.Groups[1].Value;

        // Find all function declarations
        var functionMatches = FunctionPattern.Matches(content);

        foreach (Match functionMatch in functionMatches)
        {
            var functionStartIndex = functionMatch.Index;

            // Extract comment block preceding the function
            var commentBlock = _commentParser.ExtractPrecedingComment(content, functionStartIndex);
            var metadata = _commentParser.ParseComments(commentBlock);

            // Only process exported functions
            if (!metadata.IsExported)
                continue;

            var returnType = functionMatch.Groups[1].Value.Trim();
            var functionName = functionMatch.Groups[2].Value.Trim();
            var parametersString = functionMatch.Groups[3].Value.Trim();

            // Skip certain built-in functions
            if (IsBuiltInFunction(functionName))
                continue;

            var functionInfo = new MixinFunctionInfo
            {
                Name = functionName,
                ReturnType = returnType,
                ClrReturnType = SdslTypeMapper.GetClrType(returnType) ?? typeof(float),
                MixinName = mixinName,
                FilePath = filePath,
                Metadata = metadata,
                Parameters = ParseParameters(parametersString, metadata)
            };

            functions.Add(functionInfo);
        }

        return functions;
    }

    /// <summary>
    /// Parses the parameter string into individual parameter info objects.
    /// </summary>
    private List<MixinParameterInfo> ParseParameters(string parametersString, MixinMetadata metadata)
    {
        var parameters = new List<MixinParameterInfo>();

        if (string.IsNullOrWhiteSpace(parametersString))
            return parameters;

        // Split by comma, but be careful with generic types like Buffer<float>
        var paramStrings = SplitParameters(parametersString);

        foreach (var paramString in paramStrings)
        {
            var match = ParameterPattern.Match(paramString.Trim());
            if (!match.Success)
                continue;

            var modifier = match.Groups["modifier"].Value.ToLowerInvariant();
            var type = match.Groups["type"].Value;
            var name = match.Groups["name"].Value;
            var defaultString = match.Groups["default"].Value;
            var arraySizeString = match.Groups["arraysize"].Value;

            // Parse array size if present
            var isArray = !string.IsNullOrEmpty(arraySizeString);
            var arraySize = isArray && int.TryParse(arraySizeString, out var size) ? size : 0;

            var inputModifier = modifier switch
            {
                "out" => InputModifier.Out,
                "inout" => InputModifier.InOut,
                _ => InputModifier.In
            };

            var clrType = SdslTypeMapper.GetClrType(type) ?? typeof(float);

            // Get description from @param comment
            metadata.ParamDescriptions.TryGetValue(name, out var description);

            // Get default from @default comment or inline default
            object? defaultValue = null;
            var hasInlineDefault = !string.IsNullOrWhiteSpace(defaultString);

            if (metadata.ParamDefaults.TryGetValue(name, out var commentDefault))
            {
                defaultValue = SdslTypeMapper.ParseValue(type, commentDefault);
            }
            else if (hasInlineDefault)
            {
                defaultValue = SdslTypeMapper.ParseValue(type, defaultString);
            }
            else
            {
                defaultValue = SdslTypeMapper.GetDefaultValue(type);
            }

            parameters.Add(new MixinParameterInfo
            {
                Name = name,
                SdslType = type,
                ClrType = clrType,
                Modifier = inputModifier,
                Description = description ?? string.Empty,
                DefaultValue = defaultValue,
                HasInlineDefault = hasInlineDefault,
                InlineDefaultString = hasInlineDefault ? defaultString : null,
                BufferElementType = SdslTypeMapper.IsBufferType(type) ? SdslTypeMapper.GetBufferElementType(type) : null,
                IsReadWrite = SdslTypeMapper.IsReadWriteType(type),
                IsArray = isArray,
                ArraySize = arraySize
            });
        }

        return parameters;
    }

    /// <summary>
    /// Splits parameter string by comma, respecting angle brackets for generic types.
    /// </summary>
    private static List<string> SplitParameters(string parametersString)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        var depth = 0;

        foreach (var c in parametersString)
        {
            if (c == '<')
            {
                depth++;
                current.Append(c);
            }
            else if (c == '>')
            {
                depth--;
                current.Append(c);
            }
            else if (c == ',' && depth == 0)
            {
                var param = current.ToString().Trim();
                if (!string.IsNullOrEmpty(param))
                {
                    result.Add(param);
                }
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        var lastParam = current.ToString().Trim();
        if (!string.IsNullOrEmpty(lastParam))
        {
            result.Add(lastParam);
        }

        return result;
    }

    /// <summary>
    /// Checks if a function name is a built-in shader function that shouldn't be exported.
    /// </summary>
    private static bool IsBuiltInFunction(string functionName)
    {
        var builtIns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Compute",
            "VSMain",
            "PSMain",
            "CSMain",
            "GSMain",
            "HSMain",
            "DSMain"
        };

        return builtIns.Contains(functionName);
    }

    /// <summary>
    /// Gets the mixin name from an SDSL file.
    /// </summary>
    /// <param name="content">The SDSL content.</param>
    /// <returns>The mixin name, or null if not found.</returns>
    public string? GetMixinName(string content)
    {
        var match = ShaderPattern.Match(content);
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>
    /// Gets the base shaders that this mixin inherits from.
    /// </summary>
    /// <param name="content">The SDSL content.</param>
    /// <returns>List of base shader names.</returns>
    public List<string> GetBaseShaders(string content)
    {
        var result = new List<string>();
        var match = ShaderPattern.Match(content);

        if (match.Success && match.Groups[2].Success)
        {
            var baseShaders = match.Groups[2].Value.Split(',');
            foreach (var shader in baseShaders)
            {
                var trimmed = shader.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    result.Add(trimmed);
                }
            }
        }

        return result;
    }
}
