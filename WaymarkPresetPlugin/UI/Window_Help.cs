using System;
using System.ComponentModel;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;

using CheapLoc;

using Dalamud.Interface;
using Dalamud.Plugin;

using ImGuiNET;

using ImGuiScene;

namespace WaymarkPresetPlugin
{
	internal sealed class Window_Help : IDisposable
	{
		public Window_Help( PluginUI UI, DalamudPluginInterface pluginInterface, Configuration configuration, IntPtr pEditWaymarkCoordDragAndDropData )
		{
			mUI = UI;
			mConfiguration = configuration;
			mpEditWaymarkCoordDragAndDropData = pEditWaymarkCoordDragAndDropData;
			mCoordinateSystemsDiagram = pluginInterface.UiBuilder.LoadImage( Path.Join( pluginInterface.AssemblyLocation.DirectoryName, "Resources\\CoordinateSystemDiagrams.png" ) );
		}

		public void Dispose()
		{
			mCoordinateSystemsDiagram?.Dispose();
		}

		public void Draw()
		{
			if( !WindowVisible )
			{
				return;
			}

			ImGui.SetNextWindowSizeConstraints( new( Math.Max( 200f, mHelpWindowMinWidth ), 300f ), new( float.MaxValue ) );
			ImGuiHelpers.SetNextWindowPosRelativeMainViewport( ImGuiHelpers.MainViewport.Size / 3f, ImGuiCond.FirstUseEver );
			if( ImGui.Begin( Loc.Localize( "Window Title: Waymark Help", "Waymark Help" ) + "###Waymark Help", ref mWindowVisible ) )
			{
				var cachedCurrentHelpPage = mCurrentHelpPage;
				for( int i = 0; i < Enum.GetValues( typeof( HelpWindowPage ) ).Length; ++i )
				{
					if( i > 0 ) ImGui.SameLine();
					if( i == (int)cachedCurrentHelpPage )
					{
						ImGui.PushStyleColor( ImGuiCol.Button, ImGui.GetStyle().Colors[(int)ImGuiCol.ButtonHovered] );
					}
					if( ImGui.Button( ((HelpWindowPage)i).GetTranslatedName() + $"###HelpButton_{(HelpWindowPage)i}" ) )
					{
						mCurrentHelpPage = (HelpWindowPage)i;
					}
					if( i == (int)cachedCurrentHelpPage )
					{
						ImGui.PopStyleColor();
					}
				}
				mHelpWindowMinWidth = ImGui.GetItemRectMax().X - ImGui.GetWindowPos().X + ImGui.GetStyle().WindowPadding.X;

				if( ImGui.BeginChild( "###Help Text Pane" ) )
				{
					ImGui.PushTextWrapPos( ImGui.GetContentRegionAvail().X );
					switch( mCurrentHelpPage )
					{
						case HelpWindowPage.General: DrawHelpWindow_General(); break;
						case HelpWindowPage.Editing: DrawHelpWindow_Editing(); break;
						case HelpWindowPage.Maps: DrawHelpWindow_Maps(); break;
						case HelpWindowPage.Coordinates: DrawHelpWindow_Coordinates(); break;
						case HelpWindowPage.CircleCalculator: DrawHelpWindow_CircleCalculator(); break;
						default: DrawHelpWindow_General(); break;
					};
					ImGui.PopTextWrapPos();
					ImGui.EndChild();
				}
			}

			ImGui.End();
		}

		private void DrawHelpWindow_General()
		{
			ImGui.Text( Loc.Localize( "Help Window Text: General 1",
						"All presets in this plugin's list are fully separate from the game's presets.  This allows you to store an " +
						"unlimited number of presets, as well as to easily back up and share them, or import presets that others have shared with you." ) );
			ImGui.Spacing();
			ImGui.Spacing();
			ImGui.Spacing();
			ImGui.Text( Loc.Localize( "Help Window Text: General 2",
						"Selecting a preset in the library will show a window to the side with information about that preset, such as where the waymarks " +
						"are placed, as well as actions that you can take with that preset." ) );
			ImGui.Spacing();
			ImGui.Spacing();
			ImGui.Spacing();
			ImGui.Text( Loc.Localize( "Help Window Text: General 3",
						"If you want to copy a preset in the " +
						"library to a game slot, select that preset in the list, and then press the button with the number of the slot to " +
						"which you want to copy it.  If you want to import a preset from the game's list into the library, scroll down to " +
						"\"Import Options\" and press the button of the slot that you wish to import from the game.  This is also where you " +
						"can paste presets to import them from outside of the game." ) );
			ImGui.Spacing();
			ImGui.Spacing();
			ImGui.Spacing();
			ImGui.Text( Loc.Localize( "Help Window Text: General 4",
						"If you wish to share a preset with someone else, you can select the preset in the library, and " +
						"click the \"Export to Clipboard\" button." ) );
			ImGui.Spacing();
			ImGui.Spacing();
			ImGui.Spacing();
			ImGui.Text( Loc.Localize( "Help Window Text: General 5",
						"The plugin also allows you to place and save waymarks directly to/from the field.  These are what the \"Place\" and " +
						"\"Save Current Waymarks\" buttons do.  Please note that saving and placing presets is only supported in areas that " +
						"the game allows with its built in system.  Saving presets outside of those duties will result in a preset that shows an " +
						"unknown zone.  Trying to place presets outside of those duties will simply fail to do anything." ) );
			ImGui.Spacing();
			ImGui.Spacing();
			ImGui.Spacing();
			ImGui.Text( Loc.Localize( "Help Window Text: General 6",
						"Presets can be reordered in the library by dragging and dropping them at the desired spot.  The sorting of duties in " +
						"the library is by default the same as they are sorted in the game's files, and is approximately " +
						"the order in which the duties were added to the game.  Duties can be drag and drop reordered if desired.  If you create your own " +
						"order by doing this, newly-seen duties will appear at the bottom of the list." ) );
		}

		private void DrawHelpWindow_Editing()
		{
			ImGui.PushStyleColor( ImGuiCol.Text, 0xee4444ff );
			ImGui.Text( Loc.Localize( "Help Window Text: Editing Warning Message", "SE has banned people for placing out of bounds waymarks.  Please use caution when manually editing waymark coordinates." ) );
			ImGui.PopStyleColor();
			ImGui.Spacing();
			ImGui.Text( Loc.Localize( "Help Window Text: Editing 1",
						"Clicking the \"Edit\" button in the preset info pane will bring up a window that allows you to " +
						"edit a preset.  You can adjust any of the available parameters, and you can drag waymarks on to " +
						"other waymarks to swap their positions.  You can also drag points from the circle calculator tab " +
						"in this help window on to a waymark in the editor window to replace its coordinates with the ones from that calculator." ) );
			ImGui.Spacing();
			ImGui.Spacing();
			ImGui.Spacing();
			ImGui.Text( Loc.Localize( "Help Window Text: Editing 2",
						"Changes made in the editor window will not be applied until the \"Save\" button is clicked." ) );
		}

		private void DrawHelpWindow_Maps()
		{
			ImGui.Text( Loc.Localize( "Help Window Text: Maps 1",
						"The \"Map View\" window displays a copy of the applicable map(s) for the selected preset's duty.  Any placed " +
						"waymarks are shown on the map.  If a zone has multiple submaps, you can switch between submaps using the dropdown " +
						"in the lower right corner of the window.  The world cordinates corresponding to your cursor position on the map are " +
						"shown at the bottom right of the window.  Please read the \"Coordinates\" tab of this help window if you wish to understand " +
						"the game's internal coordinate systems, and their relationship to what is presented in-game to the player." ) );
			ImGui.Spacing();
			ImGui.Spacing();
			ImGui.Spacing();
			ImGui.Text( Loc.Localize( "Help Window Text: Maps 2",
						"When editing a preset, you can drag waymark icons on the map to adjust their positions.  While you are doing this, the " +
						"coordinate readout reflects the position of the marker, and not the position of your mouse.  Please note that editing " +
						"waymarks in this manner is not advised in areas that have uneven ground, as it is not possible to automatically adjust " +
						"the Y coordinate to match the terrain." ) );
			ImGui.Spacing();
			ImGui.Spacing();
			ImGui.Spacing();
			ImGui.Text( Loc.Localize( "Help Window Text: Maps 3",
						"Please also note that the plugin cannot currently determine which waymarks are present on which submaps, so all waymarks " +
						"are shown at their positions on all submaps (provided that they are within the map's bounds).  For some rare cases (like e12s), " +
						"the default submap is not the one in which you enter the area, so you will need to manually select the correct submap." ) );
		}

		private void DrawHelpWindow_Coordinates()
		{
			ImGui.Text( Loc.Localize( "Help Window Text: Coordinates 1", "Coordinate Systems:" ) + "\r\n" );
			ImGui.Spacing();
			ImGui.Indent();
			ImGui.Text( Loc.Localize( "Help Window Text: Coordinates 2",
						"The game internally uses a right-handed 3D coordinate system, " +
						"with X running West to East, Y running down to up, and Z running North to South.  The on-map " +
						"coordinate system is a 2D projection of the XZ plane, with X running West to East, and Y running " +
						"North to South.  Please note that the coordinates presented in chat links or on the map widgets " +
						"in game are scaled to arbitrary values, and the Y and Z axes are swapped.  This plugin uses the " +
						"game's internal coordinate systems as shown below:" ) );
			ImGui.Unindent();
			ImGui.Spacing();
			ImGui.Spacing();
			ImGui.Spacing();
			if( mCoordinateSystemsDiagram != null )
			{
				const float imgWidthScale = 0.75f;
				const float imguiPaddingScale = 1.0f - imgWidthScale;
				ImGui.Indent( ImGui.GetContentRegionAvail().X * imguiPaddingScale / 2f );
				var size = new Vector2( mCoordinateSystemsDiagram.Width, mCoordinateSystemsDiagram.Height );
				size *= ImGui.GetContentRegionAvail().X / mCoordinateSystemsDiagram.Width * imgWidthScale;
				ImGui.Image( mCoordinateSystemsDiagram.ImGuiHandle, size );
				ImGui.Unindent();
			}
		}

		private void DrawHelpWindow_CircleCalculator()
		{
			ImGui.PushStyleColor( ImGuiCol.Text, 0xee4444ff );
			ImGui.Text( Loc.Localize( "Help Window Text: Circle Computer Warning Message", "SE has banned people for placing out of bounds waymarks.  Please use caution when creating or editing a preset using this calculator." ) );
			ImGui.PopStyleColor();
			ImGui.Spacing();
			ImGui.Text( Loc.Localize( "Circle Computer Text: Instructions 1",
						"This calculator will compute radially symmetric points (\"clock spots\") with the information that you " +
						"give it.  You can then drag these into the preset editor to replace any waymarks with the calculated points, " +
						"or you can use the buttons at the bottom of this pane." ) );

			ImGui.Spacing();
			ImGui.Spacing();
			ImGui.Spacing();
			ImGui.Spacing();
			ImGui.Spacing();

			ImGui.InputFloat3( Loc.Localize( "Circle Computer Text: Center Position", "Center Position" ) + "###Center Position", ref mCircleComputer_Center );
			ImGui.InputFloat( Loc.Localize( "Circle Computer Text: Radius", "Radius (y)" ) + "###Radius (y)", ref mCircleComputer_Radius_Yalms );
			ImGui.SliderInt( Loc.Localize( "Circle Computer Text: Number of Points", "Number of Points" ) + "##Number of Points", ref mCircleComputer_NumPoints, 1, 8 );
			ImGui.InputFloat( Loc.Localize( "Circle Computer Text: Angle Offset", "Angle Offset (deg)" ) + "###Angle Offset (deg)", ref mCircleComputer_AngleOffset_Deg );

			ImGui.Spacing();
			ImGui.Spacing();
			ImGui.Spacing();
			ImGui.Spacing();
			ImGui.Spacing();

			var points = ComputeRadialPositions( mCircleComputer_Center, mCircleComputer_Radius_Yalms, mCircleComputer_NumPoints, mCircleComputer_AngleOffset_Deg );

			for( int i = 0; i < 8; ++i )
			{
				if( i < points.Length )
				{
					ImGui.Selectable( $"{i + 1}: {points[i].X:F3}, {points[i].Y:F3}, {points[i].Z:F3}" );
					if( ImGui.BeginDragDropSource( ImGuiDragDropFlags.None ) )
					{
						ImGui.SetDragDropPayload( $"EditPresetCoords", mpEditWaymarkCoordDragAndDropData, (uint)Marshal.SizeOf<Vector3>() );
						Marshal.StructureToPtr( points[i], mpEditWaymarkCoordDragAndDropData, true );
						ImGui.Text( Loc.Localize( "Drag and Drop Preview: Circle Computer Waymark", "Copy coordinates to..." ) );
						ImGui.EndDragDropSource();
					}
				}
				else
				{
					ImGui.Text( "---" );
				}
			}

			ImGui.Spacing();
			ImGui.Spacing();
			ImGui.Spacing();
			ImGui.Spacing();
			ImGui.Spacing();
			//***** TODO: Maybe draw a diagram of these points. *****

			string copyIntoEditorButtonText = Loc.Localize( "Button: Copy Points from Circle Computer", "Copy these points into the editor" );
			if( mUI.EditorWindow.EditingPreset )
			{
				if( ImGui.Button( copyIntoEditorButtonText + "###Copy these points into the editor button" ) )
				{
					for( int i = 0; i < points.Length && i < 8; ++i )
					{
						mUI.EditorWindow.ScratchEditingPreset.SetWaymark( i, true, points[i] );
					}
				}
			}
			else
			{
				ImGui.PushStyleColor( ImGuiCol.Button, ImGui.GetStyle().Colors[(int)ImGuiCol.Button] * 0.5f );
				ImGui.PushStyleColor( ImGuiCol.ButtonHovered, ImGui.GetStyle().Colors[(int)ImGuiCol.Button] );
				ImGui.PushStyleColor( ImGuiCol.ButtonActive, ImGui.GetStyle().Colors[(int)ImGuiCol.Button] );
				ImGui.PushStyleColor( ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled] );

				ImGui.Button( copyIntoEditorButtonText + "###Copy these points into the editor button" );

				ImGui.PopStyleColor();
				ImGui.PopStyleColor();
				ImGui.PopStyleColor();
				ImGui.PopStyleColor();
			}

			if( ImGui.Button( Loc.Localize( "Button: Create Preset from Circle Computer", "Create a new preset using these points" ) + "###Create a new preset using these points" ) )
			{
				WaymarkPreset newPreset = new();
				newPreset.Name = Loc.Localize( "Default Preset Name - Circle Computer", "Imported from Circle Computer" );
				for( int i = 0; i < points.Length && i < 8; ++i )
				{
					newPreset[i].Active = true;
					newPreset[i].SetCoords( points[i] );
				}

				int newPresetIndex = mConfiguration.PresetLibrary.ImportPreset( newPreset );
				if( !mUI.EditorWindow.EditingPreset && newPresetIndex >= 0 && newPresetIndex < mConfiguration.PresetLibrary.Presets.Count )
				{
					mUI.LibraryWindow.TrySetSelectedPreset( newPresetIndex );
				}
			}
		}

		private Vector3[] ComputeRadialPositions( Vector3 center, float radius_Yalms, int numPoints, float angleOffset_Deg = 0f )
		{
			//	Can't have less than one point (even that doesn't make much sense, but it's technically allowable).
			numPoints = Math.Max( 1, numPoints );
			var computedPoints = new Vector3[numPoints];

			//	Zero azimuth is facing North (90 degrees)
			angleOffset_Deg -= 90f;
			double stepAngle_Deg = 360.0 / numPoints;

			//	Compute the coordinates on the circle about the center point.
			for( int i = 0; i < numPoints; ++i )
			{
				//	Because of FFXIV's coordinate system, we need to go backward in angle.
				double angle_Rad = ( i * stepAngle_Deg + angleOffset_Deg ) * Math.PI / 180.0;
				computedPoints[i].X = (float)Math.Cos( angle_Rad );
				computedPoints[i].Z = (float)Math.Sin( angle_Rad );
				computedPoints[i] *= radius_Yalms;
				computedPoints[i] += center;
			}

			return computedPoints;
		}

		public void OpenHelpWindow( HelpWindowPage page )
		{
			mCurrentHelpPage = page;
			WindowVisible = true;
		}

		private bool mWindowVisible = false;
		public bool WindowVisible
		{
			get { return mWindowVisible; }
			set { mWindowVisible = value; }
		}

		private readonly PluginUI mUI;
		private readonly Configuration mConfiguration;

		private Vector3 mCircleComputer_Center = Vector3.Zero;
		private float mCircleComputer_Radius_Yalms = 20f;
		private int mCircleComputer_NumPoints = 8;
		private float mCircleComputer_AngleOffset_Deg = 0f;
		private TextureWrap mCoordinateSystemsDiagram;
		private HelpWindowPage mCurrentHelpPage = HelpWindowPage.General;
		private readonly IntPtr mpEditWaymarkCoordDragAndDropData;
		private float mHelpWindowMinWidth;
	}

	public enum HelpWindowPage : int
	{
		General,
		Editing,
		Maps,
		Coordinates,
		CircleCalculator
	}

	public static class HelpWindowPageExtensions
	{
		public static string GetTranslatedName( this HelpWindowPage value )
		{
			return value switch
			{
				HelpWindowPage.General => Loc.Localize( "Header: Help Window Page - General", "General" ),
				HelpWindowPage.Editing => Loc.Localize( "Header: Help Window Page - Editing", "Editing" ),
				HelpWindowPage.Maps => Loc.Localize( "Header: Help Window Page - Maps", "Maps" ),
				HelpWindowPage.Coordinates => Loc.Localize( "Header: Help Window Page - Coordinates", "Coordinates" ),
				HelpWindowPage.CircleCalculator => Loc.Localize( "Header: Help Window Page - Circle Calculator", "Circle Calculator" ),
				_ => throw new InvalidEnumArgumentException( $"Unrecognized \"HelpWindowPage\" Enum value \"{value}\"." ),
			};
		}
	}
}
