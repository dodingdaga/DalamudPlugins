using Dalamud.Configuration;
using System;
using System.Collections.Generic;

namespace Copycat
{
    public class PlayerConfiguration
    {
        public String PlayerName { get; set; } = "Default";

        public bool Enabled { get; set; } = true;
        public bool TargetBack { get; set; } = true;
        public String MotionOnly  { get; set; } = "";
    }

    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 0;

        public List<PlayerConfiguration> PlayerConfigurations { get; set; } = new List<PlayerConfiguration>();

        // the below exist just to make saving less cumbersome
        public static void Initialize()
        {
        }

        public void Save()
        {
            Service.pluginInterface.SavePluginConfig(this);
        }
    }
}
