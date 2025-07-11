namespace Fuse.Calculus;

public enum GradientDifferentiationMode
{
    ForwardDifference,
    CentralDifference,
    FivePointStencil
}

public enum IntegrationMode
{
    Euler,
    RungeKutta2,
    RungeKutta4
}

public enum ComputeIntegrationMode
{
    EulerForward,
    EulerAccel,
    VerletAccel
}

public enum ComputeIntegrationModeRegion
{
    EulerForward,
    RungeKutta2Forward,
    RungeKutta4Forward,
    EulerAccel,
    VerletAccel,
    LeapFrogAccel,
    RungeKutta2Accel,
    RungeKutta4Accel
}