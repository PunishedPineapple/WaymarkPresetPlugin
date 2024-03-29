﻿using System;
using System.IO;

using Dalamud.Configuration;
using Dalamud.Plugin;

using Newtonsoft.Json;

namespace WaymarkPresetPlugin
{
	[Serializable]
	public class Configuration : IPluginConfiguration
	{
		public Configuration()
		{
			GetZoneNameDelegate = new WaymarkPreset.GetZoneNameDelegate( GetZoneNameHelperFunc );
		}

		//  Our own configuration options and data.
		public WaymarkPresetLibrary PresetLibrary { get; protected set; } = new WaymarkPresetLibrary();

		//	Need a real backing field on the following properties for use with ImGui.
		public bool mSortPresetsByZone = true;
		public bool SortPresetsByZone
		{
			get { return mSortPresetsByZone; }
			set { mSortPresetsByZone = value; }
		}

		public bool mAlwaysShowInfoPane = false;
		public bool AlwaysShowInfoPane
		{
			get { return mAlwaysShowInfoPane; }
			set { mAlwaysShowInfoPane = value; }
		}

		public bool mAllowUnselectPreset = false;
		public bool AllowUnselectPreset
		{
			get { return mAllowUnselectPreset; }
			set { mAllowUnselectPreset = value; }
		}

		public bool mFilterOnCurrentZone = false;
		public bool FilterOnCurrentZone
		{
			get { return mFilterOnCurrentZone; }
			set { mFilterOnCurrentZone = value; }
		}

		public bool mShowIDNumberNextToZoneNames = false;
		public bool ShowIDNumberNextToZoneNames
		{
			get { return mShowIDNumberNextToZoneNames; }
			set { mShowIDNumberNextToZoneNames = value; }
		}

		public bool mShowLibraryIndexInPresetList = false;
		public bool ShowLibraryIndexInPresetInfo
		{
			get { return mShowLibraryIndexInPresetList; }
			set { mShowLibraryIndexInPresetList = value; }
		}

		/*public bool mAllowClientSidePlacementInOverworldZones = false;
		public bool AllowClientSidePlacementInOverworldZones
		{
			get { return mAllowClientSidePlacementInOverworldZones && mAllowDirectPlacePreset; }
			set { mAllowClientSidePlacementInOverworldZones = value; }
		}*/

		public bool mAutoPopulatePresetsOnEnterInstance = false;
		public bool AutoPopulatePresetsOnEnterInstance
		{
			get { return mAutoPopulatePresetsOnEnterInstance; }
			set { mAutoPopulatePresetsOnEnterInstance = value; }
		}

		public bool mAutoSavePresetsOnInstanceLeave = false;
		public bool AutoSavePresetsOnInstanceLeave
		{
			get { return mAutoSavePresetsOnInstanceLeave; }
			set { mAutoSavePresetsOnInstanceLeave = value; }
		}

		public bool mSuppressCommandLineResponses = false;
		public bool SuppressCommandLineResponses
		{
			get { return mSuppressCommandLineResponses; }
			set { mSuppressCommandLineResponses = value; }
		}

		public bool mOpenAndCloseWithFieldMarkerAddon = false;
		public bool OpenAndCloseWithFieldMarkerAddon
		{
			get { return mOpenAndCloseWithFieldMarkerAddon; }
			set { mOpenAndCloseWithFieldMarkerAddon = value; }
		}

		public bool mAttachLibraryToFieldMarkerAddon = false;
		public bool AttachLibraryToFieldMarkerAddon
		{
			get { return mAttachLibraryToFieldMarkerAddon; }
			set { mAttachLibraryToFieldMarkerAddon = value; }
		}

		public bool mAllowPresetDragAndDropOrdering = true;
		public bool AllowPresetDragAndDropOrdering
		{
			get { return mAllowPresetDragAndDropOrdering; }
			set { mAllowPresetDragAndDropOrdering = value; }
		}

		public bool mAllowZoneDragAndDropOrdering = true;
		public bool AllowZoneDragAndDropOrdering
		{
			get { return mAllowZoneDragAndDropOrdering; }
			set { mAllowZoneDragAndDropOrdering = value; }
		}

		public bool mSortZonesDescending = false;
		public bool SortZonesDescending
		{
			get { return mSortZonesDescending; }
			set { mSortZonesDescending = value; }
		}

		public bool mShowLibraryZoneFilterBox = true;
		public bool ShowLibraryZoneFilterBox
		{
			get { return mShowLibraryZoneFilterBox; }
			set { mShowLibraryZoneFilterBox = value; }
		}

		public string GetZoneName( UInt16 ID )
		{
			return GetZoneNameDelegate( ID, ShowIDNumberNextToZoneNames );
		}

		[JsonIgnore]
		public WaymarkPreset.GetZoneNameDelegate GetZoneNameDelegate { get; protected set; }

		protected string GetZoneNameHelperFunc( UInt16 ID, bool showID )
		{
			string str = /*ShowDutyNames ?*/ ZoneInfoHandler.GetZoneInfoFromContentFinderID( ID ).DutyName /*: ZoneInfoHandler.GetZoneInfoFromContentFinderID( ID ).ZoneName*/;
			str += showID ? $" ({ID})" : "";
			return str;
		}

		//  Plugin framework and related convenience functions below.
		public void Initialize( DalamudPluginInterface pluginInterface )
		{
			mPluginInterface = pluginInterface;
		}

		public void Save()
		{
			mPluginInterface.SavePluginConfig( this );
		}

		public void BackupConfigFile()
		{
			string backupFolderPath = Path.Join( mPluginInterface.GetPluginConfigDirectory(), "Backups" );
			Directory.CreateDirectory( backupFolderPath );
			string backupFilePath = GetBackupFilePath( backupFolderPath, "PluginConfig", "json" );
			mPluginInterface.ConfigFile.CopyTo( backupFilePath );
		}

		internal void BackupConfigFolderFile( string fileName, string extension )
		{
			string backupFolderPath = Path.Join( mPluginInterface.GetPluginConfigDirectory(), "Backups" );
			Directory.CreateDirectory( backupFolderPath );
			if( File.Exists( Path.Join( mPluginInterface.GetPluginConfigDirectory(), $"{fileName}.{extension}" ) ) )
			{
				string backupFilePath = GetBackupFilePath( backupFolderPath, fileName, extension );
				var fileToCopy = new FileInfo( Path.Combine( mPluginInterface.GetPluginConfigDirectory(), $"{fileName}.{extension}" ) );
				fileToCopy?.CopyTo( backupFilePath );
			}
		}

		internal static string GetBackupFilePath( string folderPath, string fileName, string extension )
		{
			string fileBasePath = Path.Join( folderPath, $"{fileName}_{DateTimeOffset.UtcNow:yyyy-MM-dd_HH.mm.ssZ}" );
			string filePath = fileBasePath + $".{extension}";
			int i = 2;
			while( File.Exists( filePath ) )
			{
				filePath = fileBasePath + $"_{i}.{extension}";
				++i;
			}
			return filePath;
		}

		[NonSerialized]
		protected DalamudPluginInterface mPluginInterface;

		public int Version { get; set; } = 0;
	}
}
