[![CI](https://img.shields.io/github/actions/workflow/status/javidahmed64592/valheim-betterhoe/ci.yml?branch=main&style=flat-square&label=CI&logo=github)](https://github.com/javidahmed64592/valheim-betterhoe/actions/workflows/ci.yml)
[![Package](https://img.shields.io/github/actions/workflow/status/javidahmed64592/valheim-betterhoe/package.yml?branch=main&style=flat-square&label=Package&logo=github)](https://github.com/javidahmed64592/valheim-betterhoe/actions/workflows/package.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

# BetterHoe

Shape terrain on a grid with the hoe: level, raise, lower, paint and reset exact areas, build smooth ramps, and take out a hoe with one key.

- **Equip a hoe**: Press `N` to take out a hoe without having one in your inventory, and again to put it away.
- **Terrain grid**: A grid is drawn on the ground at the terrain's 1 m spacing, matching [BetterHammer](https://thunderstore.io/c/valheim/p/Aestelwen/BetterHammer/)'s build grid. The hoe changes exactly the rectangle marked on it.
- **Level ground**: Flatten the whole area to the height you aim at, or lock a height to level several areas to the same height.
- **Ramp**: Click a start and an end to build a smooth, constant slope between them, with sides that blend into the ground.
- **Raise and lower ground**: Lift or sink the whole area by a fixed step. Lower ground is a new hoe tool.
- **Reset height**: A new hoe tool that undoes every height change in the area, back to the natural ground.
- **Paint**: Pathen, Paved road and the new Reset paint tool paint exactly the area. Reset paint removes paths and paving so the grass grows back.
- **Costs**: Shaping the ground is free and painting costs what vanilla charges, with no workbench or stonecutter needed.
- **Preview**: See the area's outline and the surface it will have before you click, with its size and height under the crosshair.
- **Key hints**: The hints at the bottom of the screen say what each key does with the hoe, and the ones that do nothing with a hoe are left out.

## Terrain Grid

While one of the hoe's terrain tools is selected, the area it changes is outlined in gold on the ground. Level, Raise and Lower ground and Reset height also show the new surface in blue.

| Key                | Action                                                        |
| ------------------ | ------------------------------------------------------------- |
| `Ctrl+Scroll`      | Change the area's width (west to east).                       |
| `Shift+Scroll`     | Change the area's length (south to north).                    |
| `Alt+Scroll`       | Change how far Raise and Lower ground move the ground.        |
| `Middle-Click`     | Level ground: lock or unlock the height you are aiming at.    |
| `Ctrl+Right-Click` | Turn the grid on or off for the vanilla tools.                |

- Each use costs the same stamina and durability as one vanilla use, whatever the area's size. The largest area is set by the server.
- Like vanilla, ground can't be moved more than 8 m above or below its natural height. The text under the crosshair warns you when part of the area would stop there.
- Wards and no-build areas are checked across the whole area, not just where you aim.
- With the grid off, Level ground, Raise ground, Pathen and Paved road work like vanilla. Ramp, Lower ground, Reset height and Reset paint always use the grid.

## Ramp

Select **Ramp** in the hoe menu, click where the ramp starts, then click where it ends. Until the second click, a preview shows the ramp's surface in blue, its outline on the ground in gold, and the edge blend in violet, fading out towards the ground. The text under the crosshair shows the ramp's length, end height, rise and slope.

| Key             | Action                                                              |
| --------------- | ------------------------------------------------------------------- |
| `Left-Click`    | Set the start, then build the ramp to the point you're aiming at.   |
| `Right-Click`   | Cancel the start.                                                   |
| `Ctrl+Scroll`   | Change the ramp's width.                                            |
| `Alt+Scroll`    | Change how far the sides blend into the ground around the ramp.     |
| `Middle-Click`  | Lock or unlock the height you are aiming at. Each end uses the lock if it's on when you click. |

- Each end takes the height of the ground at that point, or the top of a floor piece you aim at, or the locked height.
- The ramp can run in any direction. On the 1 m terrain grid a diagonal ramp's sides are slightly stepped; the blend smooths them.
- Setting the start is free; building the ramp costs one use.

## Equip Hoe

Press `N` to take out a hoe, even if you don't have one in your inventory, and press it again to put it away. It never wears out. Rebind the key in the game's controls menu.

## Configuration

Settings are in the BepInEx config file `javidahmed64592.betterhoe.cfg`.
Settings marked "(Synced with server)" use the server's values.

## Compatibility

- Every player and the server need the mod, because the terrain changes are sent to whoever looks after that piece of ground.
- Mods that change the hoe's tools or how terrain is edited may clash with the grid.
- Works alongside [BetterHammer](https://thunderstore.io/c/valheim/p/Aestelwen/BetterHammer/): `B` takes out its hammer and `N` this mod's hoe.
- This mod has not been tested with a gamepad, only with keyboard and mouse.

## Uninstalling

The terrain changes are ordinary vanilla terrain data, so they stay in the world after the mod is removed. Ramp, Lower ground, Reset height and Reset paint disappear from the hoe, and Raise ground and Paved road need their vanilla materials and stations again.

## License

This project is licensed under the MIT License - see the [LICENSE](https://github.com/javidahmed64592/Valheim-BetterHoe/blob/main/LICENSE) file for details.
