// Taken from https://github.com/Marco2011T2/AvaloniaOpenTK/blob/main/AvaloniaOpenTK/OpenTK/AvaloniaTkContext.cs
using Avalonia.OpenGL;
using OpenTK;

namespace terrainBench.UI;

/// <summary>
/// Wrapper to expose GetProcAddress from Avalonia in a manner that OpenTK can consume. 
/// </summary>
internal class AvaloniaTkContext(GlInterface glInterface) : IBindingsContext {
    public nint GetProcAddress(string procName) => glInterface.GetProcAddress(procName);
}