using System;
using System.Diagnostics;
using System.IO;
using VL.Core;
using VL.Core.CompilerServices;

[assembly: AssemblyInitializer(typeof(Fuse.MixinNodeFactory.Initialization))]

namespace Fuse.MixinNodeFactory;

/// <summary>
/// Logging helper for MixinNodeFactory debugging.
/// </summary>
public static class MixinNodeFactoryLogger
{
    private static readonly string LogFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
        "MixinNodeFactory.log");

    public static void Log(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
        Debug.WriteLine(line);
        Trace.WriteLine(line);
        try
        {
            File.AppendAllText(LogFile, line + Environment.NewLine);
        }
        catch { }
    }
}

/// <summary>
/// Assembly initializer that registers the MixinShaderNodes factory with VL.
/// </summary>
public sealed class Initialization : AssemblyInitializer<Initialization>
{
    public override void Configure(AppHost appHost)
    {
        MixinNodeFactoryLogger.Log("Configure called - registering factory");

        try
        {
            appHost.RegisterNodeFactory("Fuse.MixinNodeFactory.MixinShaderNodes", MixinShaderNodes.Init);
            MixinNodeFactoryLogger.Log("RegisterNodeFactory completed");
        }
        catch (Exception ex)
        {
            MixinNodeFactoryLogger.Log($"Error: {ex}");
        }
    }
}
