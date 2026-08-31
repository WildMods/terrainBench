Terrain Workbench is a PC editor for Zelda: Breath of the Wild's overworld map.
The map is made of a terrain heightmap, ground material data, a grass
color/heightmap, and a water/lava heightmap.
Terrain Workbench currently supports editing the terrain heightmap and ground
material (which also determines the footstep sounds).

## Setup & Usage
To load the terrain data, Terrain Workbench needs your BOTW game files.
If you have BCML or UKMM set up, the editor will automatically import your
saved game paths. Otherwise, you can set the paths by running the editor on the
command-line. On my machine, it looks like this:

```
terrainBench.exe C:\storage\games\Cemu\mlc01\usr\title\00050000\101c9400\content\ C:\storage\games\Cemu\mlc01\usr\title\0005000e\101c9400\content\ C:\storage\games\Cemu\mlc01\usr\title\0005000c\101c9400\content\0010
```
Make sure your DLC folder ends in `0010`, not `content` or `romfs`.
These paths will be saved to `%LOCALAPPDATA%\botw_tools\settings.json`
(or on Linux, `~/.local/share/botw_tools/settings.json`), meaning it won't
automatically get new BCML/UKMM paths if you change them there.

Next, you need to choose where your terrain mod will be saved to. You can set
this in the JSON file, or use the `File > Open Mod Folder` button in the
editor. When you run the editor, edits from your mod folder will be
automatically loaded. I reccommend you set this to one of your emulator mod
folders, not a folder for your mod loader. Terrain data is especially easy to
edit, because it doesn't need any merging or RSTB changes to work. If you save
directly to an emulator mod folder (like a Cemu graphics pack), you can see
changes without rebooting the game or remerging your mods, just by teleporting
away and coming back.

When you save (`Ctrl + S`), the editor automatically generates data for lower
detail levels, which prevents pop-in.

### Controls
- Movement: `W/A/S/D` for horizontal, `Shift`/`Space` for vertical
- Pan camera: Middle mouse click + drag (like Blender)
- Change camera mode (e.g. from orbit to first-person): `M`
- Change primary texture: Up/Down arrows
- Change secondary texture: Left/Right arrows
- Change brush mode (heightmap and texture painting modes): `Q`
- Apply heightmap brush: Left click & drag
- Paint with primary/secondary texture: Left/Right click & drag
  - Hold Shift to paint on the texture blend data
- If you have a drawing tablet, the pen pressure will determine the brush radius


If the keyboard shortcuts aren't responding, try left clicking in the 3D
viewport.

### Pitfalls & Known Issues
- Normally, the game uses static collision in
`Physics/TeraMeshRigidBody/MainField/*.shktmrb` for the terrain. To make the
collision work, you need to stop the game from loading these files (which
forces it to regenerate collision from the heightmap). The easiest method is to 
copy all those vanilla files into your mod, and delete their contents
(you can do this in Notepad on Windows, or do `ftruncate --size 1 *` on Linux).
This is manual for now, but eventually Terrain Workbench will do this for you.

- The brush targeting is still a bit buggy, it sometimes jumps away from the
mouse by about 5% in seemingly random places.

- The renderer uses a fixed amount of VRAM, so if you increase the render
distance or go to a very dense area it may not all load at once.

- There are some minor frame drops (from ~120 -> ~100FPS on my machine) when
using brushes.

## Average Performance
On my machine (a 2021 laptop), startup takes about 7-10 seconds. During normal
use, Terrain Workbench uses about 4.7GB of RAM, and about 1.5GB of VRAM.
The minimum OpenGL version is 4.3, from 2012 (so you should be fine with any
decent post-2013 hardware).

In my testing, saving an average map edit takes 0.3 - 1 second, depending on
size. There are some easy optimizations I haven't implemented yet that should
make this much faster in the future. This isn't a huge concern for me, since it
takes longer to teleport around and get the game to reload your changes than
for the editor to save them.

## Future Plans
The terrain has a dynamic resolution, and at the moment you can't increase the
resolution of an area beyond what's present in the original game. This is
especially an issue for `AocField` (the Trial of the Sword map), which has tons
of open space but is fairly low resolution. I'd like to support increasing the
resolution in the future, which will probably require editing the TSCB
(`MainField.tscb` / `AocField.tscb`).

To support this, I'd also like to add the option of loading `AocField`. I
tested this early on (which worked fine), but parts of the editor are currently
hardcoded to use `MainField`.

## Sources & Thanks
This program was written entirely based on the file specifications on the ZeldaMods wiki, written mostly by [Zephenryus](https://github.com/zephenryus). I've written a higher-level summary linking to his wiki pages [here](https://zeldamods.org/wiki/Terrain). These pages also had contributions from [Ginger](https://github.com/GingerAvalanche), [Echocolat](https://github.com/Echocolat), [Greenlord / S41L0R](https://github.com/S41L0R), and [Waikuteru](https://www.youtube.com/@Waikuteru). Ginger also wrote the BCML/UKMM settings loader, terrain tile loader, and tile upscaler for Terrain Workbench.
