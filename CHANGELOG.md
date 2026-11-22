# Changelog

## Unreleased

- New Cultivate tool: cultivates exactly the marked area for planting, without changing its height and without needing a cultivator.
- Fixed painting and undoing paint sometimes not being saved: swapping one paint for another (such as undoing a path or cultivated ground) came back after logging out and in again.

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
