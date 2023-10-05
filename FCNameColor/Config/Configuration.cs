using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Configuration;
using Dalamud.Plugin;

namespace FCNameColor
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 0;

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

        ///// <summary>
        ///// The RGBA colour representing the currently selected colour.
        ///// </summary>
        //public Vector4 Color { get; set; } = new(0.8f, 0.21568628f, 0.21568628f, 1.0f); // The same as UiColor 14.

        ///// <summary>
        ///// The ID of the selected colour.
        ///// </summary>
        //public string UiColor { get; set; } = "14"; // A red-ish colour.
        #endregion

        #region Plugin Data
        /// <summary>
        /// A mapping of character Name@Server and character IDs
        /// </summary>
        public Dictionary<string, string> PlayerIDs { get; set; } = new();

        /// <summary>
        /// A mapping of Player ID and their FC’s ID
        /// </summary>
        public Dictionary<string, string> PlayerFCs { get; set; } = new();

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
                "Default", new Group
                { 
                    UiColor = "14", // A red-ish colour.
                    Color = new Vector4(0.8f, 0.21568628f, 0.21568628f, 1.0f) // The same as UiColor 14.
                }
            }
        };

        /// <summary>
        /// A list of FC groups set to a specific FC, mapped by player name.
        /// [Player@World][FC ID] => Group name
        /// </summary>
        public Dictionary<string, Dictionary<string, string>> FCGroups{ get; set; } = new();

        /// <summary>
        /// Every FC currently tracked by the plugin.
        /// </summary>
        public Dictionary<string, FC> FCs { get; set; } = new();
        #endregion

        [NonSerialized] private DalamudPluginInterface pluginInterface;

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;
        }

        public void MigrateFromV0(ConfigurationV0 old)
        {
            var allFCs = new Dictionary<string, FC>();
            foreach (var fc in old.PlayerFCs)
            {
                allFCs.Add(fc.Value.ID, fc.Value);
            }
            //foreach (var additionalFCList in old.AdditionalFCs.Values)
            //{
            //    foreach (var fc in additionalFCList)
            //    {
            //        if (!allFCs.ContainsKey(fc.ID))
            //        {
            //            //allFCs.Add(fc.ID, fc.f);
            //        }
            //    }
            //}

            Version = 2;


            Save();
        }

        public void Save()
        {
            pluginInterface.SavePluginConfig(this);
        }
    }
}