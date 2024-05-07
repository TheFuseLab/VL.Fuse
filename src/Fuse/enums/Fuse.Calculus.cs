using System;

namespace Fuse.Calculus
{
    public enum GradientDifferentiationMode { ForwardDifference, CentralDifference, FivePointStencil };

    public enum IntegrationMode { Euler, RungeKutta2, RungeKutta4 };
    
    
    public enum ComputeIntegrationMode { Direct, Euler, Verlet, LeapFrog, RungeKutta2, RungeKutta4 };

}
