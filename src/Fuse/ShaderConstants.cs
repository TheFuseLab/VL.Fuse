namespace Fuse;

/// <summary>
/// Constants used throughout the shader generation system.
/// </summary>
/// <remarks>
/// Centralizes magic strings to improve maintainability and reduce typos.
/// These constants are referenced by shader nodes and code generation utilities.
/// </remarks>
public static class ShaderConstants
{
    /// <summary>
    /// Property key constants for shader node dependencies.
    /// </summary>
    public static class PropertyKeys
    {
        /// <summary>Property key for required shader mixins.</summary>
        public const string Mixins = "Mixins";

        /// <summary>Property key for GPU input parameters (uniforms, buffers, textures).</summary>
        public const string Inputs = "Inputs";

        /// <summary>Property key for shader compositions.</summary>
        public const string Compositions = "Compositions";

        /// <summary>Property key for field declarations.</summary>
        public const string Declarations = "Declarations";

        /// <summary>Property key for custom struct definitions.</summary>
        public const string Structs = "Structs";

        /// <summary>Property key for constant array definitions.</summary>
        public const string ConstantArrays = "ConstantArrays";

        /// <summary>Property key for shader stream definitions.</summary>
        public const string Streams = "Streams";

        /// <summary>Property key for viewer ID (debugging).</summary>
        public const string ViewerID = "ViewerID";
    }

    /// <summary>
    /// Template placeholder names used in shader code generation.
    /// </summary>
    public static class Placeholders
    {
        /// <summary>Placeholder for the result type (e.g., "float", "float3").</summary>
        public const string ResultType = "resultType";

        /// <summary>Placeholder for the result variable name.</summary>
        public const string ResultName = "resultName";

        /// <summary>Placeholder for the default value.</summary>
        public const string Default = "default";

        /// <summary>Placeholder for comma-separated argument list.</summary>
        public const string Arguments = "arguments";

        /// <summary>Placeholder for the implementation expression.</summary>
        public const string Implementation = "implementation";
    }

    /// <summary>
    /// Common shader mixin names.
    /// </summary>
    public static class Mixins
    {
        /// <summary>Common buffer utilities mixin.</summary>
        public const string FuseCommonBuffer = "FuseCommonBuffer";

        /// <summary>Common draw utilities mixin.</summary>
        public const string FuseCommonDraw = "FuseCommonDraw";

        /// <summary>Common SDF utilities mixin.</summary>
        public const string FuseCommonSDF = "FuseCommonSDF";

        /// <summary>Common SDG utilities mixin.</summary>
        public const string FuseCommonSDG = "FuseCommonSDG";

        /// <summary>Common types mixin.</summary>
        public const string FuseCommonTypes = "FuseCommonTypes";

        /// <summary>Core transform utilities mixin.</summary>
        public const string FuseCoreTransform = "FuseCoreTransform";

        /// <summary>Core color utilities mixin.</summary>
        public const string FuseCoreColor = "FuseCoreColor";

        /// <summary>Core texture utilities mixin.</summary>
        public const string FuseCoreTexture = "FuseCoreTexture";
    }

    /// <summary>
    /// Buffer type identifiers used in shader generation.
    /// </summary>
    public static class BufferTypes
    {
        /// <summary>Structured buffer (read-only).</summary>
        public const string StructuredBuffer = "StructuredBuffer";

        /// <summary>Read-write structured buffer.</summary>
        public const string RWStructuredBuffer = "RWStructuredBuffer";

        /// <summary>Append structured buffer.</summary>
        public const string AppendStructuredBuffer = "AppendStructuredBuffer";

        /// <summary>Consume structured buffer.</summary>
        public const string ConsumeStructuredBuffer = "ConsumeStructuredBuffer";
    }

    /// <summary>
    /// Node name prefixes for common operations.
    /// </summary>
    public static class NodeNames
    {
        /// <summary>Buffer get operation.</summary>
        public const string GetBuffer = "getBuffer";

        /// <summary>Buffer set operation.</summary>
        public const string SetBuffer = "setBuffer";

        /// <summary>Buffer append operation.</summary>
        public const string AppendBuffer = "appendBuffer";

        /// <summary>Buffer consume operation.</summary>
        public const string ConsumeBuffer = "consumeBuffer";

        /// <summary>Buffer get dimensions operation.</summary>
        public const string BufferGetDimension = "bufferGetDimension";
    }
}
