namespace terrainBench.LodComponents;

public readonly struct Material
{
    public readonly byte Material0;
    public readonly byte Material1;
    public readonly byte BlendWeight;
    public readonly byte Unk3;

    public Material(byte material0, byte material1, byte blendWeight, byte unk3)
    {
        Material0 = material0;
        Material1 = material1;
        BlendWeight = blendWeight;
        Unk3 = unk3;
    }
}