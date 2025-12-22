using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Dalamud.Configuration;
using Dalamud.Plugin;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Dalamud.Plugin.Services;

namespace FCNameColor.Config
{
    public class ConfigurationMigrator
    {
        private IDalamudPluginInterface? pi;
        private IPluginLog? pluginLog;
        private IChatGui? chat;

        public ConfigurationV1 GetConfig(IDalamudPluginInterface pi, IPluginLog pluginLog, IChatGui chat)
        {
            this.pi = pi;
            this.pluginLog = pluginLog;
            this.chat = chat;

            var configPath = pi.ConfigFile.FullName;
            IPluginConfiguration? savedConfig;

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
                0 => MigrateFromV0((Configuration)savedConfig),
                1 => (ConfigurationV1)savedConfig,
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private ConfigurationV1 MigrateFromV0(Configuration old)
        {
            ConfigurationV1 result;

            try
            {
                pluginLog?.Info("Migrating from V0 to V1");

                var path = Path.Combine(pi?.GetPluginConfigDirectory() ?? string.Empty, "FCNameColor.v0.json");
                File.WriteAllText(Path.Combine(pi.GetPluginConfigDirectory(), "FCNameColor.v0.json"), JsonConvert.SerializeObject(old));
                pluginLog?.Info("Wrote backup at {path}", path);

                result = new ConfigurationV1
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

                pluginLog?.Info("Imported old flags");

                foreach (var (key, value) in old.PlayerFCs)
                {
                    result.PlayerFCIDs.Add(key, value.ID);
                }
                pluginLog?.Info("Converted PlayerFCs");

                if (!result.Groups.ContainsKey("Default"))
                {
                    result.Groups.Add("Default", new Group
                    {
                        UiColor = old.UiColor, // Inherit from old settings
                        Color = old.Color
                    });
                    pluginLog?.Info("Added Default group with UiColor {a}", old.UiColor);
                }

                if (!result.Groups.ContainsKey("Other FC"))
                {
                    result.Groups.Add("Other FC", new Group
                    {
                        UiColor = "52",
                        Color = new Vector4(0.07450981f, 0.8f, 0.6392157f, 1.0f)
                    });
                    pluginLog?.Info("Added group Other FC");
                }

                var mainFCs = new Dictionary<string, FC>();
                foreach (var fc in old.PlayerFCs)
                {
                    if (fc.Value.ID != null && !mainFCs.ContainsKey(fc.Value.ID))
                    {
                        mainFCs.Add(fc.Value.ID, fc.Value);
                    }

                    if (fc.Value.ID != null)
                    {
                        pluginLog?.Info("Imported FC {id}", fc.Value.ID);

                        var playerIdFound = old.PlayerIDs.Where(a => a.Value == fc.Key).ToList();
                        if (playerIdFound.Count > 0)
                        {
                            var playerKey = playerIdFound[0].Key;

                            // Assign all previous main FCs for players to the default group.
                            result.FCGroups[playerKey] = new()
                            {
                                [fc.Value.ID] = "Default"
                            };

                            pluginLog?.Info("Assigned group Default to FC {fc} for player {player}", fc.Value.ID,
                                playerKey);
                        }
                    }
                }
                result.FCs = mainFCs;

                foreach (var (playerKey, fcConfigs) in old.AdditionalFCs)
                {
                    if (!result.FCGroups.ContainsKey(playerKey))
                    {
                        result.FCGroups[playerKey] = new();
                        pluginLog?.Info("Created FCGroup entry for player {player}", playerKey);
                    }

                    foreach (var fcConfig in fcConfigs)
                    {
                        if (fcConfig.FC.ID != null)
                        {
                            result.FCGroups[playerKey][fcConfig.FC.ID] = fcConfig.Group;
                            pluginLog?.Info("Assigned group {group} to FC {fc} for player {player}", fcConfig.Group,
                                fcConfig.FC.ID, playerKey);

                            if (!result.FCs.ContainsKey(fcConfig.FC.ID))
                            {
                                result.FCs.Add(fcConfig.FC.ID, fcConfig.FC);
                                pluginLog?.Info("Imported FC {id}", fcConfig.FC.ID);
                            }
                        }
                    }
                }

                pluginLog?.Info("Successfully migrated to V1");
            }
            catch (Exception ex)
            {
                result = new ConfigurationV1();
                pluginLog?.Error("Error when migrating config, returned blank config instead.");
                pluginLog?.Error("Error: {ex}", ex.Message);
                chat?.Print("[FCNameColor]: Something went wrong when migrating the configuration to the next version.");
                chat?.Print("[FCNameColor]: A backup of your old settings has been saved, please send a feedback report with contact info included, or create an issue on GitHub.");
            }

            return result;
        }
    }
}
