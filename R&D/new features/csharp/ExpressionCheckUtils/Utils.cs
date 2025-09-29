namespace Fuse.AutoPins;

public enum ParamDir { In, Out, InOut }
public enum ResKind { None, Texture1D, Texture2D, Texture3D, Sampler, Buffer, RWTexture2D, RWTexture3D }

public sealed record Param(
    string Name,
    string Type,           // e.g. "float3", "float4x4", "Texture3D<float>"
    ParamDir Direction,
    bool IsConst,
    ResKind ResourceKind
);

public sealed record Signature(
    string FunctionName,
    IReadOnlyList<Param> Params,
    string[] RequiredIncludes
);

// runtime value container (VL-friendly)
public abstract record PinValue;
public sealed record VFloat(float V): PinValue;
public sealed record VFloat2(System.Numerics.Vector2 V): PinValue;
public sealed record VFloat3(System.Numerics.Vector3 V): PinValue;
public sealed record VFloat4(System.Numerics.Vector4 V): PinValue;
public sealed record VMatrix(float[] M4x4RowMajor): PinValue;
public sealed record VInt(int V): PinValue;
public sealed record VBool(bool V): PinValue;
public sealed record VTex2D(object Texture): PinValue;     // keep as object for VL resource handle
public sealed record VTex3D(object Texture): PinValue;
public sealed record VSampler(object Sampler): PinValue;
public sealed record VBuffer(object Buffer): PinValue;