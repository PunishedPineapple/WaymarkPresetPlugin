# WaymarkPresetPlugin

## IMPORTANT
**This plugin is now being maintained at https://github.com/squidmade/WaymarkPresetPlugin, and this repository will be archived.**

## Purpose
This is a plugin for [XIVLauncher/Dalamud](https://github.com/goatcorp/FFXIVQuickLauncher) that stores unlimited waymark presets.

## Obtaining Presets
Em-Six has a [collection of presets for current and older content on their wiki](https://github.com/Em-Six/FFXIVWaymarkPresets/wiki).  See the Usage section below for how to import these.

## Usage
* This plugin operates on a "library" of presets that is maintained separately from the game itself.  It only interacts with the game's saved presets when you tell it to.
* Type `/pwaymark` in the game's chat window to bring up the main GUI for the plugin.
* By default, you will see a window with text indicating that you have no presets in your library, and an expandable section below it for importing waymarks.  There are two ways to import a waymark preset to the library: Copy from one of the game's five slots, or importing from another player/program.
	* Importing from a game slot should be obvious; just press one of the 1-5 buttons under the "Import Options" heading to copy from the corresponding slot in the game's waymark dialog.  Note that importing this way will give the preset a generic name (you can edit this later).
	* Importing a preset from another user or program is also very simple; just paste the preset into the text box, and click the "Import" button.  Presets from any program that uses the Paisley Park JSON format are accepted.  If you don't see the text box go empty and a new preset appear in the library, that means that the preset you pasted was invalid.
* Once you have imported one or more presets, you will now see a listing of those presets above the "Import Options" area.  By default, these presets are grouped by the zones to which they apply.
* Clicking on one of these presets will display an additional pane to the right of the list.  This pane has a few different functions:
	* Shows the coordinates, zone, and last-modified time of the selected preset.
	* The five buttons at the top of the pane will copy the selected preset into the corresponding slot in the game's waymark dialog, allowing it to be placed while in a duty.  This overwrites whatever was in that slot, so make sure that you've backed up whatever was in that slot if you want to keep it.
	* The "Map View" button shows a window with the zone map and placed waymark icons (these can be dragged when the editor window is also open).
	* Finally, the export, edit, and delete buttons at the bottom.  The export and delete buttons are self-explanatory.  The edit button brings up a window that allows you to directly edit the coordinates of each waymark, which waymarks are active, and the zone to which the preset applies.

## Direct Placement

The plugin provides a button and text commands to place a preset directly from the library as of v1.0.0.0, and a button to directly save current waymarks to the library as of v1.1.0.0

This feature performs most of the same checks that the game does, including verifying that you are in an instance type that allows waymarks to be saved, and checking that you are not in combat.  The only thing that it does not check is that the zone of the preset matches the zone that you are currently in.

I am sorry that this will not work outside of instances, but there appear to be additional checks further down the chain that prevent this.  If someone else wants to pursue this, go ahead, but I don't personally want to spend the time going deeper into figuring it all out.  Getting around this by placing waymarks individually sequentially would probably not get accepted to the main Dalamud repo.

## Map View Notes

The map view has two major limitations:
* For dungeons/raids that have multiple maps, all active waymarks are displayed on all maps.  This is planned to be fixed at some point, but the solution is currently unknown.
* Dragging waymarks on the map while editing in zones that have uneven ground will generally give undesirable results, in that the height of the waymark will not be constrained to the ground.  You may end up with waymarks that the game won't place, or even with waymarks below the ground or floating in the air.  This is not likely to be addressed due to the immense amount of work for very little payoff.

## IMPORTANT
**As of 2022-09-12, SE has taken action against players that have placed out of bounds (OOB) waymarks.  Please exercise judgement when placing presets that may contain OOB waymarks.**

As is the case with all third-party programs and tools, this plugin *does not* operate through an official API; it digs into the game and operates on the best information available inferred from game behavior and structure.  Something could always go wrong, SE could find out and punish you, something unintended and bad could happen, etc.  Use of this plugin is very much at your own risk.  There are no guarantees, express or implied.  I've made my best effort to keep it as safe as I can (I use this myself, after all), and that's it.

## License
Code and executable are covered under the [MIT License](../LICENSE).  Final Fantasy XIV (and any associated data used by this plugin) is owned by and copyright Square Enix.
