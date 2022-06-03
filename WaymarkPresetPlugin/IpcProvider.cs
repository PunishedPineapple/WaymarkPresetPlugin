using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Dalamud.Logging;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;

namespace WaymarkPresetPlugin
{
	internal static class IpcProvider
	{
		public static void RegisterIPC( Plugin plugin, DalamudPluginInterface pluginInterface )
		{
			mPlugin = plugin;

			mCallGate_GetPresetsForCurrentArea = pluginInterface.GetIpcProvider<SortedDictionary<int, string>>( $"{Plugin.InternalName}.GetPresetsForCurrentArea" );
			mCallGate_GetPresetsForCurrentArea?.RegisterFunc( GetPresetsForCurrentArea );

			mCallGate_GetPresetsForTerritoryType = pluginInterface.GetIpcProvider<UInt32, SortedDictionary<int, string>>( $"{Plugin.InternalName}.GetPresetsForTerritoryType" );
			mCallGate_GetPresetsForTerritoryType?.RegisterFunc( GetPresetsForTerritoryType );

			mCallGate_GetPresetsForContentFinderCondition = pluginInterface.GetIpcProvider<UInt16, SortedDictionary<int, string>>( $"{Plugin.InternalName}.GetPresetsForContentFinderCondition" );
			mCallGate_GetPresetsForContentFinderCondition?.RegisterFunc( GetPresetsForContentFinderCondition );

			mCallGate_PlacePresetByIndex = pluginInterface.GetIpcProvider<int, bool>( $"{Plugin.InternalName}.PlacePresetByIndex" );
			mCallGate_PlacePresetByIndex?.RegisterFunc( PlacePresetByIndex );

			mCallGate_PlacePresetByName = pluginInterface.GetIpcProvider<string, bool>( $"{Plugin.InternalName}.PlacePresetByName" );
			mCallGate_PlacePresetByName?.RegisterFunc( PlacePresetByName );

			mCallGate_PlacePresetByNameAndTerritoryType = pluginInterface.GetIpcProvider<string, UInt32, bool>( $"{Plugin.InternalName}.PlacePresetByNameAndTerritoryType" );
			mCallGate_PlacePresetByNameAndTerritoryType?.RegisterFunc( PlacePresetByNameAndTerritoryType );

			mCallGate_PlacePresetByNameAndContentFinderCondition = pluginInterface.GetIpcProvider<string, UInt16, bool>( $"{Plugin.InternalName}.PlacePresetByNameAndContentFinderCondition" );
			mCallGate_PlacePresetByNameAndContentFinderCondition?.RegisterFunc( PlacePresetByNameAndContentFinderCondition );
		}

		public static void UnregisterIPC()
		{
			mCallGate_GetPresetsForCurrentArea?.UnregisterFunc();
			mCallGate_GetPresetsForTerritoryType?.UnregisterFunc();
			mCallGate_GetPresetsForContentFinderCondition?.UnregisterFunc();
			mCallGate_PlacePresetByIndex?.UnregisterFunc();
			mCallGate_PlacePresetByName?.UnregisterFunc();
			mCallGate_PlacePresetByNameAndTerritoryType?.UnregisterFunc();
			mCallGate_PlacePresetByNameAndContentFinderCondition?.UnregisterFunc();

			mCallGate_GetPresetsForCurrentArea = null;
			mCallGate_GetPresetsForTerritoryType = null;
			mCallGate_GetPresetsForContentFinderCondition = null;
			mCallGate_PlacePresetByIndex = null;
			mCallGate_PlacePresetByName = null;
			mCallGate_PlacePresetByNameAndTerritoryType = null;
			mCallGate_PlacePresetByNameAndContentFinderCondition = null;

			mPlugin = null;
		}

		private static SortedDictionary<int, string> GetPresetsForCurrentArea()
		{
			if( mPlugin == null ) return new();

			var presets = new SortedDictionary<int, string>();
			var presetIndices = mPlugin.InternalCommand_GetPresetsForCurrentArea();

			foreach( var index in presetIndices )
			{
				presets.Add( index, mPlugin.GetLibraryPresetName( index ) );
			}

			return presets;
		}

		private static SortedDictionary<int, string> GetPresetsForTerritoryType( UInt32 territoryType )
		{
			if( mPlugin == null ) return new();

			var presets = new SortedDictionary<int, string>();
			var presetIndices = mPlugin.InternalCommand_GetPresetsForTerritoryType( territoryType );

			foreach( var index in presetIndices )
			{
				presets.Add( index, mPlugin.GetLibraryPresetName( index ) );
			}

			return presets;
		}

		private static SortedDictionary<int, string> GetPresetsForContentFinderCondition( UInt16 contentFinderCondition )
		{
			if( mPlugin == null ) return new();

			var presets = new SortedDictionary<int, string>();
			var presetIndices = mPlugin.InternalCommand_GetPresetsForContentFinderCondition( contentFinderCondition );

			foreach( var index in presetIndices )
			{
				presets.Add( index, mPlugin.GetLibraryPresetName( index ) );
			}

			return presets;
		}

		private static bool PlacePresetByIndex( int index )
		{
			PluginLog.LogInformation( $"IPC request received to place a preset.  Index: {index}" );
			if( mPlugin == null ) return false;
			if( DateTimeOffset.UtcNow - mTimeOfLastPresetPlacement < mIPCPresetPlacementCooldown ) return false;
			else mTimeOfLastPresetPlacement = DateTimeOffset.UtcNow;
			return mPlugin.InternalCommand_PlacePresetByIndex( index );
		}

		private static bool PlacePresetByName( string presetName )
		{
			PluginLog.LogInformation( $"IPC request received to place a preset.  Preset Name: {presetName}" );
			if( mPlugin == null ) return false;
			if( DateTimeOffset.UtcNow - mTimeOfLastPresetPlacement < mIPCPresetPlacementCooldown ) return false;
			else mTimeOfLastPresetPlacement = DateTimeOffset.UtcNow;
			return mPlugin.InternalCommand_PlacePresetByName( presetName );
		}

		private static bool PlacePresetByNameAndTerritoryType( string presetName, UInt32 territoryType )
		{
			PluginLog.LogInformation( $"IPC request received to place a preset.  Preset Name: {presetName}, TerritoryType: {territoryType}" );
			if( mPlugin == null ) return false;
			if( DateTimeOffset.UtcNow - mTimeOfLastPresetPlacement < mIPCPresetPlacementCooldown ) return false;
			else mTimeOfLastPresetPlacement = DateTimeOffset.UtcNow;
			return mPlugin.InternalCommand_PlacePresetByNameAndTerritoryType( presetName, territoryType );
		}

		private static bool PlacePresetByNameAndContentFinderCondition( string presetName, UInt16 contentFinderCondition )
		{
			PluginLog.LogInformation( $"IPC request received to place a preset.  Preset Name: {presetName}, ContentFinderCondition: {contentFinderCondition}" );
			if( mPlugin == null ) return false;
			if( DateTimeOffset.UtcNow - mTimeOfLastPresetPlacement < mIPCPresetPlacementCooldown ) return false;
			else mTimeOfLastPresetPlacement = DateTimeOffset.UtcNow;
			return mPlugin.InternalCommand_PlacePresetByNameAndContentFinderCondition( presetName, contentFinderCondition );
		}

		private static ICallGateProvider<SortedDictionary<int, string>> mCallGate_GetPresetsForCurrentArea;
		private static ICallGateProvider<UInt32, SortedDictionary<int, string>> mCallGate_GetPresetsForTerritoryType;
		private static ICallGateProvider<UInt16, SortedDictionary<int, string>> mCallGate_GetPresetsForContentFinderCondition;

		private static ICallGateProvider<int, bool> mCallGate_PlacePresetByIndex;
		private static ICallGateProvider<string, bool> mCallGate_PlacePresetByName;
		private static ICallGateProvider<string, UInt32, bool> mCallGate_PlacePresetByNameAndTerritoryType;
		private static ICallGateProvider<string, UInt16, bool> mCallGate_PlacePresetByNameAndContentFinderCondition;

		//	Just in case someone fucks up calling our placement IPC; I don't want someone else making me try to place waymarks 60 times a second and getting people banned.
		private static DateTimeOffset mTimeOfLastPresetPlacement = DateTimeOffset.UtcNow;
		private static readonly TimeSpan mIPCPresetPlacementCooldown = new( 0, 0, 3 );	

		private static Plugin mPlugin;
	}
}
