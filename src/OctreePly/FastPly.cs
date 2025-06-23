namespace VL.E57;

// In VVVV, create a new C# node
[ProcessNode]
public class FastPly 
{
    private FastPlyReader.ProgressInfo _progressInfo;
    private Task _loadingTask;
    private bool _isLoading;

    public string FilePath { get; set; }
    public bool Load { get; set; }
    
    // Output pins
    public Dictionary<string, float[]> Result { get; private set; }
    public float Progress { get; private set; }
    public string Status { get; private set; }
    public bool IsCompleted { get; private set; }
    public bool HasError { get; private set; }
    public string ErrorMessage { get; private set; }

    public void Update()
    {
        // Start loading when Load is triggered
        if (Load && !_isLoading && !string.IsNullOrEmpty(FilePath))
        {
            StartLoading();
        }

        // Update outputs with current progress
        if (_progressInfo != null)
        {
            Progress = (float)_progressInfo.ProgressPercentage;
            Status = _progressInfo.StageName;
            IsCompleted = _progressInfo.IsCompleted;
            HasError = _progressInfo.Error != null;
            ErrorMessage = _progressInfo.Error?.Message;

            if (_progressInfo.IsCompleted && _progressInfo.Result != null)
            {
                Result = _progressInfo.Result;
                _isLoading = false;
            }
        }
    }

    private async void StartLoading()
    {
        _isLoading = true;
        _progressInfo = new FastPlyReader.ProgressInfo();
        
        try
        {
            _loadingTask = FastPlyReader.LoadInBackgroundAsync(FilePath, _progressInfo);
            await _loadingTask;
        }
        catch (Exception ex)
        {
            _progressInfo.Error = ex;
            _progressInfo.IsCompleted = true;
        }
    }
}