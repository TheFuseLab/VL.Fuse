namespace Fuse.IO.Ply;

#pragma warning disable CS1591

/// <summary>
/// Unified progress tracking for combined PLY loading and Octree building operations.
/// Provides weighted progress across multiple stages for a smooth 0-100% experience.
/// </summary>
public class CombinedProgressInfo
{
    /// <summary>
    /// Processing stages with their progress weight ranges.
    /// </summary>
    public enum ProcessStage
    {
        Idle = 0,
        LoadingPly = 1,      // 0-40% of total
        BuildingOctree = 2,  // 40-80% of total
        GeneratingLODs = 3,  // 80-100% of total
        Complete = 4
    }

    /// <summary>
    /// Stage weight configuration: (StartPercent, EndPercent)
    /// </summary>
    private static readonly Dictionary<ProcessStage, (float Start, float End)> StageWeights = new()
    {
        { ProcessStage.LoadingPly, (0f, 40f) },
        { ProcessStage.BuildingOctree, (40f, 80f) },
        { ProcessStage.GeneratingLODs, (80f, 100f) }
    };

    // Current state
    public ProcessStage CurrentStage { get; set; } = ProcessStage.Idle;
    public string StageName { get; set; } = string.Empty;
    public string DetailedStatus { get; set; } = string.Empty;

    /// <summary>
    /// Progress within the current stage (0-100).
    /// </summary>
    public double StageProgress { get; set; }

    /// <summary>
    /// Overall progress across all stages (0-100).
    /// </summary>
    public double OverallProgress { get; set; }

    // Stage-specific metrics
    public int VerticesLoaded { get; set; }
    public int TotalVertices { get; set; }
    public int NodesProcessed { get; set; }
    public int TotalNodes { get; set; }
    public long BytesProcessed { get; set; }
    public long TotalBytes { get; set; }

    // Completion/error state
    public bool IsCompleted { get; set; }
    public Exception? Error { get; set; }

    // Timing
    public TimeSpan Elapsed { get; set; }
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Updates the overall progress based on current stage and stage progress.
    /// Call this after modifying StageProgress.
    /// </summary>
    public void UpdateOverallProgress()
    {
        if (!StageWeights.TryGetValue(CurrentStage, out var weights))
        {
            OverallProgress = CurrentStage == ProcessStage.Complete ? 100.0 : 0.0;
            return;
        }

        var stageRange = weights.End - weights.Start;
        var stageContribution = (StageProgress / 100.0) * stageRange;
        OverallProgress = weights.Start + stageContribution;
    }

    /// <summary>
    /// Advances to the next stage, resetting stage progress.
    /// </summary>
    public void AdvanceStage(ProcessStage newStage, string stageName)
    {
        CurrentStage = newStage;
        StageName = stageName;
        StageProgress = 0;
        UpdateOverallProgress();
    }

    /// <summary>
    /// Updates from FastPlyReader.ProgressInfo, mapping its progress to the LoadingPly stage.
    /// </summary>
    public void UpdateFromPlyProgress(FastPlyReader.ProgressInfo plyProgress)
    {
        if (plyProgress == null) return;

        CurrentStage = ProcessStage.LoadingPly;
        StageName = "Loading PLY";
        DetailedStatus = plyProgress.StageName ?? "Reading file";
        StageProgress = plyProgress.ProgressPercentage;
        VerticesLoaded = plyProgress.VerticesProcessed;
        TotalVertices = plyProgress.TotalVertices;
        BytesProcessed = plyProgress.BytesProcessed;
        TotalBytes = plyProgress.TotalBytes;
        Elapsed = plyProgress.Elapsed;
        UpdateOverallProgress();
    }

    /// <summary>
    /// Updates from GPUOctree.OctreeProgressInfo, mapping to BuildingOctree or GeneratingLODs stage.
    /// </summary>
    public void UpdateFromOctreeProgress(GPUOctree.OctreeProgressInfo octreeProgress, bool isLodPhase = false)
    {
        if (octreeProgress == null) return;

        CurrentStage = isLodPhase ? ProcessStage.GeneratingLODs : ProcessStage.BuildingOctree;
        StageName = isLodPhase ? "Generating LODs" : "Building Octree";
        DetailedStatus = octreeProgress.StageName ?? string.Empty;
        StageProgress = octreeProgress.ProgressPercentage;
        NodesProcessed = octreeProgress.NodesProcessed;
        TotalNodes = octreeProgress.TotalNodes;
        UpdateOverallProgress();
    }

    /// <summary>
    /// Marks the operation as complete.
    /// </summary>
    public void MarkComplete()
    {
        CurrentStage = ProcessStage.Complete;
        StageName = "Complete";
        StageProgress = 100;
        OverallProgress = 100;
        IsCompleted = true;
    }

    /// <summary>
    /// Marks the operation as failed with an error.
    /// </summary>
    public void MarkError(Exception ex)
    {
        Error = ex;
        IsCompleted = true;
        StageName = $"Error: {ex.Message}";
    }
}
