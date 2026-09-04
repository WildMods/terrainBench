namespace terrainBench.Core.LodComponents;

public readonly struct GrassExtm
{
    public readonly byte Height;
    public readonly byte Red;
    public readonly byte Green;
    public readonly byte Blue;

    public GrassExtm(byte height, byte r, byte g, byte b)
    {
        Height = height;
        Red = r;
        Green = g;
        Blue = b;
    }
}
