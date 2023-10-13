using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Configuration;
using Dalamud.Plugin;
using Newtonsoft.Json;

namespace FCNameColor.Config
{
    [Serializable]
    public class ConfigurationV1 : IPluginConfiguration
    {
        public int Version { get; set; } = 1;
        [NonSerialized] public bool FirstTime = false;

        #region User Settings
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
        #endregion

        #region Plugin Data
        /// <summary>
        /// A mapping of character Name@Server and character IDs
        /// </summary>
        public Dictionary<string, string> PlayerIDs { get; set; } = new();

        /// <summary>
        /// A mapping of Player ID and their FC’s ID
        /// </summary>
        public Dictionary<string, string> PlayerFCIDs { get; set; } = new();

        /// <summary>
        /// A mapping of player IDs and player names to ignore processing of.
        /// </summary>
        public Dictionary<string, string> IgnoredPlayers { get; set; } = new();

        /// <summary>
        /// The list of groups that FCs can be assigned to.
        /// </summary>
        public Dictionary<string, Group> Groups { get; set; } = new()
        {
            {  DefaultGroups[0].Key, DefaultGroups[0].Value },
            {  DefaultGroups[1].Key, DefaultGroups[1].Value },
        };

        /// <summary>
        /// A list of FC groups set to a specific FC, mapped by player name.
        /// [Player@World][FC ID] => Group name
        /// </summary>
        public Dictionary<string, Dictionary<string, string>> FCGroups { get; set; } = new();

        /// <summary>
        /// Every FC currently tracked by the plugin.
        /// </summary>
        public Dictionary<string, FC> FCs { get; set; } = new();
        #endregion

        [NonSerialized] private DalamudPluginInterface pluginInterface;
        [NonSerialized]
        public static KeyValuePair<string, Group>[] DefaultGroups =
        {
            new KeyValuePair<string, Group>("Default", new Group {
                UiColor = "14", // A red-ish colour.
                Color = new Vector4(0.8f, 0.21568628f, 0.21568628f, 1.0f) // The same as UiColor 14.})
            }),
            new KeyValuePair<string, Group>("Other FC", new Group {
                UiColor = "52",
                Color = new Vector4(0.07450981f, 0.8f, 0.6392157f, 1.0f)
            })
        };

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