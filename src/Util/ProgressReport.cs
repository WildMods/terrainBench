namespace terrainBench;

/// <summary>
/// Standalone wrapper for Avalonia progress bar
/// </summary>
public class ProgressReport {
    private int _value = 0;
    public int Value { get => _value; set => _value = value; }
    public int Min { get; set; } = 0;

    public int Max {
        get;
        set {
            IsIndeterminate = false;
            field = value;
        }
    }

    public float Percent {
        get {
            var range = Max - Min;
            return (Value - Min) / (float)range;
        }    
    }

    /// <summary>
    /// If this is set, the end point of the progress bar is unknown
    /// </summary>
    public bool IsIndeterminate { get; set; } = true;
    public void Increment() => Interlocked.Increment(ref _value);
}