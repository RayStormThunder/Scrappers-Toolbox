# Scrapper's-Toolbox
This is a fork created to solve one specific problem: when editing Layout and Animations in the BRLYT and BRLAN formats,
the files would get exported in slightly different ways, causing crashes in Skyward Sword.
This fork's only purpose is to fix this issue and overall improve the layout system.
This program has only been tested in this regard, and changes I made to fix this issue may have broken other areas of the program.

# List of all changes made for Scrapper's Toolbox
File Data
- BRLYT (layout) file types now export correctly.
- BRLAN (animation) file types now export correctly.
- ARC files now export correctly from the Main Window.


Main Window
- Loaded Files now start Maximized.
- Added a "Backups" toggle. When enabled it will create backups on load/saves.
- Made Save button always work due to ARC file fixes.


Layout Window
- Added "Visibility Mode" toggle.
	- "Always Visible" Makes every pane show its texture.
	- "Partial Transparency" Makes every pane that usually wouldn't show, show up with 50% transparency.
	- "Follows Animation" Makes a pane show only if the animation makes it visible.
	- "Never Visible" Makes every pane not show at all.
- Added "Multiclick" which makes everything you multiselect with ctrl click have the same changes applied to them.
- Added "Multiclick Behavior" toggle.
	- "Absolute" Changing a value makes every other pane have that same value.
	- "Relative" Changing a value makes every other pane have its value changed by the difference in values.
		- When applied to colors, this setting only changes hue.
- Added the ability to add, delete, copy, paste in Hierarchy.
- Added the ability to move items around in the Hierarchy.
- Added "Frame Colors" section to the "Colors" tab which shows the colors of every animation that pane is affected by.
- Added the ability to add a group in the "RootGroups" section.
- Added the ability to add/replace textures to a layout from the arc it resides in via a secondary UI window.
- Changed the "Animation Hierarchy" to sort animations from your current layout at the top and grey everything else out.
- Fixed an issue where adding more animation info in the animation window would break.
- Added the ability to copy and paste animation data.
- Added a toggle in the "Timeline" called "Force Max Width" which makes the timeline automatically display from its smallest to largest value.
- Added back and forward buttons in the "Timeline."
- Changed the "Timeline" to show you what pane and animation is currently selected.
- Made the Docks remember their position when you close the program.
- Fixed many issues of UI elements breaking causing them to not show anymore.
- The "Name" field on a pane now tells you the maximum number of characters you can have.
- Fixed an issue where deleting a texture from the "Texture List" would cause every pane with a texture ID after the texture that got deleted would have the wrong ID after saving and reloading.
- Deleting an entry in the "Texture List" no longer deletes that texture from the arc file entirely.
- Fixed an issue where copying and pasting a "Window Pane" would cause a crash on save and reload.
- Fixed an issue where copied panes shared color data with the pane they were copied from.
- You can now add MaterialColors in Animations.
- Added a new toggle at the top called "Transform Data to Animation Data." When enabled, if you make a transformation, for example changing the X Position from 20 to 40, and there is an animation that currently sets the X Position to 20, it will update to the new value of 40.
- Changed "Display Panes" to "Toggle Visibility in Viewer" which now hides that pane and its children in the viewer and does not affect the visibility toggle.
- Added a way to expand/collapse all of a pane's children when you right-click it.
- Properly updates docks when going between multiple layouts in the same editor.
- Clicking on empty space on the Timeline now selects the nearest frame.
- Using the Frame Step buttons now properly updates the Viewer visually.