using terrainBench.Core;

namespace terrainBench;

public static class EnumLocalizers {
    public static string Localize(Camera.Mode m) {
        var msg = GMsg.l.camera;
        return m switch {
            Camera.Mode.FLY => msg.modeFly,
            Camera.Mode.ORBIT => msg.modeOrbit,
            Camera.Mode.MINECRAFT => msg.modeMinecraft,
            _ => "[unknown camera mode]",
        };
    }

    public static string Localize(LodComponent l) {
        var msg = GMsg.l.brush;
        return l switch {
            LodComponent.hght => msg.tileTypeHGHT,
            LodComponent.mate => msg.tileTypeMATE,
            LodComponent.grass => msg.tileTypeGRASS,
            LodComponent.water => msg.tileTypeWATER,
            _ => "[unknown tile type]",
        };
    }
    
    public static string Localize(Brush.EditFunc f) {
        var msg = GMsg.l.brush;
        return f switch {
            Brush.EditFunc.ADD => "",
            _ => "[unknown brush edit function]",
        };
    }
    
    public static string Localize(Brush.FalloffFunc f) {
        var msg = GMsg.l.brush;
        return f switch {
            Brush.FalloffFunc.LINEAR => msg.falloffLinear,
            Brush.FalloffFunc.INVERSE => msg.falloffInverse,
            Brush.FalloffFunc.SQUARE => msg.falloffSquare,
            Brush.FalloffFunc.INVERSE_SQUARE => msg.falloffInverseSquare,
            _ => "[unknown brush falloff function]",
        };
    }
    
    public static string Localize(Brush.DistanceType t) {
        var msg = GMsg.l.brush;
        return t switch {
            Brush.DistanceType.EUCLIDEAN => msg.shapeCircle,
            Brush.DistanceType.MANHATTAN => msg.shapeSquare,
            _ => "[unknown brush falloff shape]",
        };
    }
}