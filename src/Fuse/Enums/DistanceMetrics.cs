namespace Fuse.math
{
    public enum DistanceMetrics
    {
        Euclidean,
        EuclideanSquared,
        Chebyshev,
        Manhattan,
        Canberra,
        CosineSimilarity,
        PearsonCorrelation,
        Minkowski //not sure about this guy, since changes the complicates the signature 
    }
}