﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using System.Diagnostics;

using ImGuiNET;

namespace WaymarkPresetPlugin
{
	internal static class ImGuiUtils
	{
		public static void URLLink( string URL, string textToShow = "", bool showTooltip = true, ImFontPtr? iconFont = null )
		{
			ImGui.PushStyleColor( ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.ButtonHovered] );
			ImGui.Text( textToShow.Length > 0 ? textToShow : URL );
			ImGui.PopStyleColor();
			if( ImGui.IsItemHovered() )
			{
				ImGui.SetMouseCursor( ImGuiMouseCursor.Hand );
				if( ImGui.IsMouseClicked( ImGuiMouseButton.Left ) )
				{
					Process.Start( new ProcessStartInfo( URL ) { UseShellExecute = true } );
				}

				AddUnderline( ImGui.GetStyle().Colors[(int)ImGuiCol.ButtonHovered], 1.0f );

				if( showTooltip )
				{
					ImGui.BeginTooltip();
					if( iconFont != null )
					{
						ImGui.PushFont( iconFont.Value );
						ImGui.Text( "\uF0C1" );
						ImGui.PopFont();
						ImGui.SameLine();
					}
					ImGui.Text( URL );
					ImGui.EndTooltip();
				}
			}
			else
			{
				AddUnderline( ImGui.GetStyle().Colors[(int)ImGuiCol.Button], 1.0f );
			}
		}

		public static void AddUnderline( Vector4 color, float thickness )
		{
			Vector2 min = ImGui.GetItemRectMin();
			Vector2 max = ImGui.GetItemRectMax();
			min.Y = max.Y;
			ImGui.GetWindowDrawList().AddLine( min, max, ColorVecToUInt( color ), thickness );
		}

		public static void AddOverline( Vector4 color, float thickness )
		{
			Vector2 min = ImGui.GetItemRectMin();
			Vector2 max = ImGui.GetItemRectMax();
			max.Y = min.Y;
			ImGui.GetWindowDrawList().AddLine( min, max, ColorVecToUInt( color ), thickness );
		}

		public static void RightAlignTableText( string str )
		{
			ImGui.SetCursorPosX( ImGui.GetCursorPosX() + ImGui.GetColumnWidth() - ImGui.CalcTextSize( str ).X - ImGui.GetScrollX() - 2 * ImGui.GetStyle().ItemSpacing.X );
			ImGui.Text( str );
		}

		public static UInt32 ColorVecToUInt( Vector4 color )
		{
			return
			(uint)( color.X * 255f ) << 0 |
			(uint)( color.Y * 255f ) << 8 |
			(uint)( color.Z * 255f ) << 16 |
			(uint)( color.W * 255f ) << 24;
		}

		public static Vector4 ColorUIntToVec( UInt32 color )
		{
			return new Vector4()
			{
				X = (float)( color & 0xFF ) / 255f,
				Y = (float)( color & 0xFF00 ) / 255f,
				Z = (float)( color & 0xFF0000 ) / 255f,
				W = (float)( color & 0xFF000000 ) / 255f
			};
		}

		public static void HelpMarker( string description, bool sameLine = true, string marker = "(?)" )
		{
			if( sameLine ) ImGui.SameLine();
			ImGui.TextDisabled( marker );
			if( ImGui.IsItemHovered() )
			{
				ImGui.BeginTooltip();
				ImGui.PushTextWrapPos( ImGui.GetFontSize() * 35.0f );
				ImGui.TextUnformatted( description );
				ImGui.PopTextWrapPos();
				ImGui.EndTooltip();
			}
		}

		public const ImGuiWindowFlags OverlayWindowFlags =  ImGuiWindowFlags.NoDecoration |
															ImGuiWindowFlags.NoSavedSettings |
															ImGuiWindowFlags.NoMove |
															ImGuiWindowFlags.NoMouseInputs |
															ImGuiWindowFlags.NoFocusOnAppearing |
															ImGuiWindowFlags.NoBackground |
															ImGuiWindowFlags.NoNav;
	}
}
