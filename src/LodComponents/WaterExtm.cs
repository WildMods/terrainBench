namespace terrainBench.LodComponents;

public struct WaterExtm
{
    public readonly ushort Height;
    public readonly ushort XAxisFlowRate;
    public readonly ushort ZAxisFlowRate;
    private readonly byte _materialIndexPlusThree;
    public readonly byte MaterialIndex;

    public WaterExtm(ushort height, ushort xAxisFlowRate, ushort zAxisFlowRate, byte materialIndex)
    {
        Height  = height;
        XAxisFlowRate = xAxisFlowRate;
        ZAxisFlowRate = zAxisFlowRate;
        _materialIndexPlusThree = (byte)(materialIndex + 3);
        MaterialIndex = materialIndex;
    }
}