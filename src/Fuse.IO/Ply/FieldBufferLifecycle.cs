#pragma warning disable CS1591

namespace Fuse.IO.Ply;

/// <summary>
/// When the PLY field buffers of a <see cref="PlyOctreeLoader"/> in GpuOnly mode are on the GPU, when the consumer
/// has to (re)initialise from them, and when they may be released. Kept apart from the loader so the rules can be
/// checked without a graphics device.
///
/// Every frame the caller passes the release switch, the init request (in the Gallery A forest: preheating) and the
/// time. The answer says whether the consumer should read the fields now (<see cref="InitFields"/>) and whether the
/// loader has to release or reload them.
///
/// - Init pending: set by a new file load and by the rising edge of the init request. While it is set, the consumer
///   reads the fields as soon as they are on the GPU (also outside a preheat). It is cleared only after the fields
///   were read for <see cref="MinInitSeconds"/> and the init request is off. So a load that finishes after the
///   preheat, or a preheat shorter than a reload, still initialises once the data is there.
/// - InitFields = fields on the GPU AND (init request OR init pending). Never true without the fields.
/// - Release: only with the switch on, no init request, nothing pending, and after one completed init since the
///   file was loaded.
/// - Reload: when released and the fields are needed (switch off, init request, or init pending). A failed reload is
///   tried again after <see cref="RetrySeconds"/>.
/// </summary>
public sealed class FieldBufferLifecycle
{
    public enum State { Resident, Released, Reloading }

    public enum Action { None, Release, StartReload }

    public double MinInitSeconds { get; set; } = 2.0;
    public double RetrySeconds { get; set; } = 30.0;

    private bool _pending = true;
    private bool _initialized;
    private bool _lastRequest;
    private double? _initSince;
    private double _retryAt = double.NegativeInfinity;

    public State Current { get; private set; } = State.Resident;
    public bool InitPending => _pending;
    public bool FieldsReady => Current == State.Resident;
    public bool InitFields { get; private set; }

    /// <summary>A new load of the file: buffers resident, the consumer has not read them yet.</summary>
    public void Reset()
    {
        Current = State.Resident;
        _pending = true;
        _initialized = false;
        _initSince = null;
        _retryAt = double.NegativeInfinity;
        InitFields = false;
    }

    public Action Step(bool releaseSwitch, bool initRequest, double time)
    {
        if (initRequest && !_lastRequest)
            _pending = true;
        _lastRequest = initRequest;

        InitFields = FieldsReady && (initRequest || _pending);
        if (InitFields)
        {
            _initSince ??= time;
            if (time - _initSince.Value >= MinInitSeconds)
            {
                _initialized = true;
                if (!initRequest)
                    _pending = false;   // this frame still reads; from the next one on it is done
            }
        }
        else
        {
            _initSince = null;
        }

        switch (Current)
        {
            case State.Resident:
                if (releaseSwitch && !initRequest && !_pending && _initialized && !InitFields)
                {
                    Current = State.Released;
                    return Action.Release;
                }
                return Action.None;

            case State.Released:
                if ((!releaseSwitch || initRequest || _pending) && time >= _retryAt)
                {
                    Current = State.Reloading;
                    return Action.StartReload;
                }
                return Action.None;

            default:
                return Action.None;
        }
    }

    public void ReloadFinished(bool ok, double time)
    {
        if (Current != State.Reloading)
            return;
        Current = ok ? State.Resident : State.Released;
        _initSince = null;
        if (!ok)
            _retryAt = time + RetrySeconds;
    }
}
