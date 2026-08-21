// Taken from:
// https://github.com/clibequilibrium/Tracy-CSharp/blob/main/src/cs/samples/HelloWorld/ProfilerZone.cs
using static Tracy.PInvoke;

public readonly struct ProfilerZone : IDisposable
{
    public readonly TracyCZoneCtx Context;

    public uint Id => Context.Data.Id;

    public int Active => Context.Data.Active;

    internal ProfilerZone(TracyCZoneCtx context)
    {
        Context = context;
    }

    public void EmitName(string name)
    {
        if (Profiler.tracyDisabled) return;
        using var namestr = Profiler.GetCString(name, out var nameln);
        TracyEmitZoneName(Context, namestr, nameln);
    }

    public void EmitColor(uint color)
    {
        if (Profiler.tracyDisabled) return;
        TracyEmitZoneColor(Context, color);
    }

    public void EmitText(string text)
    {
        if (Profiler.tracyDisabled) return;
        using var textstr = Profiler.GetCString(text, out var textln);
        TracyEmitZoneText(Context, textstr, textln);
    }
    
    public void EmitValue(ulong val)
    {
        if (Profiler.tracyDisabled) return;
        TracyEmitZoneValue(Context, val);
    }

    public void Dispose()
    {
        if (Profiler.tracyDisabled) return;
        TracyEmitZoneEnd(Context);
    }
}
