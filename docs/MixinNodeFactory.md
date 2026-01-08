# MixinNodeFactory

The MixinNodeFactory automatically generates VL nodes from SDSL mixin shader files. Functions marked with special comment directives are parsed and exposed as nodes in the VL node browser.

## Quick Start

1. Create a `shaders/` folder in your VL project directory
2. Add `.sdsl` files with exported functions
3. Nodes appear automatically in the node browser under their specified namespace

## Comment-Based Metadata Syntax

Use comments to annotate functions for export. This approach doesn't affect SDSL compilation.

### Basic Example

```hlsl
shader MyMathMixin
{
    // @export
    // @namespace Fuse.Math
    // @summary Signed power function that preserves sign
    // @param a Base value
    // @param b Exponent
    float signedPow(float a, float b)
    {
        return sign(a) * pow(abs(a), b);
    }
}
```

This creates a node called `signedPow` under `Fuse.Math` in the node browser.

### Supported Directives

| Directive | Required | Description | Example |
|-----------|----------|-------------|---------|
| `@export` | Yes | Marks function for node generation | `// @export` |
| `@namespace` | No | Node browser category path (dot-separated) | `// @namespace Fuse.SDF.3D` |
| `@summary` | No | Node tooltip/description | `// @summary Computes smooth minimum` |
| `@param name` | No | Parameter description | `// @param radius Sphere radius` |
| `@default name value` | No | Default value for parameter | `// @default radius 1.0` |
| `@groupable` | No | Enable groupable mode for the node | `// @groupable` |
| `@groupoptions n` | No | Number of group options | `// @groupoptions 2` |

### Out Parameters

Functions with `out` parameters automatically get additional output pins:

```hlsl
// @export
// @namespace Fuse.SDF.Combine
// @summary Smooth union with blend factor output
// @param d1 First distance
// @param d2 Second distance
// @param k Smoothness factor
// @param blend Output blend factor (0 = d1, 1 = d2)
float smoothUnion(float d1, float d2, float k, out float blend)
{
    float h = clamp(0.5 + 0.5 * (d2 - d1) / k, 0.0, 1.0);
    blend = h;
    return lerp(d2, d1, h) - k * h * (1.0 - h);
}
```

This creates a node with:
- **Inputs**: `d1`, `d2`, `k`
- **Outputs**: `Output` (return value), `blend` (out parameter)

### Supported Types

| SDSL Type | VL Type | Default Value |
|-----------|---------|---------------|
| `float` | `Float32` | `0` |
| `float2` | `Vector2` | `(0, 0)` |
| `float3` | `Vector3` | `(0, 0, 0)` |
| `float4` | `Vector4` | `(0, 0, 0, 0)` |
| `int` | `Int32` | `0` |
| `int2` | `Int2` | `(0, 0)` |
| `int3` | `Int3` | `(0, 0, 0)` |
| `int4` | `Int4` | `(0, 0, 0, 0)` |
| `uint` | `UInt32` | `0` |
| `bool` | `Boolean` | `false` |
| `float4x4` | `Matrix` | `Identity` |
| `float3x3` | `Matrix3` | - |

## Complete Example

```hlsl
// SDF utility functions for VL.Fuse
// Place in: YourProject/shaders/SDFUtils.sdsl

shader SDFUtils
{
    // @export
    // @namespace Fuse.SDF.3D
    // @summary Signed distance to a sphere
    // @param p Sample position
    // @param radius Sphere radius
    // @default radius 1.0
    float sdSphere(float3 p, float radius = 1.0)
    {
        return length(p) - radius;
    }

    // @export
    // @namespace Fuse.SDF.3D
    // @summary Signed distance to a box
    // @param p Sample position
    // @param size Box half-extents
    float sdBox(float3 p, float3 size)
    {
        float3 q = abs(p) - size;
        return length(max(q, 0.0)) + min(max(q.x, max(q.y, q.z)), 0.0);
    }

    // @export
    // @namespace Fuse.SDF.Combine
    // @summary Smooth minimum of two distances
    // @param a First distance
    // @param b Second distance
    // @param k Smoothness factor
    // @default k 0.1
    // @groupable
    float smin(float a, float b, float k = 0.1)
    {
        float h = max(k - abs(a - b), 0.0) / k;
        return min(a, b) - h * h * k * 0.25;
    }

    // @export
    // @namespace Fuse.Transform
    // @summary Repeats space infinitely with cell index output
    // @param p Input position
    // @param spacing Repetition spacing
    // @param cellId Output cell index for variation
    float3 opRepeat(float3 p, float3 spacing, out float3 cellId)
    {
        cellId = floor(p / spacing);
        return mod(p + 0.5 * spacing, spacing) - 0.5 * spacing;
    }

    // This function is NOT exported (no @export comment)
    float internalHelper(float x)
    {
        return x * x;
    }
}
```

## File Organization

Recommended structure for your project:

```
YourProject/
├── YourProject.vl
└── shaders/
    ├── SDFPrimitives.sdsl      # 3D SDF shapes
    ├── SDFOperations.sdsl      # Union, intersect, blend
    ├── MathUtils.sdsl          # Math helper functions
    └── ColorUtils.sdsl         # Color manipulation
```

## Hot Reload

The factory watches for file changes. When you modify a `.sdsl` file:
1. Save the file
2. The nodes update automatically in VL
3. No restart required

## Troubleshooting

### Nodes don't appear
- Ensure the `shaders/` folder is in your VL project directory (same level as your `.vl` file)
- Check that functions have the `// @export` comment
- Verify the shader file has `.sdsl` extension

### Wrong category
- Check the `@namespace` directive spelling
- Use dot-separated paths: `Fuse.Category.Subcategory`

### Missing parameters
- Ensure parameter types are supported (see type table above)
- Check for typos in `@param` directive names

---

# AI Prompt Suggestions for Generating Shaders

When using AI assistants (Claude, ChatGPT, etc.) to generate shader functions for VL.Fuse, use these prompts to get correctly formatted output.

## Basic Prompt Template

```
Generate an SDSL mixin shader function for VL.Fuse with the following requirements:

1. Use the MixinNodeFactory comment format:
   - Start with `// @export`
   - Add `// @namespace [Category.Path]` for node browser location
   - Add `// @summary [description]` for tooltip
   - Add `// @param [name] [description]` for each parameter
   - Add `// @default [name] [value]` for default values

2. Function: [describe what you want]
3. Namespace: [where it should appear in node browser]

Example format:
```hlsl
// @export
// @namespace Fuse.Math
// @summary Description here
// @param x Input value
// @default x 1.0
float myFunc(float x = 1.0)
{
    return x * 2.0;
}
```
```

## Specific Use Case Prompts

### SDF Primitives

```
Generate SDSL mixin functions for 3D signed distance field primitives.
Include sphere, box, torus, and cylinder.

Requirements:
- Use @export, @namespace, @summary, @param comments
- Place under namespace "Fuse.SDF.3D"
- Include sensible default values with @default
- Use float3 for position parameters
- Return float for distance
```

### SDF Operations

```
Generate SDSL mixin functions for SDF boolean operations.
Include union, intersection, subtraction, and smooth variants.

Requirements:
- Use MixinNodeFactory comment format (@export, @namespace, etc.)
- Place under namespace "Fuse.SDF.Combine"
- Smooth operations should have a smoothness parameter 'k' with @default 0.1
- Add @groupable for operations that can chain (union, intersection)
- For smooth operations, add an 'out float blend' parameter for material mixing
```

### Math Utilities

```
Generate SDSL mixin math utility functions for shader programming.
Include: remap, smoothstep variants, bias, gain, and pulse functions.

Requirements:
- Use @export and @namespace Fuse.Math
- Include @summary explaining each function's purpose
- Document all parameters with @param
- Add @default for commonly used default values
```

### Color Functions

```
Generate SDSL mixin functions for color space conversions and adjustments.
Include: RGB to HSV, HSV to RGB, contrast, saturation, hue shift.

Requirements:
- Use MixinNodeFactory format with @export
- Place under namespace "Fuse.Color"
- Use float3 for RGB/HSV colors
- Include @summary with clear descriptions
- Add @default for adjustment amounts (default to no change: 1.0 or 0.0)
```

### Domain Operations

```
Generate SDSL mixin functions for SDF domain manipulations.
Include: repeat, mirror, twist, bend, and scale operations.

Requirements:
- Use @export, @namespace Fuse.Transform
- Functions should take float3 position and return float3
- For operations that produce cell indices, use 'out float3 cellId' parameter
- Include @summary and @param for all
```

## Full Example Prompt

```
I'm using VL.Fuse's MixinNodeFactory to generate nodes from SDSL shaders.

Please create a complete shader file with noise functions. Include:
1. 2D and 3D hash functions
2. Value noise
3. Gradient noise (Perlin-like)
4. Simplex noise

Format requirements:
- Wrap in a shader block: `shader NoiseMixin { ... }`
- Each exported function needs these comments BEFORE it:
  // @export
  // @namespace Fuse.Noise
  // @summary [what it does]
  // @param [name] [description]
  // @default [name] [value]  (if applicable)

- Use float/float2/float3/float4 types
- Helper functions without @export won't become nodes

Output as a complete .sdsl file I can save directly.
```

## Tips for AI-Generated Shaders

1. **Be specific about the comment format** - AI models may not know the MixinNodeFactory syntax
2. **Provide an example** - Include one correctly formatted function in your prompt
3. **Specify the namespace** - Tell the AI where nodes should appear
4. **Request defaults** - Ask for `@default` directives for optional parameters
5. **Mention out parameters** - If you need multiple outputs, specify `out` parameters
6. **Ask for complete files** - Request the full shader wrapper, not just functions
