# Changelog

## 0.1.0

- The hoe works on a grid. A grid like BetterHammer's build grid is drawn on the ground at the terrain's own 1 m spacing, and the hoe changes exactly the rectangle marked on it instead of a round patch.
- Change the area's width (west to east) with `Ctrl+Scroll` and its length (south to north) with `Shift+Scroll`.
- Level ground flattens the whole area to the height you aim at. Middle-click locks that height, so neighbouring areas can be levelled to exactly the same height.
- The new Ramp tool builds a smooth slope with a constant angle. Click where it starts and where it ends, and set its width with `Ctrl+Scroll`. Its sides blend into the ground around them over a distance you set with `Alt+Scroll`. Each end takes the height you aim at, or the locked height, so a ramp can lead up to a floor. Right-click cancels a start you've set. The preview shows the ramp in blue and the edge blend in violet, and the length, end height, rise and slope are shown while you aim.
- Raise ground lifts the area by a fixed step, and the new Lower ground tool sinks it. Change the step with `Alt+Scroll`.
- The new Reset height tool undoes every height change in the area, back to the natural ground.
- Pathen, Paved road and the new Reset paint tool paint exactly the area. Reset paint removes paths and paving so the grass grows back.
- A preview shows the result before you click: the outline of the area on the ground, and the surface it will have.
- Text under the crosshair shows the tool, the area size and the height or step, and warns when ground would stop at vanilla's 8 m limit.
- Shaping the ground is free: Raise ground no longer costs stone. Painting costs what vanilla charges (one stone for Paved road). No hoe tool needs a workbench or stonecutter nearby.
- Each use costs the same stamina and durability as one vanilla use, whatever the area's size.
- The key hints at the bottom of the screen fit the hoe: they say what each key does with the selected tool ("Level area", "Hoe menu", "Lock height"), list the area controls, and leave out the ones that do nothing with a hoe (remove, copy, snapping, and rotating terrain tools).
- The hoe menu no longer shows the Q and E tab keys, as it has no tabs.
- Turn the grid off for the vanilla tools with `Ctrl+Right-Click` to use vanilla's round brushes again.
