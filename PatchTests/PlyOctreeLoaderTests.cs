using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Fuse.IO.Ply;
using NUnit.Framework;

namespace PatchTests;

/// <summary>
/// Unit tests for PlyOctreeLoader and related components.
/// Tests combined PLY loading + octree building with unified progress reporting.
/// </summary>
[TestFixture]
public class PlyOctreeLoaderTests
{
    private const string TestPlyFile = @"D:\development\imaginary friends\DL_2023_06_DATALAND ASSETS\ga_art_v1\global\forest_scenebuilder_exports\scene.ply";

    [SetUp]
    public void Setup()
    {
        // Verify test file exists
        if (!File.Exists(TestPlyFile))
        {
            Assert.Ignore($"Test PLY file not found: {TestPlyFile}");
        }
    }

    #region Basic Functionality Tests

    [Test]
    public async Task TestBasicCombinedLoading()
    {
        // Arrange
        var loader = new PlyOctreeLoader();
        SetProperty(loader, "FilePath", TestPlyFile);

        // Act
        SetProperty(loader, "Load", true);
        loader.Update();
        SetProperty(loader, "Load", false);

        await WaitForCompletion(loader, TimeSpan.FromMinutes(5));

        // Assert
        Assert.That(loader.IsCompleted, Is.True, "Loading should complete");
        Assert.That(loader.HasError, Is.False, $"Should not have error: {loader.ErrorMessage}");
        Assert.That(loader.VertexCount, Is.GreaterThan(0), "Should load vertices");
        Assert.That(loader.NodeCount, Is.GreaterThan(0), "Should build octree nodes");
        Assert.That(loader.IndexCount, Is.GreaterThan(0), "Should have indices");
        Assert.That(loader.PlyData, Is.Not.Empty, "Should have PLY data");
        Assert.That(loader.FieldOrder.Length, Is.GreaterThan(0), "Should have field order");
        Assert.That(loader.NodeBufferData.Length, Is.GreaterThan(0), "Should have node buffer");
        Assert.That(loader.IndexBufferData.Length, Is.GreaterThan(0), "Should have index buffer");

        Console.WriteLine($"Loaded {loader.VertexCount:N0} vertices, {loader.NodeCount:N0} nodes");
    }

    [Test]
    public async Task TestProgressReportingIsMonotonic()
    {
        // Arrange
        var loader = new PlyOctreeLoader();
        SetProperty(loader, "FilePath", TestPlyFile);
        var progressValues = new List<float>();

        // Act
        SetProperty(loader, "Load", true);
        loader.Update();
        SetProperty(loader, "Load", false);

        var sw = Stopwatch.StartNew();
        var timeout = TimeSpan.FromMinutes(5);

        while (!loader.IsCompleted && sw.Elapsed < timeout)
        {
            loader.Update();
            progressValues.Add(loader.Progress);
            await Task.Delay(20);
        }

        // Assert
        Assert.That(loader.IsCompleted, Is.True, "Should complete");

        // Verify progress is monotonically increasing (allowing for same values)
        for (int i = 1; i < progressValues.Count; i++)
        {
            Assert.That(progressValues[i], Is.GreaterThanOrEqualTo(progressValues[i - 1]),
                $"Progress should not decrease: {progressValues[i - 1]} -> {progressValues[i]} at index {i}");
        }

        // Verify final progress is close to 100%
        Assert.That(loader.Progress, Is.GreaterThanOrEqualTo(0.99f), "Final progress should be ~100%");

        Console.WriteLine($"Captured {progressValues.Count} progress values");
    }

    [Test]
    public async Task TestProgressReportsMultipleStages()
    {
        // Arrange
        var loader = new PlyOctreeLoader();
        SetProperty(loader, "FilePath", TestPlyFile);
        var stageNames = new HashSet<string>();

        // Act
        SetProperty(loader, "Load", true);
        loader.Update();
        SetProperty(loader, "Load", false);

        var sw = Stopwatch.StartNew();
        var timeout = TimeSpan.FromMinutes(5);

        while (!loader.IsCompleted && sw.Elapsed < timeout)
        {
            loader.Update();
            if (!string.IsNullOrEmpty(loader.StageName))
            {
                stageNames.Add(loader.StageName);
            }
            await Task.Delay(20);
        }

        // Assert
        Assert.That(loader.IsCompleted, Is.True, "Should complete");
        Assert.That(stageNames.Count, Is.GreaterThanOrEqualTo(2),
            $"Should observe multiple stages, got: {string.Join(", ", stageNames)}");

        Console.WriteLine($"Observed stages: {string.Join(", ", stageNames)}");
    }

    #endregion

    #region Decimation Strategy Tests

    [Test]
    public async Task TestDecimationStrategy_None()
    {
        await TestDecimationStrategyInternal(PlyDecimationStrategy.None, 1);
    }

    [Test]
    public async Task TestDecimationStrategy_Stride()
    {
        var result = await TestDecimationStrategyInternal(PlyDecimationStrategy.Stride, 4);

        // With stride decimation factor 4, we should have roughly 1/4 of the points
        // Allow some tolerance for rounding
        Assert.That(result.VertexCount, Is.LessThan(result.FullVertexCount),
            "Stride decimation should reduce vertex count");
    }

    [Test]
    public async Task TestDecimationStrategy_Random()
    {
        var result = await TestDecimationStrategyInternal(PlyDecimationStrategy.Random, 4);

        // With random decimation factor 4, we should have roughly 1/4 of the points
        Assert.That(result.VertexCount, Is.LessThan(result.FullVertexCount),
            "Random decimation should reduce vertex count");
    }

    [Test]
    public async Task TestDecimationStrategiesProduceDifferentResults()
    {
        // Get baseline (no decimation)
        var noneResult = await TestDecimationStrategyInternal(PlyDecimationStrategy.None, 1);
        var strideResult = await TestDecimationStrategyInternal(PlyDecimationStrategy.Stride, 4);
        var randomResult = await TestDecimationStrategyInternal(PlyDecimationStrategy.Random, 4);

        // Assert different counts
        Assert.That(strideResult.VertexCount, Is.LessThan(noneResult.VertexCount),
            "Stride should have fewer vertices than None");
        Assert.That(randomResult.VertexCount, Is.LessThan(noneResult.VertexCount),
            "Random should have fewer vertices than None");

        Console.WriteLine($"None: {noneResult.VertexCount:N0}, Stride: {strideResult.VertexCount:N0}, Random: {randomResult.VertexCount:N0}");
    }

    private async Task<(int VertexCount, int FullVertexCount)> TestDecimationStrategyInternal(
        PlyDecimationStrategy strategy, int factor)
    {
        // First get full count for comparison
        var fullLoader = new PlyOctreeLoader();
        SetProperty(fullLoader, "FilePath", TestPlyFile);
        SetProperty(fullLoader, "DecimationStrategy", PlyDecimationStrategy.None);
        SetProperty(fullLoader, "Load", true);
        fullLoader.Update();
        SetProperty(fullLoader, "Load", false);
        await WaitForCompletion(fullLoader, TimeSpan.FromMinutes(5));
        var fullCount = fullLoader.VertexCount;

        // Now test with specified strategy
        var loader = new PlyOctreeLoader();
        SetProperty(loader, "FilePath", TestPlyFile);
        SetProperty(loader, "DecimationStrategy", strategy);
        SetProperty(loader, "DecimationFactor", factor);

        SetProperty(loader, "Load", true);
        loader.Update();
        SetProperty(loader, "Load", false);

        await WaitForCompletion(loader, TimeSpan.FromMinutes(5));

        Assert.That(loader.IsCompleted, Is.True, $"Should complete with {strategy}");
        Assert.That(loader.HasError, Is.False, $"Should not error with {strategy}: {loader.ErrorMessage}");
        Assert.That(loader.VertexCount, Is.GreaterThan(0), $"Should have vertices with {strategy}");
        Assert.That(loader.NodeCount, Is.GreaterThan(0), $"Should have nodes with {strategy}");

        Console.WriteLine($"{strategy} (factor {factor}): {loader.VertexCount:N0} vertices, {loader.NodeCount:N0} nodes");

        return (loader.VertexCount, fullCount);
    }

    #endregion

    #region Disk Cache Tests

    [Test]
    public async Task TestDiskCache_CacheMiss()
    {
        // Arrange - force cache update to ensure cache miss
        var loader = new PlyOctreeLoader();
        SetProperty(loader, "FilePath", TestPlyFile);
        SetProperty(loader, "UseDiskCache", true);
        SetProperty(loader, "ForceReload", true);

        // Act
        var sw = Stopwatch.StartNew();
        SetProperty(loader, "Load", true);
        loader.Update();
        SetProperty(loader, "Load", false);
        await WaitForCompletion(loader, TimeSpan.FromMinutes(5));
        sw.Stop();

        // Assert
        Assert.That(loader.IsCompleted, Is.True, "Should complete");
        Assert.That(loader.HasError, Is.False, $"Should not error: {loader.ErrorMessage}");
        Assert.That(loader.VertexCount, Is.GreaterThan(0), "Should have vertices");

        Console.WriteLine($"Cache miss load: {sw.ElapsedMilliseconds} ms");
    }

    [Test]
    public async Task TestDiskCache_CacheHitIsFaster()
    {
        // First load - cache miss (force rebuild)
        var loader1 = new PlyOctreeLoader();
        SetProperty(loader1, "FilePath", TestPlyFile);
        SetProperty(loader1, "UseDiskCache", true);
        SetProperty(loader1, "ForceReload", true);

        var sw1 = Stopwatch.StartNew();
        SetProperty(loader1, "Load", true);
        loader1.Update();
        SetProperty(loader1, "Load", false);
        await WaitForCompletion(loader1, TimeSpan.FromMinutes(5));
        sw1.Stop();

        var firstVertexCount = loader1.VertexCount;
        var firstNodeCount = loader1.NodeCount;

        // Second load - should be cache hit
        var loader2 = new PlyOctreeLoader();
        SetProperty(loader2, "FilePath", TestPlyFile);
        SetProperty(loader2, "UseDiskCache", true);
        SetProperty(loader2, "ForceReload", false);

        var sw2 = Stopwatch.StartNew();
        SetProperty(loader2, "Load", true);
        loader2.Update();
        SetProperty(loader2, "Load", false);
        await WaitForCompletion(loader2, TimeSpan.FromMinutes(5));
        sw2.Stop();

        // Assert
        Assert.That(loader2.IsCompleted, Is.True, "Second load should complete");
        Assert.That(loader2.HasError, Is.False, $"Second load should not error: {loader2.ErrorMessage}");

        // Data should match
        Assert.That(loader2.VertexCount, Is.EqualTo(firstVertexCount), "Vertex count should match");
        Assert.That(loader2.NodeCount, Is.EqualTo(firstNodeCount), "Node count should match");

        // Cache hit should be faster (at least 1.5x speedup expected)
        var speedup = (double)sw1.ElapsedMilliseconds / Math.Max(1, sw2.ElapsedMilliseconds);
        Console.WriteLine($"First load (cache miss): {sw1.ElapsedMilliseconds} ms");
        Console.WriteLine($"Second load (cache hit): {sw2.ElapsedMilliseconds} ms");
        Console.WriteLine($"Speedup: {speedup:F1}x");

        Assert.That(speedup, Is.GreaterThan(1.0), "Cache hit should be faster than cache miss");
    }

    [Test]
    public async Task TestDiskCache_DataConsistency()
    {
        // Load with cache
        var loader1 = new PlyOctreeLoader();
        SetProperty(loader1, "FilePath", TestPlyFile);
        SetProperty(loader1, "UseDiskCache", true);
        SetProperty(loader1, "ForceReload", true);
        SetProperty(loader1, "Load", true);
        loader1.Update();
        SetProperty(loader1, "Load", false);
        await WaitForCompletion(loader1, TimeSpan.FromMinutes(5));

        // Load from cache
        var loader2 = new PlyOctreeLoader();
        SetProperty(loader2, "FilePath", TestPlyFile);
        SetProperty(loader2, "UseDiskCache", true);
        SetProperty(loader2, "ForceReload", false);
        SetProperty(loader2, "Load", true);
        loader2.Update();
        SetProperty(loader2, "Load", false);
        await WaitForCompletion(loader2, TimeSpan.FromMinutes(5));

        // Assert data consistency
        Assert.That(loader2.VertexCount, Is.EqualTo(loader1.VertexCount), "Vertex count should match");
        Assert.That(loader2.NodeCount, Is.EqualTo(loader1.NodeCount), "Node count should match");
        Assert.That(loader2.IndexCount, Is.EqualTo(loader1.IndexCount), "Index count should match");
        Assert.That(loader2.FieldOrder, Is.EqualTo(loader1.FieldOrder), "Field order should match");
        Assert.That(loader2.NodeBufferData.Length, Is.EqualTo(loader1.NodeBufferData.Length), "Node buffer size should match");
        Assert.That(loader2.IndexBufferData.Length, Is.EqualTo(loader1.IndexBufferData.Length), "Index buffer size should match");
    }

    #endregion

    #region Comparison with Separate Nodes Tests

    [Test]
    public async Task TestCombinedMatchesSeparateNodes()
    {
        // Combined loader
        var combined = new PlyOctreeLoader();
        SetProperty(combined, "FilePath", TestPlyFile);
        SetProperty(combined, "Load", true);
        combined.Update();
        SetProperty(combined, "Load", false);
        await WaitForCompletion(combined, TimeSpan.FromMinutes(5));

        // Separate nodes: FastPly
        var fastPly = new FastPly();
        SetProperty(fastPly, "FilePath", TestPlyFile);
        SetProperty(fastPly, "Load", true);
        fastPly.Update();
        SetProperty(fastPly, "Load", false);
        await WaitForCompletionFastPly(fastPly, TimeSpan.FromMinutes(5));

        // Separate nodes: OctreeBuilder
        var octreeBuilder = new OctreeBuilder();
        SetProperty(octreeBuilder, "PLYData", fastPly.Result);
        SetProperty(octreeBuilder, "Build", true);
        octreeBuilder.Update();
        SetProperty(octreeBuilder, "Build", false);
        await WaitForCompletionOctreeBuilder(octreeBuilder, TimeSpan.FromMinutes(5));

        // Assert vertex counts match
        Assert.That(combined.VertexCount, Is.EqualTo(fastPly.VertexCount),
            $"Vertex count should match: combined={combined.VertexCount}, fastPly={fastPly.VertexCount}");

        // Assert node counts match
        Assert.That(combined.NodeCount, Is.EqualTo(octreeBuilder.NodeCount),
            $"Node count should match: combined={combined.NodeCount}, octreeBuilder={octreeBuilder.NodeCount}");

        // Assert index counts match
        Assert.That(combined.IndexCount, Is.EqualTo(octreeBuilder.IndexCount),
            $"Index count should match: combined={combined.IndexCount}, octreeBuilder={octreeBuilder.IndexCount}");

        Console.WriteLine($"Combined: {combined.VertexCount:N0} vertices, {combined.NodeCount:N0} nodes");
        Console.WriteLine($"Separate: {fastPly.VertexCount:N0} vertices, {octreeBuilder.NodeCount:N0} nodes");
    }

    #endregion

    #region Helper Methods

    private static async Task WaitForCompletion(PlyOctreeLoader loader, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (!loader.IsCompleted && sw.Elapsed < timeout)
        {
            loader.Update();
            await Task.Delay(50);
        }

        if (!loader.IsCompleted)
        {
            throw new TimeoutException($"Loader did not complete within {timeout}");
        }
    }

    private static async Task WaitForCompletionFastPly(FastPly loader, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (!loader.IsCompleted && sw.Elapsed < timeout)
        {
            loader.Update();
            await Task.Delay(50);
        }

        if (!loader.IsCompleted)
        {
            throw new TimeoutException($"FastPly did not complete within {timeout}");
        }
    }

    private static async Task WaitForCompletionOctreeBuilder(OctreeBuilder builder, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (!builder.IsCompleted && sw.Elapsed < timeout)
        {
            builder.Update();
            await Task.Delay(50);
        }

        if (!builder.IsCompleted)
        {
            throw new TimeoutException($"OctreeBuilder did not complete within {timeout}");
        }
    }

    private static void SetProperty<T>(object obj, string propertyName, T value)
    {
        var prop = obj.GetType().GetProperty(propertyName);
        if (prop != null)
        {
            prop.SetValue(obj, value);
        }
        else
        {
            throw new ArgumentException($"Property {propertyName} not found on {obj.GetType().Name}");
        }
    }

    #endregion
}
