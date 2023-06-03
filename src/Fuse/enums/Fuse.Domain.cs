using System;

namespace Fuse.Core.Domain
{
    public enum DistanceMetric { Euclidean, EuclideanSquared, Chebyshev, Manhattan, Canberra, CosineSimilarity, PearsonCorrelation, Minkowski };

    public enum DomainRepeat2D { WrapXY, MirrorXY, GridXY, WrapX, MirrorX, WrapY, MirrorY, PolarXY };

    public enum DomainRepeat3D { WrapX, WrapY, WrapZ, MirrorX, MirrorY, MirrorZ, WrapXY, WrapXZ, WrapYZ, MirrorXY, MirrorXZ, MirrorYZ, WrapXYZ, PolarXY, PolarXZ, PolarYZ };
}
