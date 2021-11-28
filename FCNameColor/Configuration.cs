using Dalamud.Configuration;
using Dalamud.Plugin;
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
        public bool OnlyColorFCTag { get; set; } = true;
        public bool IncludeSelf { get; set; } = true;
        public bool IncludeDuties { get; set; } = true;
        public bool Glow { get; set; }
        public Vector4 Color { get; set; } = new(0.8f, 0.21568628f, 0.21568628f, 1.0f); // The same as UiColor 14.
        public string UiColor { get; set; } = "14"; // A red-ish colour.

        /// <summary>
        /// A mapping of character Name@Server and character IDs
        /// </summary>
        public Dictionary<string, string> PlayerIDs { get; set; } = new();

        /// <summary>
        /// A mapping of Player ID and their FC’s ID
        /// </summary>
        public Dictionary<string, FC> PlayerFCs { get; set; } = new();

        public Dictionary<string, string> IgnoredPlayers { get; set; } = new();

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
