namespace Fuse.Math
{
    public enum Constrain { Off, Clamp, Wrap, Mirror };
    public enum EaseMode 
    {
        backInOut,
        backIn,
        backOut,
        bounceOut,
        bounceIn,
        bounceInOut,
        circularInOut,
        circularIn,
        circularOut,
        cubicInOut,
        cubicIn,
        cubicOut,
        elasticInOut,
        elasticIn,
        elasticOut,
        exponentialInOut,
        exponentialIn,
        exponentialOut,
        linear,
        quadraticInOut,
        quadraticIn,
        quadraticOut,
        quarticInOut,
        quarticIn,
        quarticOut,
        qinticInOut,
        qinticIn,
        qinticOut,
        sineInOut,
        sineIn,
        sineOut
    };
    
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

    public enum InterpolationMode
    {
        Linear,
        Quadratic,
        Cubic,
        Quartic,
        Quintic,
        General
    }

    public enum SplineMode
    {
        Cubic,
        Hermite,
        Seiler
    }
    
    public enum NeighborSplineMode
    {
        CatmullRom,
        BSpline,
        Cardinal,
        KochanekBartels
    }
}
