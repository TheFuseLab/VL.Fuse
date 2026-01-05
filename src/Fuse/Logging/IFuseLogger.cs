using System;
using System.Diagnostics;

#nullable enable

namespace Fuse.Logging;

/// <summary>
/// Interface for logging throughout the Fuse library.
/// Implement this interface to integrate with your preferred logging framework.
/// </summary>
public interface IFuseLogger
{
    /// <summary>
    /// Logs a debug message. Use for detailed diagnostic information.
    /// </summary>
    /// <param name="message">The message to log.</param>
    void Debug(string message);

    /// <summary>
    /// Logs an informational message.
    /// </summary>
    /// <param name="message">The message to log.</param>
    void Info(string message);

    /// <summary>
    /// Logs a warning message. Use for potentially harmful situations.
    /// </summary>
    /// <param name="message">The message to log.</param>
    void Warning(string message);

    /// <summary>
    /// Logs an error message with an optional exception.
    /// </summary>
    /// <param name="message">The message to log.</param>
    /// <param name="ex">Optional exception that caused the error.</param>
    void Error(string message, Exception? ex = null);
}

/// <summary>
/// Default logger implementation that writes to Debug output and Console.
/// </summary>
public class DefaultFuseLogger : IFuseLogger
{
    /// <summary>
    /// Singleton instance of the default logger.
    /// </summary>
    public static readonly DefaultFuseLogger Instance = new();

    /// <inheritdoc />
    public void Debug(string message)
    {
        System.Diagnostics.Debug.WriteLine($"[FUSE:DEBUG] {message}");
    }

    /// <inheritdoc />
    public void Info(string message)
    {
        System.Diagnostics.Debug.WriteLine($"[FUSE:INFO] {message}");
    }

    /// <inheritdoc />
    public void Warning(string message)
    {
        Console.WriteLine($"[FUSE:WARN] {message}");
    }

    /// <inheritdoc />
    public void Error(string message, Exception? ex = null)
    {
        var exInfo = ex != null ? $" | Exception: {ex.GetType().Name}: {ex.Message}" : "";
        Console.WriteLine($"[FUSE:ERROR] {message}{exInfo}");
        System.Diagnostics.Debug.WriteLine($"[FUSE:ERROR] {message}{exInfo}");
        if (ex != null)
        {
            System.Diagnostics.Debug.WriteLine($"[FUSE:ERROR] StackTrace: {ex.StackTrace}");
        }
    }
}

/// <summary>
/// Global logger configuration for the Fuse library.
/// </summary>
public static class FuseLogger
{
    private static IFuseLogger _instance = DefaultFuseLogger.Instance;

    /// <summary>
    /// Gets or sets the current logger instance.
    /// Set this to integrate with your preferred logging framework.
    /// </summary>
    public static IFuseLogger Instance
    {
        get => _instance;
        set => _instance = value ?? DefaultFuseLogger.Instance;
    }

    /// <summary>
    /// Logs a debug message.
    /// </summary>
    public static void Debug(string message) => _instance.Debug(message);

    /// <summary>
    /// Logs an informational message.
    /// </summary>
    public static void Info(string message) => _instance.Info(message);

    /// <summary>
    /// Logs a warning message.
    /// </summary>
    public static void Warning(string message) => _instance.Warning(message);

    /// <summary>
    /// Logs an error message with optional exception.
    /// </summary>
    public static void Error(string message, Exception? ex = null) => _instance.Error(message, ex);
}
