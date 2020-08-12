# WaymarkPresetPlugin

## Purpose
This is a plugin for [XIVLauncher/Dalamud](https://github.com/goatcorp/FFXIVQuickLauncher) that stores unlimited waymark presets.

## Usage
* This plugin operates on a "library" of presets that is maintained separately from the game itself.  It only interacts with the game's saved presets when you tell it to.
* Type `/pwaymark` in the game's chat window to bring up the main GUI for the plugin.
* By default, you will see a window with text indicating that you have no presets in your library, and an expandable section below it for importing waymarks.  There are two ways to import a waymark preset to the library: Copy from one of the game's five slots, or importing from another player/program.
	* Importing from a game slot should be obvious; just press one of the 1-5 buttons under the "Import Options" heading to copy from the corresponding slot in the game's waymark dialog.  Note that importing this way will give the preset a generic name (you can edit this later).
	* Importing a preset from another user or program is also very simple; just paste the preset into the text box, and click the "Import" button.  Presets from any program that uses the Paisley Park JSON format are accepted.  If you don't see the text box go empty and a new preset appear in the library, that means that the preset you pasted was invalid.
* Once you have imported one or more presets, you will now see a listing of those presets above the "Import Options" area.  By default, these presets are grouped by the zones to which they apply.
* Clicking on one of these presets will display an additional pane to the right of the list.  This pane has a few different functions:
	* The first, and most obvious, is that it shows the coordinates, zone ID, and last-modified time of the selected preset.
	* Next, the five buttons at the top of the pane will copy the selected preset into the corresponding slot in the game's waymark dialog, allowing it to be placed while in a duty.  This overwrites whatever was in that slot, so make sure that you've backed up whatever was in that slot if you want to keep it.
	* Finally, the edit and delete buttons at the bottom.  The delete button is self-explanatory.  The edit button brings up a window that allows you to directly edit the coordinates of each waymark, which waymarks are active, and the zone to which the preset applies.

## Direct Placement
**Note: Temporarily disabled in 1.0.1.0 pending new Actor struct info.**

~~This plugin supports placing a preset directly from the plugin as of version 1.0.0.0; however, this must be enabled in the plugin settings.  The reason for this is that placing from the plugin goes a little bit deeper into the game's chain of how waymark presets are placed, and while I've tried to be careful, and this feature has been tested and appears to work without issue, it is possible that something has been missed.  Copying presets from the library into a slot in the game's menu is quite clear-cut and simple.  Direct placement less so.~~

One more thing to note about this feature: it performs most of the checks that the game does, including checking that you are in an instance type that allows waymarks to be saved, and checking that you are not in combat.  The only thing that it does not check is that the zone of the preset matches the zone that you are currently in.

I am sorry that this will not work outside of instances, but there appear to be additional checks further down the chain that prevent this.  If someone else wants to figure it out, go ahead, but I don't personally want to spend the time going deeper into figuring it all out.

## IMPORTANT
As is the case with all third-party programs and tools, this plugin *does not* operate through an official API; it digs into the game and operates on the best information available inferred from game behavior and structure.  Something could always go wrong, SE could find out and punish you, something unintended and bad could happen, etc.  Use of this plugin is very much at your own risk.  There are no guarantees, express or implied.  I've made my best effort to keep it as safe as I can, and that's it.

## License
Code and executable are covered under the [MIT License](../LICENSE).  Final Fantasy XIV (and any associated data used by this plugin) is owned by and copyright Square Enix.
