using System;
using System.Collections.Generic;
using Stride.Core.Mathematics;

namespace Fuse.compute;

public readonly record struct ComputeDispatchSize(long X, long Y, long Z)
{
    public static ComputeDispatchSize Zero => new(0, 0, 0);

    public static ComputeDispatchSize One => new(1, 1, 1);

    public static ComputeDispatchSize FromInt3(Int3 value)
    {
        return new ComputeDispatchSize(value.X, value.Y, value.Z);
    }

    public Int3 ToInt3()
    {
        return new Int3(CheckedInt(X), CheckedInt(Y), CheckedInt(Z));
    }

    public long ThreadCount
    {
        get
        {
            try
            {
                return checked(X * Y * Z);
            }
            catch (OverflowException)
            {
                return long.MaxValue;
            }
        }
    }

    public override string ToString()
    {
        return $"{X}, {Y}, {Z}";
    }

    private static int CheckedInt(long value)
    {
        if (value < int.MinValue || value > int.MaxValue)
            throw new OverflowException($"Dispatch dimension {value} does not fit into Int32.");

        return (int)value;
    }
}

public readonly record struct ComputeDispatchRequest(
    ComputeDispatchSize ElementCount,
    ComputeDispatchSize ThreadGroupSize)
{
    public static ComputeDispatchRequest For1D(long elementCount, long threadGroupSize)
    {
        return new ComputeDispatchRequest(
            new ComputeDispatchSize(elementCount, 1, 1),
            new ComputeDispatchSize(threadGroupSize, 1, 1));
    }
}

public readonly record struct ComputeDispatchLimits(
    ComputeDispatchSize MaxDispatchGroups,
    ComputeDispatchSize MaxThreadGroupSize,
    long MaxThreadsPerGroup)
{
    public static ComputeDispatchLimits D3D11 { get; } = new(
        new ComputeDispatchSize(65_535, 65_535, 65_535),
        new ComputeDispatchSize(1_024, 1_024, 64),
        1_024);
}

public enum ComputeDispatchDiagnosticCode
{
    ElementCountNonPositive,
    ThreadGroupSizeNonPositive,
    ThreadGroupDimensionExceeded,
    ThreadGroupThreadCountExceeded,
    DispatchGroupCountExceeded
}

public readonly record struct ComputeDispatchDiagnostic(
    ComputeDispatchDiagnosticCode Code,
    string Dimension,
    long Value,
    long Limit,
    string Message);

public sealed class ComputeDispatchValidationResult
{
    internal ComputeDispatchValidationResult(
        ComputeDispatchRequest request,
        ComputeDispatchLimits limits,
        ComputeDispatchSize dispatchGroups,
        IReadOnlyList<ComputeDispatchDiagnostic> diagnostics)
    {
        Request = request;
        Limits = limits;
        DispatchGroups = dispatchGroups;
        Diagnostics = diagnostics;
    }

    public ComputeDispatchRequest Request { get; }

    public ComputeDispatchLimits Limits { get; }

    public ComputeDispatchSize DispatchGroups { get; }

    public IReadOnlyList<ComputeDispatchDiagnostic> Diagnostics { get; }

    public bool IsValid => Diagnostics.Count == 0;
}

public static class ComputeDispatchValidator
{
    public static ComputeDispatchValidationResult Validate1D(
        long elementCount,
        long threadGroupSize,
        ComputeDispatchLimits? limits = null)
    {
        return Validate(ComputeDispatchRequest.For1D(elementCount, threadGroupSize), limits);
    }

    public static ComputeDispatchValidationResult Validate(
        ComputeDispatchRequest request,
        ComputeDispatchLimits? limits = null)
    {
        var actualLimits = limits ?? ComputeDispatchLimits.D3D11;
        var diagnostics = new List<ComputeDispatchDiagnostic>();

        ValidatePositive(
            request.ElementCount,
            ComputeDispatchDiagnosticCode.ElementCountNonPositive,
            "Element count",
            diagnostics);

        ValidatePositive(
            request.ThreadGroupSize,
            ComputeDispatchDiagnosticCode.ThreadGroupSizeNonPositive,
            "Thread group size",
            diagnostics);

        ValidateThreadGroupDimensions(request.ThreadGroupSize, actualLimits, diagnostics);
        ValidateThreadCount(request.ThreadGroupSize, actualLimits, diagnostics);

        var dispatchGroups = CalculateDispatchGroups(request.ElementCount, request.ThreadGroupSize);
        ValidateDispatchGroupCounts(dispatchGroups, actualLimits, diagnostics);

        return new ComputeDispatchValidationResult(
            request,
            actualLimits,
            dispatchGroups,
            diagnostics.AsReadOnly());
    }

    private static ComputeDispatchSize CalculateDispatchGroups(
        ComputeDispatchSize elementCount,
        ComputeDispatchSize threadGroupSize)
    {
        return new ComputeDispatchSize(
            CeilDividePositive(elementCount.X, threadGroupSize.X),
            CeilDividePositive(elementCount.Y, threadGroupSize.Y),
            CeilDividePositive(elementCount.Z, threadGroupSize.Z));
    }

    private static long CeilDividePositive(long value, long divisor)
    {
        if (value <= 0 || divisor <= 0)
            return 0;

        return value / divisor + (value % divisor == 0 ? 0 : 1);
    }

    private static void ValidatePositive(
        ComputeDispatchSize size,
        ComputeDispatchDiagnosticCode code,
        string label,
        List<ComputeDispatchDiagnostic> diagnostics)
    {
        AddNonPositiveDiagnostic(size.X, code, "X", label, diagnostics);
        AddNonPositiveDiagnostic(size.Y, code, "Y", label, diagnostics);
        AddNonPositiveDiagnostic(size.Z, code, "Z", label, diagnostics);
    }

    private static void AddNonPositiveDiagnostic(
        long value,
        ComputeDispatchDiagnosticCode code,
        string dimension,
        string label,
        List<ComputeDispatchDiagnostic> diagnostics)
    {
        if (value > 0)
            return;

        diagnostics.Add(new ComputeDispatchDiagnostic(
            code,
            dimension,
            value,
            1,
            $"{label} {dimension} must be greater than zero, but was {value}."));
    }

    private static void ValidateThreadGroupDimensions(
        ComputeDispatchSize threadGroupSize,
        ComputeDispatchLimits limits,
        List<ComputeDispatchDiagnostic> diagnostics)
    {
        AddExceededDiagnostic(
            threadGroupSize.X,
            limits.MaxThreadGroupSize.X,
            ComputeDispatchDiagnosticCode.ThreadGroupDimensionExceeded,
            "X",
            "Thread group size",
            diagnostics);
        AddExceededDiagnostic(
            threadGroupSize.Y,
            limits.MaxThreadGroupSize.Y,
            ComputeDispatchDiagnosticCode.ThreadGroupDimensionExceeded,
            "Y",
            "Thread group size",
            diagnostics);
        AddExceededDiagnostic(
            threadGroupSize.Z,
            limits.MaxThreadGroupSize.Z,
            ComputeDispatchDiagnosticCode.ThreadGroupDimensionExceeded,
            "Z",
            "Thread group size",
            diagnostics);
    }

    private static void ValidateThreadCount(
        ComputeDispatchSize threadGroupSize,
        ComputeDispatchLimits limits,
        List<ComputeDispatchDiagnostic> diagnostics)
    {
        var threadCount = threadGroupSize.ThreadCount;
        if (threadCount <= limits.MaxThreadsPerGroup)
            return;

        diagnostics.Add(new ComputeDispatchDiagnostic(
            ComputeDispatchDiagnosticCode.ThreadGroupThreadCountExceeded,
            "XYZ",
            threadCount,
            limits.MaxThreadsPerGroup,
            $"Thread group contains {threadCount} threads, but the limit is {limits.MaxThreadsPerGroup}."));
    }

    private static void ValidateDispatchGroupCounts(
        ComputeDispatchSize dispatchGroups,
        ComputeDispatchLimits limits,
        List<ComputeDispatchDiagnostic> diagnostics)
    {
        AddExceededDiagnostic(
            dispatchGroups.X,
            limits.MaxDispatchGroups.X,
            ComputeDispatchDiagnosticCode.DispatchGroupCountExceeded,
            "X",
            "Dispatch group count",
            diagnostics);
        AddExceededDiagnostic(
            dispatchGroups.Y,
            limits.MaxDispatchGroups.Y,
            ComputeDispatchDiagnosticCode.DispatchGroupCountExceeded,
            "Y",
            "Dispatch group count",
            diagnostics);
        AddExceededDiagnostic(
            dispatchGroups.Z,
            limits.MaxDispatchGroups.Z,
            ComputeDispatchDiagnosticCode.DispatchGroupCountExceeded,
            "Z",
            "Dispatch group count",
            diagnostics);
    }

    private static void AddExceededDiagnostic(
        long value,
        long limit,
        ComputeDispatchDiagnosticCode code,
        string dimension,
        string label,
        List<ComputeDispatchDiagnostic> diagnostics)
    {
        if (value <= limit)
            return;

        diagnostics.Add(new ComputeDispatchDiagnostic(
            code,
            dimension,
            value,
            limit,
            $"{label} {dimension} is {value}, but the limit is {limit}."));
    }
}
