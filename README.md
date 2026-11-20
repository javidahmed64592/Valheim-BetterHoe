[![CI](https://img.shields.io/github/actions/workflow/status/javidahmed64592/valheim-betterhoe/ci.yml?branch=main&style=flat-square&label=CI&logo=github)](https://github.com/javidahmed64592/valheim-betterhoe/actions/workflows/ci.yml)
[![Package](https://img.shields.io/github/actions/workflow/status/javidahmed64592/valheim-betterhoe/package.yml?branch=main&style=flat-square&label=Package&logo=github)](https://github.com/javidahmed64592/valheim-betterhoe/actions/workflows/package.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

# BetterHoe

Shape terrain on a grid with the hoe: level, raise, lower and paint exact rectangles of ground.

- **Terrain grid**: A grid is drawn on the ground at the terrain's 1 m spacing, matching [BetterHammer](https://thunderstore.io/c/valheim/p/Aestelwen/BetterHammer/)'s build grid. The hoe changes exactly the rectangle marked on it.
- **Level ground**: Flatten the whole area to the height you aim at, or lock a height to level several areas to the same height.
- **Raise and lower ground**: Lift or sink the whole area by a fixed step. Lower ground is a new hoe tool.
- **Paint**: Pathen, Paved road and the new Reset ground tool paint exactly the area. Reset ground removes paths and paving so the grass grows back.
- **Preview**: See the area's outline and the surface it will have before you click, with its size and height under the crosshair.
- **Key hints**: The hints at the bottom of the screen say what each key does with the hoe, and the ones that do nothing with a hoe are left out.

## Terrain Grid

While one of the hoe's terrain tools is selected, the area it changes is outlined in gold on the ground. Level, Raise and Lower ground also show the new surface in blue.

| Key                | Action                                                        |
| ------------------ | ------------------------------------------------------------- |
| `Ctrl+Scroll`      | Change the area's width (across your view).                   |
| `Shift+Scroll`     | Change the area's length (along your view).                   |
| `Alt+Scroll`       | Change how far Raise and Lower ground move the ground.        |
| `Middle-Click`     | Level ground: lock or unlock the height you are aiming at.    |
| `Ctrl+Right-Click` | Turn the grid on or off for the vanilla tools.                |

- The area turns with your view, so its width always runs across your screen.
- Each use costs the same stamina, durability and materials as one vanilla use, whatever the area's size. The largest area is set by the server.
- Like vanilla, ground can't be moved more than 8 m above or below its natural height. The text under the crosshair warns you when part of the area would stop there.
- Wards and no-build areas are checked across the whole area, not just where you aim.
- With the grid off, Level ground, Raise ground, Pathen and Paved road work like vanilla. Lower ground and Reset ground always use the grid.

## Configuration

Settings are in the BepInEx config file `javidahmed64592.betterhoe.cfg`.
Settings marked "(Synced with server)" use the server's values.

## Compatibility

- Every player and the server need the mod, because the terrain changes are sent to whoever looks after that piece of ground.
- Mods that change the hoe's tools or how terrain is edited may clash with the grid.
- This mod has not been tested with a gamepad, only with keyboard and mouse.

## Uninstalling

The terrain changes are ordinary vanilla terrain data, so they stay in the world after the mod is removed. Lower ground and Reset ground disappear from the hoe.

## License

This project is licensed under the MIT License - see the [LICENSE](https://github.com/javidahmed64592/Valheim-BetterHoe/blob/main/LICENSE) file for details.
