using System;

namespace Fuse.Core.Color
{
    public enum ColorSpace { RGB, HSL, HSV, LAB, SRGB };

    public enum BlendMode
    {
        Normal,
        
        Darken,
        Multiply,
        ColorBurn,
        LinearBurn,
        
        Lighten,
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
        
        Difference,
        Exclusion,
        Subtract,
        Divide
    }
}
