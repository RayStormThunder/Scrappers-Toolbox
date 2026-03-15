# Scrapper's-Toolbox
This is a fork created to solve one specific problem. When editing Layout and Animations in the BRLYT and BRLAN formats.
The files would get exported into slightly different ways causing crashes in Skyward Sword.
This fork's only purpose is to fix this issue and overall improve the layout system.
This program has only been tested in this regard and changes I made to fix this issue may have broken other areas of the program.

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
	- "Always Visible" Makes every pane show it's texture.
	- "Partial Transparency" Makes every pane that usually wouldn't show, show up with 50% transparency.
	- "Follows Animation" Makes pane show only if the animation has it show.
	- "Never Visible" Makes every pane not show at all.
- Added "Multiclick" which makes everything you multiselect with ctrl click have the same changes applied to them.
- Added "Multiclick Behavior" toggle.
	- "Absolute" Changing a value makes every other pane have that same value.
	- "Relative" Changing a value makes every other pane have their value change by the difference in values.
		- When applied to colors this setting only changes Hue.
- Added the ability to add, delete, copy, paste in Hierarchy.
- Added the ability to move items around in the Hierarchy.
- Added "Frame Colors" section to the "Colors" tab which shows the colors of every animation that pane is affected by.
- Added the ability to add a group in the "RootGroups" section.
- Added the ability to add/replace textures to a layout from the arc it resides in via a secondary UI window.
- Changed the "Animation Hierarchy" to sort animations from your current layout at the top and grey everything else out.
- Fixed an issue where adding more animation info in the animation window would bug out.
- Added the ability to copy and paste animation data.
- Added a toggle in the "Timeline" called "Force Max Width" which will have the timeline to automatically display from it's smallest to largest value.
- Added a back and forwards button in the "Timeline."
- Changed the "Timeline" to show you what pane and animation is currently selected.
- Made the Docks remember their position when you close the program.
- Fixed many issues of UI elements breaking causing them to not show anymore.