namespace terrainBench.LodComponents;

public struct Material
{
    public enum Component {
        Material0, Material1, BlendWeight, Unknown,
    }
    
    public byte Material0;
    public byte Material1;
    public byte BlendWeight;
    public byte Unk3;

    public Material(byte material0, byte material1, byte blendWeight, byte unk3)
    {
        Material0 = material0;
        Material1 = material1;
        BlendWeight = blendWeight;
        Unk3 = unk3;
    }

    public byte GetComponent(int comp) {
        return comp switch {
            0 => Material0,
            1 => Material1,
            2 => BlendWeight,
            3 => Unk3,
        };
    }
    public void SetComponent(byte val, int comp) {
        switch (comp) {
            case 0: Material0 = val; break;
            case 1: Material1 = val; break;
            case 2: BlendWeight = val; break;
            case 3: Unk3 = val; break;
        }
    }
}