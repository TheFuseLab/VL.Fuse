using System;

namespace Fuse.Core.Compositing
{
    public enum BlendMode
    {
        Normal,
        Add,
        Subtract,
        Multiply,
        Divide,
        Min,
        Max,
        Difference,

        ColorBurn,
        LinearBurn,

        Screen,
        Dodge,
        LinearDodge,

        Overlay,
        SoftLight,
        HardLight,
        VividLight,
        LinearLight,
        PinLight,
        HardMix,

        Exclusion,
    }
}
