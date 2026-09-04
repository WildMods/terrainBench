namespace terrainBench;

public class AtomicCounter(int v) {
    public int val = v;

    public void Increment()
    {
        Interlocked.Increment(ref val);
    }
    
    public static implicit operator int(AtomicCounter v) => v.val;
    public static implicit operator AtomicCounter(int v) => new(v);
}
