using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Windows;
using System.Security.Policy;
using Newtonsoft.Json;

namespace WaymarkPresetPlugin
{
	// It is good to have this be disposable in general, in case you ever need it
	// to do any cleanup
	public class PluginUI : IDisposable
	{
		//	Construction
		public PluginUI( Configuration configuration, Dictionary<UInt16, string> zoneNames, MemoryHandler gameMemoryHandler )
		{
			mConfiguration = configuration;
			ZoneNames = zoneNames;
			mGameMemoryHandler = gameMemoryHandler;
		}

		//	Destruction
		public void Dispose()
		{
		}

		public void Draw()
		{
			//	Draw the sub-windows.
			DrawMainWindow();
			DrawInfoWindow();
			DrawSettingsWindow();
		}

		protected void DrawMainWindow()
		{
			if( !MainWindowVisible )
			{
				return;
			}

			//	Draw the window.
			ImGui.SetNextWindowSize( new Vector2( 375, 330 ), ImGuiCond.FirstUseEver );
			ImGui.SetNextWindowSizeConstraints( new Vector2( 375, 330 ), new Vector2( float.MaxValue, float.MaxValue ) );
			if( ImGui.Begin( "Waymark Library", ref mMainWindowVisible, ImGuiWindowFlags.NoCollapse ) )
			{
				//	Populate the preset list
				ImGui.BeginGroup();
				var dict = mConfiguration.PresetLibrary.GetSortedIndices();
				if( dict.Count > 0 )
				{
					foreach( KeyValuePair<UInt16, List<int>> zone in dict )
					{
						if( ImGui.CollapsingHeader( ZoneNames.ContainsKey( zone.Key ) ? ZoneNames[zone.Key].ToString() : "Unknown Zone" ) )
						{
							foreach( int index in zone.Value )
							{
								//	Unfortunately, ImGui seems to track selectables by the label string, so we will insert the index to make each one have a unique label.
								if( ImGui.Selectable( index.ToString() + ": " + mConfiguration.PresetLibrary.Presets[index].Name, index == SelectedPreset ) )
								{
									SelectedPreset = index;
								}
							}
						}
					}
				}
				else
				{
					ImGui.Text( "Preset library empty!" );
				}
				ImGui.EndGroup();

				//	Input box for import.
				ImGui.Spacing();
				ImGui.Spacing();
				ImGui.Spacing();
				ImGui.Spacing();
				ImGui.Spacing();
				if( ImGui.CollapsingHeader( "Import Options" ) )
				{
					ImGui.BeginGroup();	//Buttons don't seem to work under a header without being in a group.
					ImGui.InputText( "", ref mPresetImportString, 1024 );   //Most exports max out around 500 characters with all waymarks, so this leaves heaps of room for a long name.
					if( ImGui.Button( "Import JSON" ) )
					{
						if( mConfiguration.PresetLibrary.ImportPreset( PresetImportString ) >= 0 )
						{
							PresetImportString = "";
						}
					}
					ImGui.SameLine();
					ImGui.Text( " or slot " );
					ImGui.SameLine();
					if( ImGui.Button( "1" ) )
					{
						mConfiguration.PresetLibrary.ImportPreset( mGameMemoryHandler.ReadSlot( 1 ) );
					}
					ImGui.SameLine();
					if( ImGui.Button( "2" ) )
					{
						mConfiguration.PresetLibrary.ImportPreset( mGameMemoryHandler.ReadSlot( 2 ) );
					}
					ImGui.SameLine();
					if( ImGui.Button( "3" ) )
					{
						mConfiguration.PresetLibrary.ImportPreset( mGameMemoryHandler.ReadSlot( 3 ) );
					}
					ImGui.SameLine();
					if( ImGui.Button( "4" ) )
					{
						mConfiguration.PresetLibrary.ImportPreset( mGameMemoryHandler.ReadSlot( 4 ) );
					}
					ImGui.SameLine();
					if( ImGui.Button( "5" ) )
					{
						mConfiguration.PresetLibrary.ImportPreset( mGameMemoryHandler.ReadSlot( 5 ) );
					}
					ImGui.EndGroup();
				}
			}

			//	Store the position and size so that we can keep the companion info window in the right place.
			MainWindowPos = ImGui.GetWindowPos();
			MainWindowSize = ImGui.GetWindowSize();

			//	We're done.
			ImGui.End();
		}

		protected void DrawInfoWindow()
		{
			ImGui.SetNextWindowSize( new Vector2( 375, 330 ), ImGuiCond.FirstUseEver );
			ImGui.SetNextWindowSizeConstraints( new Vector2( 250, 330 ), new Vector2( 250, MainWindowSize.Y ) );
			ImGui.SetNextWindowPos( new Vector2( MainWindowPos.X + MainWindowSize.X, MainWindowPos.Y ) );
			if( MainWindowVisible && ImGui.Begin( "Preset Info", ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize /*| ImGuiWindowFlags.NoTitleBar*/ ) )
			{
				if( SelectedPreset >= 0 && SelectedPreset < mConfiguration.PresetLibrary.Presets.Count )
				{
					ImGui.BeginGroup();
					ImGui.Text( "Copy to slot:" );
					ImGui.SameLine();
					ImGui.BeginGroup();
					if( ImGui.Button( "1" ) )
					{
						mGameMemoryHandler.WriteSlot( 1, mConfiguration.PresetLibrary.Presets[SelectedPreset].ConstructGamePreset() );
					}
					ImGui.SameLine();
					if( ImGui.Button( "2" ) )
					{
						mGameMemoryHandler.WriteSlot( 2, mConfiguration.PresetLibrary.Presets[SelectedPreset].ConstructGamePreset() );
					}
					ImGui.SameLine();
					if( ImGui.Button( "3" ) )
					{
						mGameMemoryHandler.WriteSlot( 3, mConfiguration.PresetLibrary.Presets[SelectedPreset].ConstructGamePreset() );
					}
					ImGui.SameLine();
					if( ImGui.Button( "4" ) )
					{
						mGameMemoryHandler.WriteSlot( 4, mConfiguration.PresetLibrary.Presets[SelectedPreset].ConstructGamePreset() );
					}
					ImGui.SameLine();
					if( ImGui.Button( "5" ) )
					{
						mGameMemoryHandler.WriteSlot( 5, mConfiguration.PresetLibrary.Presets[SelectedPreset].ConstructGamePreset() );
					}
					ImGui.EndGroup();
					ImGui.Text( "Preset Info:" );
					ImGui.Text( mConfiguration.PresetLibrary.Presets[SelectedPreset].GetPresetDataString() );
					ImGui.Spacing();
					ImGui.Spacing();
					ImGui.Spacing();
					ImGui.Spacing();
					ImGui.Spacing();
					if( ImGui.Button( "Export to Clipboard" ) )
					{
						if( SelectedPreset >= 0 && SelectedPreset < mConfiguration.PresetLibrary.Presets.Count )
						{
							Clipboard.SetText( JsonConvert.SerializeObject( mConfiguration.PresetLibrary.Presets[SelectedPreset] ) );
						}
					}
					ImGui.SameLine();
					ImGui.PushStyleColor( ImGuiCol.Text, 0xee4444ff );
					if( ImGui.Button( "Delete Preset" ) )
					{
						mConfiguration.PresetLibrary.DeletePreset( SelectedPreset );
					}
					ImGui.PopStyleColor();
				}
				else
				{
					ImGui.Text( "No preset selected." );
				}
			}

			ImGui.End();
		}

		protected void DrawSettingsWindow()
		{
			if( !SettingsWindowVisible )
			{
				return;
			}

			ImGui.SetNextWindowSize( new Vector2( 232, 75 ), ImGuiCond.Always );
			if( ImGui.Begin( "Waymark Settings", ref this.mSettingsWindowVisible,
				ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse ) )
			{
				ImGui.Text( "No settings currently." );
			}
			ImGui.End();
		}

		protected void CopyPresetToGameSlot( WaymarkPreset preset, int slot )
		{
			if( slot >= 1 && slot <= 5 )
			{
				byte[] gamePresetData = preset.ConstructGamePreset();
				if( gamePresetData.Length == 104 )
				{
					try
					{
						mGameMemoryHandler.WriteSlot( slot, gamePresetData );
					}
					catch( Exception e )
					{
					}
				}
			}
		}

		protected Configuration mConfiguration;
		protected MemoryHandler mGameMemoryHandler;

		//	Need a real backing field on the following properties for use with ImGui.
		protected bool mMainWindowVisible = false;
		public bool MainWindowVisible
		{
			get { return mMainWindowVisible; }
			set { mMainWindowVisible = value; }
		}

		protected bool mSettingsWindowVisible = false;
		public bool SettingsWindowVisible
		{
			get { return mSettingsWindowVisible; }
			set { mSettingsWindowVisible = value; }
		}

		protected string mPresetImportString = "";
		public string PresetImportString
		{
			get { return mPresetImportString; }
			set { mPresetImportString = value; }
		}

		public Vector2 MainWindowPos { get; protected set; }
		public Vector2 MainWindowSize { get; protected set; }

		public int SelectedPreset { get; protected set; } = -1;
		public Dictionary<UInt16, string> ZoneNames { get; protected set; }
	}
}
