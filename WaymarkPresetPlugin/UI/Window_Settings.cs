using System;

using CheapLoc;

using Dalamud.Plugin;

using ImGuiNET;

namespace WaymarkPresetPlugin
{
	internal sealed class Window_Settings : IDisposable
	{
		public Window_Settings( PluginUI UI, DalamudPluginInterface pluginInterface, Configuration configuration )
		{
			mUI = UI;
			mPluginInterface = pluginInterface;
			mConfiguration = configuration;
		}

		public void Dispose()
		{

		}

		public void Draw()
		{
			if( !WindowVisible )
			{
				return;
			}

			if( ImGui.Begin( Loc.Localize( "Window Title: Config", "Waymark Settings" ) + "###Waymark Settings", ref mWindowVisible,
				ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse ) )
			{
				ImGui.Checkbox( Loc.Localize( "Config Option: Always Show Info Pane", "Always show preset info pane." ) + "###Always show preset info pane checkbox", ref mConfiguration.mAlwaysShowInfoPane );
				ImGui.Checkbox( Loc.Localize( "Config Option: Clicking Preset Unselects", "Clicking the selected preset unselects it." ) + "###Clicking the selected preset unselects it checkbox", ref mConfiguration.mAllowUnselectPreset );
				ImGui.Checkbox( Loc.Localize( "Config Option: Categorize Presets by Zone", "Categorize presets by zone." ) + "###Categorize Presets By Zone Checkbox", ref mConfiguration.mSortPresetsByZone );
				ImGui.Checkbox( Loc.Localize( "Config Option: Open and Close with Game Window", "Open and close library with the game's waymark window." ) + "###Open and Close With Game Window checkbox", ref mConfiguration.mOpenAndCloseWithFieldMarkerAddon );
				ImGui.Checkbox( Loc.Localize( "Config Option: Attach to Game Window", "Attach library window to the game's waymark window." ) + "###Attach library window to the game's waymark window checkbox.", ref mConfiguration.mAttachLibraryToFieldMarkerAddon );
				ImGui.Checkbox( Loc.Localize( "Config Option: Show ID in Zone Names", "Show ID numbers next to zone names." ) + "###Show ID numbers next to zone names checkbox.", ref mConfiguration.mShowIDNumberNextToZoneNames );
				ImGuiUtils.HelpMarker( Loc.Localize( "Help: Show ID in Zone Names", "Shows the internal Content Finder ID of the zone/duty in some places.  Generally only used for debugging." ) );
				ImGui.Checkbox( Loc.Localize( "Config Option: Show Preset Indices", "Show the index of the preset within the library." ) + "###Show the index of the preset within the library checkbox", ref mConfiguration.mShowLibraryIndexInPresetList );
				ImGuiUtils.HelpMarker( Loc.Localize( "Help: Show Preset Indices", "The primary use of this is if you need to know the preset index to use within a text command.  You can always leave this disabled if you only use the GUI." ) );
				/*ImGui.Checkbox( "Allow placement of waymarks client-side in overworld zones.", ref mConfiguration.mAllowClientSidePlacementInOverworldZones );
				ImGuiUtils.HelpMarker( "Lets the plugin attempt to place waymarks in overworld zones that do not function with the game's preset interface.  These will only be visible client-side, and not to other party/alliance members.  This is out of specification behavior for the game, so please read this plugin's readme before enabling." );*/
				ImGui.Checkbox( Loc.Localize( "Config Option: Autoload Presets from Libarary", "Autoload presets from library." ) + "###Autoload presets from library checkbox", ref mConfiguration.mAutoPopulatePresetsOnEnterInstance );
				ImGuiUtils.HelpMarker( Loc.Localize( "Help: Autoload presets from Library", "Automatically loads the first five presets that exist in the library for a zone when you load into it.  THIS WILL OVERWRITE THE GAME'S SLOTS WITHOUT WARNING, so please do not turn this on until you are certain that you have saved any data that you want to keep.  Consider using this with the auto-import option below to reduce the risk of inadvertent preset loss." ) );
				ImGui.Checkbox( Loc.Localize( "Config Option: Autosave Presets to Library", "Autosave presets to library." ) + "###Autosave Presets to Library Checkbox", ref mConfiguration.mAutoSavePresetsOnInstanceLeave );
				ImGuiUtils.HelpMarker( Loc.Localize( "Help: Autosave Presets to Library", "Automatically copies any populated game preset slots into the library upon exiting an instance." ) );
				ImGui.Checkbox( String.Format( Loc.Localize( "Config Option: Suppress Text Command Responses", "Suppress responses to text commands (besides \"{0}\")." ), Plugin.SubcommandName_Help ) + "###Suppress Command Responses Checkbox", ref mConfiguration.mSuppressCommandLineResponses );
				if( ImGui.Button( Loc.Localize( "Button: Clear All Map View Data", "Clear All Map View Data" ) + "###Clear All Map View Data Button" ) )
				{
					mUI.MapWindow.ClearAllMapViewStateData();
				}
				ImGuiUtils.HelpMarker( Loc.Localize( "Help: Clear All Map View Data", "This deletes all map view pan/zoom/submap state, resetting every map back to default." ) );
				ImGui.Spacing();
				if( ImGui.Button( Loc.Localize( "Button: Save and Close", "Save and Close" ) + "###Save and Close Button" ) )
				{
					mConfiguration.Save();
					WindowVisible = false;
				}

				string showLibraryButtonString = Loc.Localize( "Button: Show Library", "Show Library" );
				ImGui.SameLine( ImGui.GetWindowContentRegionWidth() - ImGui.CalcTextSize( showLibraryButtonString ).X - ImGui.GetStyle().FramePadding.X * 2 );
				if( ImGui.Button( showLibraryButtonString + "###Show Library Button" ) )
				{
					mUI.LibraryWindow.WindowVisible = true;
				}
			}
			ImGui.End();
		}

		private bool mWindowVisible = false;
		public bool WindowVisible
		{
			get { return mWindowVisible; }
			set { mWindowVisible = value; }
		}

		private readonly PluginUI mUI;
		private readonly DalamudPluginInterface mPluginInterface;
		private readonly Configuration mConfiguration;
	}
}
