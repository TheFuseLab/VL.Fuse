using System;

namespace Fuse.Color
{
    public enum ColorSpace { RGB, HSL, HSV, LAB, SRGB };
    
    public enum Spectrum
    {
        SpectralBruton,
        SpectralGems,
        SpectralJet,
        SpectralZucconi6,
        SpectralZucconi,
        SpectralSpektre
    }
    
    public enum Tonemap
    {
        Linear,
        SimpleReinhard,
        LumaBasedReinhard,
        WhitePreservingLumaBasedReinhard,
        RomBinDaHouse,
        Filmic,
        Uncharted2,
        ACESFitted,
        Drago,
        Ward,
        Unreal
    }

}
