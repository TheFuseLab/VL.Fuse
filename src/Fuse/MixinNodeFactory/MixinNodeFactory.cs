using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using Fuse.compute;
using Fuse.function;
using Stride.Core.Mathematics;
using Stride.Graphics;
using VL.Core;

namespace Fuse.MixinNodeFactory;

/// <summary>
/// Factory for creating VL nodes from SDSL mixin files with @export annotated functions.
/// </summary>
public static class MixinNodeFactory
{
    private static readonly MixinFunctionParser Parser = new();

    /// <summary>
    /// Scans a directory for SDSL files and returns all exported functions.
    /// </summary>
    /// <param name="directory">The directory to scan.</param>
    /// <param name="recursive">Whether to scan recursively.</param>
    /// <returns>List of exported functions from all SDSL files.</returns>
    public static List<MixinFunctionInfo> ScanDirectory(string directory, bool recursive = true)
    {
        var functions = new List<MixinFunctionInfo>();

        if (!Directory.Exists(directory))
            return functions;

        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = Directory.GetFiles(directory, "*.sdsl", searchOption);

        foreach (var file in files)
        {
            try
            {
                var fileFunctions = Parser.ParseFile(file);
                functions.AddRange(fileFunctions);
            }
            catch (Exception ex)
            {
                // Log error but continue processing other files
                System.Diagnostics.Debug.WriteLine($"Error parsing {file}: {ex.Message}");
            }
        }

        return functions;
    }

    /// <summary>
    /// Parses a single SDSL file and returns exported functions.
    /// </summary>
    /// <param name="filePath">Path to the SDSL file.</param>
    /// <returns>List of exported functions.</returns>
    public static List<MixinFunctionInfo> ParseFile(string filePath)
    {
        return Parser.ParseFile(filePath);
    }

    /// <summary>
    /// Creates a MixinFunction node for a given function info.
    /// </summary>
    /// <typeparam name="T">The return type of the function.</typeparam>
    /// <param name="nodeContext">The VL node context.</param>
    /// <param name="functionInfo">The function information.</param>
    /// <param name="arguments">The shader node arguments matching the function parameters.</param>
    /// <param name="defaultValue">Optional default value.</param>
    /// <returns>A MixinFunction node.</returns>
    public static MixinFunction<T> CreateMixinFunction<T>(
        NodeContext nodeContext,
        MixinFunctionInfo functionInfo,
        IEnumerable<AbstractShaderNode> arguments,
        ShaderNode<T>? defaultValue = null)
    {
        var modifiers = functionInfo.Parameters
            .Select(p => p.Modifier)
            .ToList();

        return new MixinFunction<T>(
            nodeContext,
            functionInfo.Name,
            defaultValue,
            functionInfo.MixinName,
            arguments,
            functionInfo.Metadata.IsGroupable,
            functionInfo.Metadata.GroupOptions,
            modifiers
        );
    }

    /// <summary>
    /// Creates a MixinFunction node with the appropriate return type based on function info.
    /// Returns the node as AbstractShaderNode since the type is determined at runtime.
    /// </summary>
    /// <param name="nodeContext">The VL node context.</param>
    /// <param name="functionInfo">The function information.</param>
    /// <param name="arguments">The shader node arguments matching the function parameters.</param>
    /// <returns>A MixinFunction node as AbstractShaderNode.</returns>
    public static AbstractShaderNode CreateMixinFunctionDynamic(
        NodeContext nodeContext,
        MixinFunctionInfo functionInfo,
        IEnumerable<AbstractShaderNode> arguments)
    {
        var modifiers = functionInfo.Parameters
            .Select(p => p.Modifier)
            .ToList();

        var returnType = functionInfo.ClrReturnType;

        // Create the appropriate generic type based on return type
        if (returnType == typeof(float))
        {
            return new MixinFunction<float>(
                nodeContext, functionInfo.Name, null, functionInfo.MixinName,
                arguments, functionInfo.Metadata.IsGroupable, functionInfo.Metadata.GroupOptions, modifiers);
        }
        if (returnType == typeof(Vector2))
        {
            return new MixinFunction<Vector2>(
                nodeContext, functionInfo.Name, null, functionInfo.MixinName,
                arguments, functionInfo.Metadata.IsGroupable, functionInfo.Metadata.GroupOptions, modifiers);
        }
        if (returnType == typeof(Vector3))
        {
            return new MixinFunction<Vector3>(
                nodeContext, functionInfo.Name, null, functionInfo.MixinName,
                arguments, functionInfo.Metadata.IsGroupable, functionInfo.Metadata.GroupOptions, modifiers);
        }
        if (returnType == typeof(Vector4))
        {
            return new MixinFunction<Vector4>(
                nodeContext, functionInfo.Name, null, functionInfo.MixinName,
                arguments, functionInfo.Metadata.IsGroupable, functionInfo.Metadata.GroupOptions, modifiers);
        }
        if (returnType == typeof(int))
        {
            return new MixinFunction<int>(
                nodeContext, functionInfo.Name, null, functionInfo.MixinName,
                arguments, functionInfo.Metadata.IsGroupable, functionInfo.Metadata.GroupOptions, modifiers);
        }
        if (returnType == typeof(Int2))
        {
            return new MixinFunction<Int2>(
                nodeContext, functionInfo.Name, null, functionInfo.MixinName,
                arguments, functionInfo.Metadata.IsGroupable, functionInfo.Metadata.GroupOptions, modifiers);
        }
        if (returnType == typeof(Int3))
        {
            return new MixinFunction<Int3>(
                nodeContext, functionInfo.Name, null, functionInfo.MixinName,
                arguments, functionInfo.Metadata.IsGroupable, functionInfo.Metadata.GroupOptions, modifiers);
        }
        if (returnType == typeof(Int4))
        {
            return new MixinFunction<Int4>(
                nodeContext, functionInfo.Name, null, functionInfo.MixinName,
                arguments, functionInfo.Metadata.IsGroupable, functionInfo.Metadata.GroupOptions, modifiers);
        }
        if (returnType == typeof(uint))
        {
            return new MixinFunction<uint>(
                nodeContext, functionInfo.Name, null, functionInfo.MixinName,
                arguments, functionInfo.Metadata.IsGroupable, functionInfo.Metadata.GroupOptions, modifiers);
        }
        if (returnType == typeof(bool))
        {
            return new MixinFunction<bool>(
                nodeContext, functionInfo.Name, null, functionInfo.MixinName,
                arguments, functionInfo.Metadata.IsGroupable, functionInfo.Metadata.GroupOptions, modifiers);
        }
        if (returnType == typeof(Matrix))
        {
            return new MixinFunction<Matrix>(
                nodeContext, functionInfo.Name, null, functionInfo.MixinName,
                arguments, functionInfo.Metadata.IsGroupable, functionInfo.Metadata.GroupOptions, modifiers);
        }
        if (returnType == typeof(GpuVoid))
        {
            return new MixinFunction<GpuVoid>(
                nodeContext, functionInfo.Name, null, functionInfo.MixinName,
                arguments, functionInfo.Metadata.IsGroupable, functionInfo.Metadata.GroupOptions, modifiers);
        }

        // Default to float if type not recognized
        return new MixinFunction<float>(
            nodeContext, functionInfo.Name, null, functionInfo.MixinName,
            arguments, functionInfo.Metadata.IsGroupable, functionInfo.Metadata.GroupOptions, modifiers);
    }

    /// <summary>
    /// Gets the category path for a function based on its metadata.
    /// </summary>
    /// <param name="functionInfo">The function info.</param>
    /// <returns>The category path (e.g., "Fuse.Math").</returns>
    public static string GetCategory(MixinFunctionInfo functionInfo)
    {
        if (!string.IsNullOrWhiteSpace(functionInfo.Metadata.Namespace))
            return functionInfo.Metadata.Namespace;

        // Default category based on mixin name
        return $"Fuse.Mixin.{functionInfo.MixinName}";
    }

    /// <summary>
    /// Gets all unique categories from a list of functions.
    /// </summary>
    public static IEnumerable<string> GetCategories(IEnumerable<MixinFunctionInfo> functions)
    {
        return functions
            .Select(GetCategory)
            .Distinct()
            .OrderBy(c => c);
    }

    /// <summary>
    /// Groups functions by their category.
    /// </summary>
    public static Dictionary<string, List<MixinFunctionInfo>> GroupByCategory(IEnumerable<MixinFunctionInfo> functions)
    {
        return functions
            .GroupBy(GetCategory)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    /// <summary>
    /// Creates a file watcher for SDSL files that triggers when files change.
    /// </summary>
    /// <param name="directory">The directory to watch.</param>
    /// <returns>An observable that emits file paths when they change.</returns>
    public static IObservable<string> WatchDirectory(string directory)
    {
        if (!Directory.Exists(directory))
            return Observable.Empty<string>();

        var watcher = new FileSystemWatcher(directory, "*.sdsl")
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime
        };

        var created = Observable.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(
            h => watcher.Created += h,
            h => watcher.Created -= h)
            .Select(e => e.EventArgs.FullPath);

        var changed = Observable.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(
            h => watcher.Changed += h,
            h => watcher.Changed -= h)
            .Select(e => e.EventArgs.FullPath);

        var deleted = Observable.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(
            h => watcher.Deleted += h,
            h => watcher.Deleted -= h)
            .Select(e => e.EventArgs.FullPath);

        var renamed = Observable.FromEventPattern<RenamedEventHandler, RenamedEventArgs>(
            h => watcher.Renamed += h,
            h => watcher.Renamed -= h)
            .Select(e => e.EventArgs.FullPath);

        watcher.EnableRaisingEvents = true;

        return Observable.Merge(created, changed, deleted, renamed)
            .Throttle(TimeSpan.FromMilliseconds(100)); // Debounce rapid changes
    }
}
