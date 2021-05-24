using Dalamud.Configuration;
using Dalamud.Plugin;
using FCNameColor;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace FCNameColor
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 0;

        public bool Enabled { get; set; } = true;
        public bool OnlyColorFCTag { get; set; } = false;
        public bool IncludeSelf { get; set; } = false;
        public Vector4 Color { get; set; } = new Vector4(204, 55, 55, 255); // The same as UiColor 704.
        public string UiColor { get; set; } = "704"; // A red-ish colour.
        public List<XivApiSearchResponseCharacter> FcMembers { get; set; } = null;


        [NonSerialized]
        private DalamudPluginInterface pluginInterface;

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;
        }

        public void Save()
        {
            pluginInterface.SavePluginConfig(this);
        }
    }
}
