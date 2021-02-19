namespace Fuse.math
{
    public enum DistanceMetricsSelector
    {
        Euclidean,
        EuclideanSquared,
        Chebyshev,
        Manhattan,
        Canberra,
        CosineSimilarity,
        PearsonCorrelation,
        Minkowski // not sure about this guy, since complicates the signature 
    }
}