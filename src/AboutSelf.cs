namespace terrainBench;

public static class AboutSelf {
    public static string name = "Terrain Workbench";
    public static string version = "1.0";
    public static string authors = "Torphedo & Ginger Chody";
    public static string resources = @"ZeldaMods wiki (see zeldamods.org/wiki/Terrain)";
    public static string specialThanks = @"
    - Ginger Chody: BCML + UKMM settings loader, terrain tile loader, terrain upscaler
    - Zephenryus: Did all the original research needed to implement the renderer & editor
    - Ginger Chody, Echocolat, Greenlord, Waikuteru:
          Additional minor contributions to the relevant ZeldaMods wiki pages
    - Decaf Emu Team: Wrote the original Wii U texture decoder that Toolbox and this editor now use
    - KillzXGaming: C# port of Wii U texture decoder
    - MindStormMan06: Switch texture decoder library for C#
    - The5thTear: Helped fix Wii U texture decoding (made startup time significantly better)
    - dt13269: Answered some questions about terrain collisions";

    public static string AboutText() {
        return @$"{name} {version} written by {authors}.
Resources: {resources}
Special thanks: {specialThanks}";
    }
}
