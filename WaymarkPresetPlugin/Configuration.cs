using Dalamud.Configuration;
using Dalamud.Plugin;
using System;

namespace WaymarkPresetPlugin
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 0;

        //*****TODO: Delete when UI drawing figured out.*****
        public bool SomePropertyToBeSavedAndWithADefault { get; set; } = false;

        //  This makes things messier because you have to then worry about when new zones were added.  Maybe just rely on users to not put in nonsense.
        //public DateTimeOffset EarliestPresetTimestampAllowed { get; set; } = new DateTimeOffset( 2020, 2, 18, 10, 00, 00, new TimeSpan( 0 ) );	//	Time of servers up for patch 5.2.

        public WaymarkPresetLibrary PresetLibrary { get; set; } = new WaymarkPresetLibrary();

        // the below exist just to make saving less cumbersome

        [NonSerialized]
        private DalamudPluginInterface pluginInterface;

        public void Initialize( DalamudPluginInterface pluginInterface )
        {
            this.pluginInterface = pluginInterface;
        }

        public void Save()
        {
            this.pluginInterface.SavePluginConfig( this );
        }
    }
}
