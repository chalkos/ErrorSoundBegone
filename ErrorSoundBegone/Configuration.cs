using Dalamud.Configuration;
using Dalamud.Plugin;
using System;

namespace ErrorSoundBegone
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 0;
        public bool FilterErrorSounds { get; set; } = false;
        public bool FilterClickSounds { get; set; } = false;

        public void Save()
        {
            ErrorSoundBegone.PluginInterface.SavePluginConfig(this);
        }
    }
}
