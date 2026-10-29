using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using SharpDX.Direct3D11;
using Stride.Graphics;

namespace Fuse.Diagnostics;

/// <summary>
/// Counts the D3D11 debug layer messages of a graphics device in-process through ID3D11InfoQueue, so a test run does
/// not depend on OutputDebugString listeners (DebugView, dbwin) that can be taken over by other tools.
/// </summary>
/// <remarks>
/// The debug layer must be active for the process (dxcpl "Force On" for the executable, or a device created with the
/// debug flag); without it the device has no InfoQueue and <see cref="IsAvailable"/> stays false, which a test can
/// report instead of counting zero. Call <see cref="Attach"/> once with the app's device and <see cref="Drain"/>
/// every frame from the main loop: the queue keeps at most <see cref="MessageCountLimit"/> messages, everything
/// beyond that is discarded and counted in <c>Discarded</c>. Messages are grouped as the dbwin log reader does,
/// "SEVERITY MESSAGE_ID" (e.g. "ERROR DEVICE_SHADERRESOURCEVIEW_BUFFER_TYPE_MISMATCH"), with one sample text per
/// group. <see cref="MuteDebugOutput"/> stops the layer's OutputDebugString output, which costs frame time when
/// thousands of messages are written per frame.
/// </remarks>
public static class D3D11DebugMessages
{
    private static readonly object Lock = new();
    private static readonly Regex IdName = new(@"#\d+:\s*(\w+)\s*\]", RegexOptions.Compiled);
    private static readonly Dictionary<string, long> Counts = new();
    private static readonly Dictionary<string, string> Samples = new();
    private static InfoQueue _queue;
    private static GraphicsDevice _device;
    private static string _state = "not attached";
    private static long _total, _discarded, _discardedSeen, _drains;
    private static DateTime _since = DateTime.Now;

    public const int MessageCountLimit = 1 << 16;

    /// <summary>True while the debug layer is active and the queue is attached.</summary>
    public static bool IsAvailable => _queue != null;

    /// <summary>"on", or why no messages can be counted.</summary>
    public static string State => _state;

    /// <summary>
    /// Attaches to the device's InfoQueue. Returns false when the debug layer is not active for this process.
    /// Attaching the same device again is a no-op.
    /// </summary>
    public static bool Attach(GraphicsDevice theDevice)
    {
        lock (Lock)
        {
            if (theDevice == null)
            {
                _state = "no graphics device";
                return false;
            }

            if (ReferenceEquals(theDevice, _device) && _queue != null) return true;
            Detach();
            _device = theDevice;
            try
            {
                if (SharpDXInterop.GetNativeDevice(theDevice) is not Device native)
                {
                    _state = "not a D3D11 device";
                    return false;
                }

                _queue = native.QueryInterfaceOrNull<InfoQueue>();
                if (_queue == null)
                {
                    _state = "debug layer not active (dxcpl Force On?)";
                    return false;
                }

                _queue.MessageCountLimit = MessageCountLimit;
                _discardedSeen = _queue.NumMessagesDiscardedByMessageCountLimit;
                _state = "on";
                _since = DateTime.Now;
                return true;
            }
            catch (Exception e)
            {
                _state = "attach failed: " + e.Message;
                _queue = null;
                return false;
            }
        }
    }

    public static void Detach()
    {
        lock (Lock)
        {
            _queue?.Dispose();
            _queue = null;
            _device = null;
            _state = "not attached";
        }
    }

    /// <summary>Reads and clears the stored messages; call once per frame on the main loop.</summary>
    public static void Drain()
    {
        lock (Lock)
        {
            if (_queue == null) return;
            try
            {
                var stored = _queue.NumStoredMessages;
                for (long i = 0; i < stored; i++)
                {
                    var message = _queue.GetMessage(i);
                    var key = Key(message);
                    Counts[key] = Counts.TryGetValue(key, out var n) ? n + 1 : 1;
                    if (!Samples.ContainsKey(key))
                        Samples[key] = Trim(message.Description);
                }

                _total += stored;
                var discarded = _queue.NumMessagesDiscardedByMessageCountLimit;
                _discarded += System.Math.Max(0, discarded - _discardedSeen);
                _discardedSeen = discarded;
                _queue.ClearStoredMessages();
                _drains++;
            }
            catch (Exception e)
            {
                _state = "drain failed: " + e.Message;
            }
        }
    }

    /// <summary>Counts since the last reset (or attach). With <paramref name="theReset"/> the counters start over.</summary>
    public static Dictionary<string, object> Snapshot(bool theReset = false)
    {
        lock (Lock)
        {
            var counts = new Dictionary<string, long>(Counts);
            var result = new Dictionary<string, object>
            {
                ["available"] = _queue != null,
                ["state"] = _state,
                ["total"] = _total,
                ["discarded"] = _discarded,
                ["drains"] = _drains,
                ["seconds"] = System.Math.Round((DateTime.Now - _since).TotalSeconds, 2),
                ["muted"] = _queue != null && (bool)_queue.MuteDebugOutput,
                ["counts"] = counts,
                ["samples"] = new Dictionary<string, string>(Samples)
            };
            if (theReset) ResetLocked();
            return result;
        }
    }

    public static void Reset()
    {
        lock (Lock) ResetLocked();
    }

    /// <summary>Stops or restores the debug layer's OutputDebugString output (the InfoQueue keeps counting).</summary>
    public static bool MuteDebugOutput
    {
        get { lock (Lock) return _queue != null && (bool)_queue.MuteDebugOutput; }
        set { lock (Lock) if (_queue != null) _queue.MuteDebugOutput = value; }
    }

    private static void ResetLocked()
    {
        Counts.Clear();
        Samples.Clear();
        _total = 0;
        _discarded = 0;
        _drains = 0;
        _since = DateTime.Now;
    }

    private static string Key(Message theMessage)
    {
        var severity = theMessage.Severity switch
        {
            MessageSeverity.Corruption => "CORRUPTION",
            MessageSeverity.Error => "ERROR",
            MessageSeverity.Warning => "WARNING",
            MessageSeverity.Information => "INFO",
            _ => "MESSAGE"
        };
        var match = IdName.Match(theMessage.Description ?? "");
        var name = match.Success ? match.Groups[1].Value : MacroName(theMessage.Id.ToString());
        return severity + " " + name;
    }

    /// <summary>
    /// SharpDX enum name to the D3D11_MESSAGE_ macro spelling as far as it can be derived: "MessageIdDeviceDrawSamplerNotSet"
    /// becomes "DEVICE_DRAW_SAMPLER_NOT_SET". Words the macro writes without underscore (UNORDEREDACCESSVIEW) come out
    /// separated (UNORDERED_ACCESS_VIEW); compare keys with underscores removed when matching dbwin log output.
    /// </summary>
    private static string MacroName(string theEnumName)
    {
        if (theEnumName.StartsWith("MessageId")) theEnumName = theEnumName.Substring("MessageId".Length);
        return Regex.Replace(theEnumName, "(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])", "_").ToUpperInvariant();
    }

    private static string Trim(string theText)
    {
        if (string.IsNullOrEmpty(theText)) return "";
        theText = theText.Trim();
        return theText.Length <= 300 ? theText : theText.Substring(0, 300) + "…";
    }
}
