using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Configuration;
using Dalamud.Plugin;

namespace FCNameColor
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 0;

        /// <summary>
        /// Whether the plugin should do anything at all.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Whether the entire nameplate should be recoloured, or just the FC tag.
        /// </summary>
        public bool OnlyColorFCTag { get; set; } = true;

        /// <summary>
        /// Whether your own FC tag should be recoloured.
        /// </summary>
        public bool IncludeSelf { get; set; } = true;

        /// <summary>
        /// Whether friends should be recoloured as well.
        /// </summary>
        public bool IgnoreFriends { get; set; } = false;

        /// <summary>
        /// Whether nameplates should be recoloured inside duties.
        /// </summary>
        public bool IncludeDuties { get; set; } = true;

        /// <summary>
        /// Whether the plugin should only work inside duties.
        /// </summary>
        public bool OnlyDuties { get; set; } = false;

        /// <summary>
        /// Enable the glow effect on fonts.
        /// </summary>
        public bool Glow { get; set; } = false;

        /// <summary>
        /// The RGBA colour representing the currently selected colour.
        /// </summary>
        public Vector4 Color { get; set; } = new(0.8f, 0.21568628f, 0.21568628f, 1.0f); // The same as UiColor 14.

        /// <summary>
        /// The ID of the selected colour.
        /// </summary>
        public string UiColor { get; set; } = "14"; // A red-ish colour.

        /// <summary>
        /// A mapping of character Name@Server and character IDs
        /// </summary>
        public Dictionary<string, string> PlayerIDs { get; set; } = new();

        /// <summary>
        /// A mapping of Player ID and their FC’s ID
        /// </summary>
        public Dictionary<string, FC> PlayerFCs { get; set; } = new();

        /// <summary>
        /// A mapping of player IDs and player names to ignore processing of.
        /// </summary>
        public Dictionary<string, string> IgnoredPlayers { get; set; } = new();

        /// <summary>
        /// The list of groups that FCs can be assigned to.
        /// </summary>
        public Dictionary<string, Group> Groups { get; set; } = new()
        {
            {
                "Other FC", new Group
                {
                    UiColor = "52", Color = new Vector4(0.07450981f,
                        0.8f,
                        0.6392157f,
                        1f)
                }
            }
        };

        /// <summary>
        /// A list of additional FCs to track, mapped by player name.
        /// </summary>
        public Dictionary<string, List<FCConfig>> AdditionalFCs { get; set; } = new();

        [NonSerialized] private DalamudPluginInterface pluginInterface;

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