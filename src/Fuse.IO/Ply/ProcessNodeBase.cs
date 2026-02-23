namespace Fuse.IO.Ply;

#pragma warning disable CS1591

public abstract class ProcessNodeBase : IDisposable
{
    private readonly string _nodeName;
    private readonly int _instanceId;
    private bool _diagRegistered;
    private bool _isDisposed;
    private int _generation;

    protected ProcessNodeBase(string nodeName)
    {
        _nodeName = nodeName;
        _instanceId = PlyDiagnosticLog.GetInstanceId(this);
    }

    protected int InstanceId => _instanceId;
    protected bool IsDisposed => _isDisposed;

    protected int BeginGeneration() => Interlocked.Increment(ref _generation);

    protected bool IsCurrentGeneration(int generation) =>
        !_isDisposed && generation == Volatile.Read(ref _generation);

    protected (int generation, CancellationToken token) BeginRun(ref CancellationTokenSource? cts)
    {
        var generation = BeginGeneration();
        cts?.Cancel();
        cts?.Dispose();
        cts = new CancellationTokenSource();
        return (generation, cts.Token);
    }

    protected bool IsRisingEdge(ref bool lastValue, bool currentValue)
    {
        var rising = currentValue && !lastValue;
        lastValue = currentValue;
        return rising;
    }

    protected void UpdateDiagnostics(bool debug, Func<string?>? callsiteFactory = null)
    {
        PlyDiagnosticLog.SetDebugEnabled(_nodeName, _instanceId, debug);
        if (!debug || _diagRegistered) return;
        PlyDiagnosticLog.RegisterInstance(_nodeName, _instanceId, callsiteFactory?.Invoke());
        _diagRegistered = true;
    }

    protected void WriteDiagnostic(string message) => PlyDiagnosticLog.Write(_nodeName, _instanceId, message);

    protected void TrackAllocationEstimate(string reason, long bytes)
    {
        PlyDiagnosticLog.TrackEstimatedAllocation(_nodeName, _instanceId, bytes, reason);
    }

    protected void TrackReleaseEstimate(string reason, Func<long> estimateRetainedBytes, Action clearAction)
    {
        var before = estimateRetainedBytes();
        clearAction();
        var released = Math.Max(0, before - estimateRetainedBytes());
        PlyDiagnosticLog.TrackEstimatedRelease(_nodeName, _instanceId, released, reason);
    }

    protected void TrackExplicitRelease(string reason, long bytes)
    {
        PlyDiagnosticLog.TrackEstimatedRelease(_nodeName, _instanceId, bytes, reason);
    }

    protected virtual void OnDisposeManaged()
    {
    }

    public void Dispose()
    {
        if (_isDisposed) return;

        _isDisposed = true;
        PlyDiagnosticLog.SetDebugEnabled(_nodeName, _instanceId, false);
        Interlocked.Increment(ref _generation);
        try
        {
            PlyDiagnosticLog.Write(_nodeName, _instanceId, $"Dispose {PlyDiagnosticLog.GetMemorySnapshot()}");
            PlyDiagnosticLog.MarkDisposed(_nodeName, _instanceId);
        }
        catch
        {
            // best effort only
        }

        OnDisposeManaged();
    }
}
