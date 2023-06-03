using System;

namespace Fuse.Noise
{
    public enum NoiseBasisType
    {
        Sine,
        ValueNoise,
        GradientNoise,
        Simplex,
        WorleySimple,
        Random
    };

    public enum NoiseBasisInflection
    {
        None,
        Ridge,
        Turbulence
    };

    public enum FBMBasisType
    {
        Additive,
        Multiplicative,
        Hybrid,
        Heterogeneous
    };


    public enum WorleyCellDistance
    {
        Euclidean,
        EuclideanSquared,
        Chebyshev,
        Manhattan,
        Minkowski,
        Cubes
    } // may use general distane enum from Fuse.Core.Domain instead

    public enum WorleyCellFunction
    {
        F1,
        F2,
        F2MinusF1,
        F1PlusF2,
        Average,
        Crackle
    }
}