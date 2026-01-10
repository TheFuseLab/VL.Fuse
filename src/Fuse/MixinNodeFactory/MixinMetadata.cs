using System.Collections.Generic;

namespace Fuse.MixinNodeFactory;

/// <summary>
/// Metadata extracted from comments preceding an exported function.
/// </summary>
public class MixinMetadata
{
    /// <summary>
    /// Whether the function is marked with @export.
    /// </summary>
    public bool IsExported { get; set; }

    /// <summary>
    /// The namespace/category path for node browser (e.g., "Fuse.Math").
    /// From @namespace comment.
    /// </summary>
    public string Namespace { get; set; } = string.Empty;

    /// <summary>
    /// Summary description for the node tooltip.
    /// From @summary comment.
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// Per-parameter descriptions.
    /// Key: parameter name, Value: description.
    /// From @param comments.
    /// </summary>
    public Dictionary<string, string> ParamDescriptions { get; set; } = new();

    /// <summary>
    /// Per-parameter default values.
    /// Key: parameter name, Value: default value string.
    /// From @default comments.
    /// </summary>
    public Dictionary<string, string> ParamDefaults { get; set; } = new();

    /// <summary>
    /// Whether the function supports groupable mode.
    /// From @groupable comment.
    /// </summary>
    public bool IsGroupable { get; set; }

    /// <summary>
    /// Number of group options for groupable functions.
    /// From @groupoptions comment.
    /// </summary>
    public int GroupOptions { get; set; }
}
