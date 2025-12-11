using System.Runtime.CompilerServices;

namespace Fuse.IO.Ply;

#pragma warning disable CS1591

/// <summary>
/// Utility methods for working with PLY data layouts (SoA and AoS).
/// Provides conversion and validation helpers.
/// </summary>
public static class PlyLayoutUtils
{
    /// <summary>
    /// Converts an interleaved AoS array back to SoA dictionary format.
    /// Useful for testing equivalence between FastPly and FastPlyInterleaved outputs.
    /// </summary>
    /// <param name="interleaved">The interleaved float array from FastPlyInterleaved</param>
    /// <param name="vertexCount">Number of vertices</param>
    /// <param name="fieldOrder">Field names in order</param>
    /// <returns>Dictionary mapping field names to float arrays (SoA format)</returns>
    public static Dictionary<string, float[]> InterleavedToSoA(
        float[] interleaved, 
        int vertexCount, 
        string[] fieldOrder)
    {
        if (interleaved.Length == 0 || fieldOrder.Length == 0)
            return new Dictionary<string, float[]>(0);

        var fieldsPerVertex = fieldOrder.Length;
        
        if (interleaved.Length != vertexCount * fieldsPerVertex)
            throw new ArgumentException(
                $"Interleaved array length {interleaved.Length} doesn't match expected " +
                $"{vertexCount} vertices * {fieldsPerVertex} fields = {vertexCount * fieldsPerVertex}");

        var result = new Dictionary<string, float[]>(fieldsPerVertex);
        
        for (var f = 0; f < fieldsPerVertex; f++)
        {
            var fieldArray = new float[vertexCount];
            for (var v = 0; v < vertexCount; v++)
            {
                fieldArray[v] = interleaved[v * fieldsPerVertex + f];
            }
            result[fieldOrder[f]] = fieldArray;
        }

        return result;
    }

    /// <summary>
    /// Converts SoA dictionary format to an interleaved AoS array.
    /// Same as the internal conversion in FastPlyInterleaved, exposed for utility use.
    /// </summary>
    /// <param name="soaArrays">Dictionary of field arrays</param>
    /// <param name="fieldOrder">Order of fields in the interleaved output</param>
    /// <returns>Tuple of (interleaved array, vertex count, fields per vertex)</returns>
    public static (float[] interleaved, int vertexCount, int fieldsPerVertex) SoAToInterleaved(
        Dictionary<string, float[]> soaArrays,
        string[] fieldOrder)
    {
        if (soaArrays.Count == 0 || fieldOrder.Length == 0)
            return (Array.Empty<float>(), 0, 0);

        var fieldsPerVertex = fieldOrder.Length;
        
        // Pre-resolve field arrays
        var fieldArrays = new float[fieldsPerVertex][];
        for (var f = 0; f < fieldsPerVertex; f++)
        {
            if (!soaArrays.TryGetValue(fieldOrder[f], out var arr))
                throw new InvalidOperationException($"Field '{fieldOrder[f]}' not found in SoA arrays");
            fieldArrays[f] = arr;
        }

        // Validate lengths
        var vertexCount = fieldArrays[0].Length;
        for (var f = 1; f < fieldsPerVertex; f++)
        {
            if (fieldArrays[f].Length != vertexCount)
                throw new InvalidOperationException(
                    $"Field '{fieldOrder[f]}' has {fieldArrays[f].Length} elements, expected {vertexCount}");
        }

        // Allocate and fill
        var interleaved = new float[vertexCount * fieldsPerVertex];
        var writeIdx = 0;
        for (var v = 0; v < vertexCount; v++)
        {
            for (var f = 0; f < fieldsPerVertex; f++)
            {
                interleaved[writeIdx++] = fieldArrays[f][v];
            }
        }

        return (interleaved, vertexCount, fieldsPerVertex);
    }

    /// <summary>
    /// Verifies that two SoA dictionaries contain equivalent data.
    /// Compares field names, vertex counts, and actual values.
    /// </summary>
    /// <param name="a">First SoA dictionary</param>
    /// <param name="b">Second SoA dictionary</param>
    /// <param name="tolerance">Maximum allowed difference for float comparison</param>
    /// <returns>Tuple of (areEqual, errorMessage)</returns>
    public static (bool areEqual, string errorMessage) VerifySoAEquivalence(
        Dictionary<string, float[]> a,
        Dictionary<string, float[]> b,
        float tolerance = 1e-6f)
    {
        if (a.Count != b.Count)
            return (false, $"Field count mismatch: {a.Count} vs {b.Count}");

        foreach (var kvA in a)
        {
            if (!b.TryGetValue(kvA.Key, out var arrB))
                return (false, $"Field '{kvA.Key}' missing in second dictionary");

            var arrA = kvA.Value;
            if (arrA.Length != arrB.Length)
                return (false, $"Field '{kvA.Key}' length mismatch: {arrA.Length} vs {arrB.Length}");

            for (var i = 0; i < arrA.Length; i++)
            {
                var diff = MathF.Abs(arrA[i] - arrB[i]);
                if (diff > tolerance)
                    return (false, $"Field '{kvA.Key}' value mismatch at index {i}: {arrA[i]} vs {arrB[i]} (diff={diff})");
            }
        }

        return (true, string.Empty);
    }

    /// <summary>
    /// Verifies that SoA and AoS data are equivalent by converting AoS back to SoA
    /// and comparing field by field.
    /// </summary>
    /// <param name="soaResult">SoA dictionary from FastPly</param>
    /// <param name="aosInterleaved">Interleaved array from FastPlyInterleaved</param>
    /// <param name="vertexCount">Vertex count from FastPlyInterleaved</param>
    /// <param name="fieldOrder">Field order from FastPlyInterleaved</param>
    /// <param name="tolerance">Maximum allowed difference for float comparison</param>
    /// <returns>Tuple of (areEqual, errorMessage)</returns>
    public static (bool areEqual, string errorMessage) VerifySoAAoSEquivalence(
        Dictionary<string, float[]> soaResult,
        float[] aosInterleaved,
        int vertexCount,
        string[] fieldOrder,
        float tolerance = 1e-6f)
    {
        // Convert AoS back to SoA
        var convertedSoA = InterleavedToSoA(aosInterleaved, vertexCount, fieldOrder);
        
        // Compare the converted result with the original SoA
        return VerifySoAEquivalence(soaResult, convertedSoA, tolerance);
    }

    /// <summary>
    /// Gets a summary of the data layout for debugging purposes.
    /// </summary>
    public static string GetLayoutSummary(Dictionary<string, float[]> soaArrays, string[] fieldOrder)
    {
        if (soaArrays.Count == 0)
            return "Empty SoA data";

        var vertexCount = soaArrays.Values.First().Length;
        var fieldNames = string.Join(", ", fieldOrder);
        var totalBytes = soaArrays.Values.Sum(arr => arr.LongLength * sizeof(float));
        
        return $"SoA: {vertexCount:N0} vertices, {fieldOrder.Length} fields [{fieldNames}], {totalBytes / (1024.0 * 1024.0):F2} MB";
    }

    /// <summary>
    /// Gets a summary of the interleaved data layout for debugging purposes.
    /// </summary>
    public static string GetInterleavedSummary(float[] interleaved, int vertexCount, int fieldsPerVertex, string[] fieldOrder)
    {
        if (interleaved.Length == 0)
            return "Empty interleaved data";

        var fieldNames = string.Join(", ", fieldOrder);
        var totalBytes = interleaved.LongLength * sizeof(float);
        
        return $"AoS: {vertexCount:N0} vertices, {fieldsPerVertex} fields [{fieldNames}], {totalBytes / (1024.0 * 1024.0):F2} MB";
    }
}


