using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Dalamud.Configuration;
using Dalamud.Plugin;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace FCNameColor.Config
{
    public class ConfigurationMigrator
    {
        public ConfigurationV1 GetConfig(DalamudPluginInterface pi, Dalamud.Plugin.Services.IPluginLog pluginLog)
        {
            var configPath = pi.ConfigFile.FullName;
            IPluginConfiguration savedConfig;

            if (pi.ConfigFile.Exists)
            {
                var fileText = File.ReadAllText(configPath);
                var parsedConf = JObject.Parse(fileText);
                var version = parsedConf.GetValue("Version")?.ToObject<int>();

                if (!version.HasValue)
                {
                    savedConfig = new ConfigurationV1() { FirstTime = true };
                    pi.SavePluginConfig(savedConfig);

                    return (ConfigurationV1)savedConfig;
                }

                if (version == 0)
                {
                    savedConfig = parsedConf.ToObject<Configuration>();
                }
                else
                {
                    savedConfig = parsedConf.ToObject<ConfigurationV1>();
                }
            }
            else
            {
                savedConfig = new ConfigurationV1();
                pi.SavePluginConfig(savedConfig);
            }

            if (savedConfig == null)
            {
                pluginLog.Info("Creating new configuration.");
                return new ConfigurationV1
                {
                    FirstTime = true
                };
            }

            return savedConfig.Version switch
            {
                0 => MigrateFromV0((Configuration)savedConfig, pluginLog),
                1 => (ConfigurationV1)savedConfig,
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private static ConfigurationV1 MigrateFromV0(Configuration old, Dalamud.Plugin.Services.IPluginLog pluginLog)
        {
            pluginLog.Info("Migrating from V0 to V1");
            var result = new ConfigurationV1
            {
                OnlyDuties = old.OnlyDuties,
                IncludeDuties = old.IncludeDuties,
                OnlyColorFCTag = old.OnlyColorFCTag,
                IncludeSelf = old.IncludeSelf,
                Glow = old.Glow,
                IgnoredPlayers = old.IgnoredPlayers,
                PlayerIDs = old.PlayerIDs,
                Groups = old.Groups,
                Enabled = old.Enabled,
            };
            pluginLog.Info("Imported old flags");

            foreach (var (key, value) in old.PlayerFCs)
            {
                result.PlayerFCIDs.Add(key, value.ID);
            }
            pluginLog.Info("Converted PlayerFCs");

            if (!result.Groups.ContainsKey("Default"))
            {
                result.Groups.Add("Default", new Group
                {
                    UiColor = old.UiColor, // Inherit from old settings
                    Color = old.Color
                });
                pluginLog.Info("Added Default group with UiColor {a}", old.UiColor);
            }

            if (!result.Groups.ContainsKey("Other FC"))
            {
                result.Groups.Add("Other FC", new Group
                {
                    UiColor = "52",
                    Color = new Vector4(0.07450981f, 0.8f, 0.6392157f, 1.0f)
                });
                pluginLog.Info("Added group Other FC");
            }

            var mainFCs = new Dictionary<string, FC>();
            foreach (var fc in old.PlayerFCs)
            {
                if (!mainFCs.ContainsKey(fc.Value.ID))
                {
                    mainFCs.Add(fc.Value.ID, fc.Value);
                }

                pluginLog.Info("Imported FC {id}", fc.Value.ID);

                var playerIdFound = old.PlayerIDs.Where(a => a.Value == fc.Key).ToList();
                if (playerIdFound.Count > 0)
                {
                    var playerKey = playerIdFound[0].Key;

                    // Assign all previous main FCs for players to the default group.
                    result.FCGroups[playerKey] = new()
                    {
                        [fc.Value.ID] = "Default"
                    };

                    pluginLog.Info("Assigned group Default to FC {fc} for player {player}", fc.Value.ID, playerKey);
                }
            }
            result.FCs = mainFCs;


            foreach (var (playerKey, fcConfigs) in old.AdditionalFCs)
            {
                if (!result.FCGroups.ContainsKey(playerKey))
                {
                    result.FCGroups[playerKey] = new();
                    pluginLog.Info("Created FCGroup entry for player {player}", playerKey);
                }

                foreach (var fcConfig in fcConfigs)
                {
                    result.FCGroups[playerKey][fcConfig.FC.ID] = fcConfig.Group;
                    pluginLog.Info("Assigned group {group} to FC {fc} for player {player}", fcConfig.Group, fcConfig.FC.ID, playerKey);

                    if (!result.FCs.ContainsKey(fcConfig.FC.ID))
                    {
                        result.FCs.Add(fcConfig.FC.ID, fcConfig.FC);
                        pluginLog.Info("Imported FC {id}", fcConfig.FC.ID);
                    }
                }
            }

            pluginLog.Info("Successfully migrated to V1");
            return result;
        }
    }
}
