# Changelog

## Unreleased

- If a game update breaks part of the mod, only that part turns off and the rest keeps working: the key hints and the hoe menu's "Terrain Tools" title can each turn off while the grid, marking, ramps and undo still work. The log says which part is off, in one line. If shaping terrain itself breaks, the whole mod turns off cleanly instead of throwing errors.
- New `DisabledFeatures` setting under `[Debug]`, for testing: turns parts of the mod off by name.
- Updated Jotunn to 2.30.3.

## 0.3.1

- No longer logs a warning about a missing LiberationSans SDF font.

## 0.3.0

- Mark ground to change it all at once: sweep the area over the grid with `Alt+Middle-Click` to mark any shape (`Alt+Right-Click` unmarks, `Backspace` clears, and pressing it again with nothing marked brings the last cleared ground back), then one click levels, raises, lowers, paints or resets all of it. It can be up to 64 m across (`Max Region Size`, set by the server); at the limit, only the part of the area that fits is marked.

## 0.2.0

- New Cultivate tool: cultivates exactly the marked area for planting, without changing its height and without needing a cultivator.
- Fixed paint changes sometimes being lost on logging out: swapping one paint for another, such as undoing a path or cultivated ground, wasn't saved.

## 0.1.0

- First release.
- The hoe works on a 1 m terrain grid, like BetterHammer's build grid, and changes exactly the marked area instead of a round patch. Set its width with `Ctrl+Scroll` and its length with `Shift+Scroll`.
- Level ground flattens the area to the aimed height; middle-click locks that height.
- Raise ground and the new Lower ground move the area by a step you set with `Alt+Scroll`.
- New Ramp tool: click a start and an end for a smooth slope with blended sides.
- New Reset height and Reset paint tools return the ground to its natural height or grass.
- A preview and readout show the result, size, height and slope before you click.
- Undo and redo terrain changes with `Ctrl+Z` and `Ctrl+Y`.
- Press `N` to take out a hoe without having one.
- Shaping the ground is free, painting costs what vanilla charges, and no tool needs a workbench.
- Tidier key hints and hoe menu.
- `Ctrl+Right-Click` switches the vanilla tools back to vanilla's round brushes.
