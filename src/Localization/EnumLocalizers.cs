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
}