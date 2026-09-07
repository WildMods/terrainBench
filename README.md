Terrain Workbench is a PC editor for Zelda: Breath of the Wild's overworld map.
The map is made of a terrain heightmap, ground material data, a grass
color/heightmap, and a water/lava heightmap.
Terrain Workbench currently supports editing the terrain heightmap and ground
material (which also determines the footstep sounds).

## Setup & Usage
To load the terrain data, Terrain Workbench needs your BOTW game files.
If you have BCML or UKMM set up, the editor will automatically import your
saved game paths. Otherwise, use the `Settings` menu in the toolbar to set your
game paths (press the `Select` button next to each folder to pick a folder in
File Explorer).
On my machine, the folders look like this:

<details>
<summary> Click this dropdown to see example Wii U game paths</summary>
```
Base game folder: C:\storage\games\Cemu\mlc01\usr\title\00050000\101c9400\content
Update folder: C:\storage\games\Cemu\mlc01\usr\title\0005000e\101c9400\content
DLC folder: C:\storage\games\Cemu\mlc01\usr\title\0005000c\101c9400\content\0010
```
Make sure your DLC folder ends in `0010`, not `content`.
</details>
<details>
<summary> Click this dropdown to see example Switch game paths</summary>
```
Base game folder: C:\storage\games\Switch\yuzu\dump\01007EF00011E000\romfs
Update folder: C:\storage\games\Switch\yuzu\dump\01007EF00011E000\romfs
DLC folder: C:\storage\games\Switch\yuzu\dump\01007EF00011F001\romfs
```
Note that on Switch, the base and update folders are the same (this distinction
doesn't exist like it does on the Wii U). Also, there's no `0010` folder like
there is on the Wii U version.
</details>
  
These paths will be saved to `%LOCALAPPDATA%\botw_tools\settings.json`
(or on Linux, `~/.local/share/botw_tools/settings.json`), meaning it won't
automatically get new BCML/UKMM paths if you change them there.

You can also set the *mod folder* in the same `Settings` menu, which is where
your terrain edits will be saved. When you start the editor, files in the mod
folder are loaded instead of the versions in the other 3 folders. This means
that you can start a new session later, and still see all your edits shown in
the context of the whole map.

I reccommend you set the mod folder to one of your emulator mod folders, not a
folder for your mod loader (e.g. UKMM). Terrain data is especially easy to
edit, because it doesn't need any merging or RSTB changes to work. If you save
directly to an emulator mod folder (like a Cemu graphics pack), you can see
changes without rebooting the game or remerging your mods, just by teleporting
away and coming back.

Only the parts of the world you make edits to *during the current session* are
saved when you press the "Save" button (or keybind). Even if you have a huge
project with lots of edits loaded, saving is generally as fast as a new project
unless you've edited huge areas of the map *during that session*.

Saving also automatically generates data for lower detail levels, which
prevents pop-in.

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
- Save: `Control + S`


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

## Average Performance
On my machine (a 2021 gaming laptop with a 6GB RTX 3060), startup takes 10-30
seconds (mostly depends on disk speed and how recently the files have been
used). During normal use, Terrain Workbench uses about 4.7GB of RAM, and about
1.5GB of VRAM. The minimum OpenGL version is 4.3, from 2012 (so you should be
fine with any decent post-2013 hardware).
It's generally stable at 165FPS (my refresh rate) at 80-100% GPU usage, but if
the entire map is on screen, it drops to about 115FPS.
Startup time mostly depends on disk speed (and multi-core CPU power), but the
average framerate depends almost entirely on GPU power.

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
