using Dalamud.Configuration;
using Dalamud.Plugin;
using System;

namespace WaymarkPresetPlugin
{
	[Serializable]
	public class Configuration : IPluginConfiguration
	{
		//  Our own configuration options and data.
		public WaymarkPresetLibrary PresetLibrary { get; set; } = new WaymarkPresetLibrary();

		//  Plugin framework and related convenience functions below.
		public void Initialize( DalamudPluginInterface pluginInterface )
		{
			this.mPluginInterface = pluginInterface;
		}

		public void Save()
		{
			this.mPluginInterface.SavePluginConfig( this );
		}

		[NonSerialized]
		protected DalamudPluginInterface mPluginInterface;

		public int Version { get; set; } = 0;
	}
}
