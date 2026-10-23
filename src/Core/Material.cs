using System.Numerics;

namespace terrainBench.Core.LodComponents;

public struct Material
// We need to implement math operators for the downscaler to average pixels
: IAdditionOperators<Material, Material, Material>, IShiftOperators<Material, int, Material>, IBitwiseOperators<Material, ushort, Material>
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
            _ => 0,
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
    

    // The operators we need for averaging pixels
    public static Material operator +(Material a, Material b) {
        return new((byte)(a.Material0 + b.Material0),
            (byte)(a.Material1 + b.Material1),
            (byte)(a.BlendWeight + b.BlendWeight),
            (byte)(a.Unk3 + b.Unk3));
    }
    
    public static Material operator >>(Material a, int s) {
        return new((byte)(a.Material0 >> s), (byte)(a.Material1 >> s),
            (byte)(a.BlendWeight >> s), (byte)(a.Unk3 >> s));
    }
    
    public static Material operator &(Material a, ushort m) {
        return new((byte)(a.Material0 & m), (byte)(a.Material1 & m),
            (byte)(a.BlendWeight & m), (byte)(a.Unk3 & m));
    }

    
    
    // We don't need any of these operators, but the interfaces require them
    public static Material operator >>>(Material a, int s) {
        return new((byte)(a.Material0 >>> s), (byte)(a.Material1 >>> s),
            (byte)(a.BlendWeight >>> s), (byte)(a.Unk3 >>> s));
    }
    
    public static Material operator <<(Material a, int s) {
        return new((byte)(a.Material0 << s), (byte)(a.Material1 << s),
            (byte)(a.BlendWeight << s), (byte)(a.Unk3 << s));
    }
    
    public static Material operator |(Material a, ushort m) {
        return new((byte)(a.Material0 | m), (byte)(a.Material1 | m),
            (byte)(a.BlendWeight | m), (byte)(a.Unk3 | m));
    }

    public static Material operator ~(Material a) {
        return new((byte)~a.Material0, (byte)~a.Material1, (byte)~a.BlendWeight, (byte)~a.Unk3);
    }
    
    public static Material operator ^(Material a, ushort m) {
        return new((byte)(a.Material0 ^ m), (byte)(a.Material1 ^ m),
            (byte)(a.BlendWeight ^ m), (byte)(a.Unk3 ^ m));
    }
}
