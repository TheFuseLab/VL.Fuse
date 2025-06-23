namespace VL.E57;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VL.Core;

// VVVV Process Node for GPU Octree Building
[ProcessNode]
public class OctreeBuilder 
{
    private GPUOctree.OctreeProgressInfo _progressInfo;
    private Task _buildTask;
    private bool _isBuilding;
    private bool _lastBuildTrigger;

    // Input Pins
    public Dictionary<string, float[]> PLYData { private get; set; }
    public bool Build { private get; set; }
    public int MaxDepth { private get; set; } = 8;
    public int MaxPointsPerLeaf { private get; set; } = 5000;

    // Output Pins - Progress Information
    public float Progress { get; private set; }
    public string Status { get; private set; } = "Ready";
    public string StageName { get; private set; } = "";
    public int TotalPoints { get; private set; }
    public bool IsBuilding { get; private set; }
    public bool IsCompleted { get; private set; }
    public bool HasError { get; private set; }
    public string ErrorMessage { get; private set; } = "";

    // Output Pins - Results
    public byte[] NodeBufferData { get; private set; }
    public byte[] IndexBufferData { get; private set; }
    public int NodeCount { get; private set; }
    public int IndexCount { get; private set; }
    public int NodesProcessed { get; private set; }
    public long TotalMemoryUsed { get; private set; }

    // Output Pins - Statistics
    public float CompressionRatio { get; private set; }
    public float NodeBufferSizeMB { get; private set; }
    public float IndexBufferSizeMB { get; private set; }
    public float TotalSizeMB { get; private set; }

    public void Update()
    {
        // Detect rising edge on Build trigger
        bool buildRisingEdge = Build && !_lastBuildTrigger;
        _lastBuildTrigger = Build;

        // Start building when triggered
        if (buildRisingEdge && !_isBuilding && PLYData != null && PLYData.Count > 0)
        {
            StartBuilding();
        }

        // Update outputs with current progress
        UpdateOutputs();

        // Check if building completed
        if (_isBuilding && _progressInfo != null && _progressInfo.IsCompleted)
        {
            _isBuilding = false;
            
            if (_progressInfo.Error == null)
            {
                // Update result outputs
                NodeBufferData = _progressInfo.NodeBufferData;
                IndexBufferData = _progressInfo.IndexBufferData;
                NodeCount = _progressInfo.NodeCount;
                IndexCount = _progressInfo.IndexCount;
                TotalMemoryUsed = _progressInfo.TotalMemoryUsed;
                NodesProcessed =_progressInfo.NodesProcessed;
                
                // Calculate statistics
                CalculateStatistics();
            }
        }
    }

    private async void StartBuilding()
    {
        _isBuilding = true;
        _progressInfo = new GPUOctree.OctreeProgressInfo();
        
        // Clear previous results
        NodeBufferData = null;
        IndexBufferData = null;
        NodeCount = 0;
        IndexCount = 0;
        TotalMemoryUsed = 0;
        HasError = false;
        ErrorMessage = "";
        
        // Create build configuration from inputs
        var config = new GPUOctree.BuildConfig
        {
            MaxDepth = MaxDepth,
            MaxPointsPerLeaf = MaxPointsPerLeaf
        };
        
        try
        {
            // Start building in background
            _buildTask = GPUOctree.BuildInBackgroundAsync(PLYData, _progressInfo, config);
            await _buildTask;
        }
        catch (Exception ex)
        {
            if (_progressInfo != null)
            {
                _progressInfo.Error = ex;
                _progressInfo.IsCompleted = true;
                _progressInfo.StageName = "Error";
            }
        }
    }

    private void UpdateOutputs()
    {
        if (_progressInfo != null)
        {
            // Progress outputs
            Progress = (float)_progressInfo.ProgressPercentage / 100.0f;
            Status = _isBuilding ? "Building" : _progressInfo.IsCompleted ? "Complete" : "Ready";
            StageName = _progressInfo.StageName ?? "";
            TotalPoints = _progressInfo.TotalPoints;
            IsCompleted = _progressInfo.IsCompleted;
            HasError = _progressInfo.Error != null;
            ErrorMessage = _progressInfo.Error?.Message ?? "";
        }

        IsBuilding = _isBuilding;
    }

    private void CalculateStatistics()
    {
        if (_progressInfo != null && TotalPoints > 0)
        {
            CompressionRatio = (float)IndexCount / TotalPoints;
            NodeBufferSizeMB = (NodeBufferData?.Length ?? 0) / (1024f * 1024f);
            IndexBufferSizeMB = (IndexBufferData?.Length ?? 0) / (1024f * 1024f);
            TotalSizeMB = NodeBufferSizeMB + IndexBufferSizeMB;
        }
    }
}