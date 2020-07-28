using Dalamud.Configuration;
using Dalamud.Plugin;
using Newtonsoft.Json;
using System;

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
		public WaymarkPresetLibrary PresetLibrary { get; set; } = new WaymarkPresetLibrary();

		//	Need a real backing field on the following properties for use with ImGui.
		public bool mSortPresetsByZone = true;
		public bool SortPresetsByZone
		{
			get { return mSortPresetsByZone; }
			set { mSortPresetsByZone = value; }
		}

		public bool mShowDutyNames = true;
		public bool ShowDutyNames
		{
			get { return mShowDutyNames; }
			set { mShowDutyNames = value; }
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

		public bool mShowFilterOnCurrentZoneCheckbox = true;
		public bool ShowFilterOnCurrentZoneCheckbox
		{
			get { return mShowFilterOnCurrentZoneCheckbox; }
			set { mShowFilterOnCurrentZoneCheckbox = value; }
		}

		public bool mShowIDNumberNextToZoneNames = false;
		public bool ShowIDNumberNextToZoneNames
		{
			get { return mShowIDNumberNextToZoneNames; }
			set { mShowIDNumberNextToZoneNames = value; }
		}

		public string GetZoneName( UInt16 ID )
		{
			return GetZoneNameDelegate( ID, ShowIDNumberNextToZoneNames );
		}

		[JsonIgnore]
		public WaymarkPreset.GetZoneNameDelegate GetZoneNameDelegate { get; protected set; }

		protected string GetZoneNameHelperFunc( UInt16 ID, bool showID )
		{
			string str = ShowDutyNames ? ZoneInfoHandler.GetZoneInfoFromContentFinderID( ID ).DutyName : ZoneInfoHandler.GetZoneInfoFromContentFinderID( ID ).ZoneName;
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

		[NonSerialized]
		protected DalamudPluginInterface mPluginInterface;

		public int Version { get; set; } = 0;
	}
}
