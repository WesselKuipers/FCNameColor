using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using FCNameColor.Config;
using FCNameColor.Utils;

namespace FCNameColor.UI
{
    internal class PluginUI : Window
    {
        private readonly Plugin plugin;
        private readonly ConfigurationV1 configuration;
        
        private readonly IClientState clientState;
        private readonly IPluginLog pluginLog;

        private readonly AdditionalFCsWindow showAdditionalFCsWindow;
        private readonly IgnoreListWindow ignoreListWindow;
        private readonly AddNewGroupWindow addNewGroupWindow;
        private readonly HideNameplatesSettingsWindow hideNameplatesSettingsWindow;

        public PluginUI(
            ConfigurationV1 config,
            Plugin plugin,
            IClientState clientState,
            IPluginLog pluginLog,
            AddNewGroupWindow addNewGroupWindow,
            IgnoreListWindow ignoreListWindow,
            AdditionalFCsWindow showAdditionalFCsWindow,
            HideNameplatesSettingsWindow hideNameplatesSettingsWindow) : base("FC Name Color Config")
        {
            configuration = config;
            this.clientState = clientState;
            this.plugin = plugin;
            this.pluginLog = pluginLog;
            this.addNewGroupWindow = addNewGroupWindow;
            this.ignoreListWindow = ignoreListWindow;
            this.showAdditionalFCsWindow = showAdditionalFCsWindow;
            this.hideNameplatesSettingsWindow = hideNameplatesSettingsWindow;

            Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.AlwaysAutoResize;
            Size = new Vector2(380, 300);
            SizeCondition = ImGuiCond.FirstUseEver;
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(380, 300),
                MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
            };
        }

        public override void Draw()
        {
            if (plugin.FirstTime)
            {
                ImGui.TextColored(ImGuiColors.DalamudYellow,
                    "Plugin is setting up for the first time, please wait a moment.");
                return;
            }

            var isDirty = false;
            
            ImGuiUtils.AddCheckbox("Enabled", "Changes may take a couple of seconds to apply.", configuration.Enabled, value => configuration.Enabled = value, ref isDirty);

            if (clientState.IsPvP)
            {
                ImGui.SameLine();
                ImGui.TextColored(ImGuiColors.DalamudOrange, "Plugin is disabled during PvP");
            }
            if (plugin.NotFound)
            {
                ImGui.SameLine();
                using (ImRaii.PushColor(ImGuiCol.TextDisabled, ImGuiColors.DalamudOrange))
                    ImGuiComponents.HelpMarker("Could not find player character on Lodestone.\nIf your character is new, please wait a couple of hours for it to show up on Lodestone.\nIf your character is set to private, then the automatic FC fetching won’t work.", FontAwesomeIcon.ExclamationTriangle);
            }
            else if (plugin.NotInFC)
            {
                ImGui.SameLine();
                ImGui.TextColored(ImGuiColors.DalamudRed, "Character not in FC");
            }
            else if (plugin is { Loading: true, Error: false })
            {
                ImGui.SameLine();
                ImGui.Text(" Fetching FC members from Lodestone...");
            }
            else if (plugin.Error)
            {
                ImGui.SameLine();
                ImGui.TextColored(ImGuiColors.DalamudRed,
                    $"Error when fetching. Retrying in {plugin.Cooldown} seconds.");
            }

            ImGuiUtils.AddCheckbox("Only color the FC tag", "This will only colour the FC tag instead of the entire name.", configuration.OnlyColorFCTag, value => configuration.OnlyColorFCTag = value, ref isDirty);
            ImGuiUtils.AddCheckbox("Include self", "This will colour your own FC tag.", configuration.IncludeSelf, value => configuration.IncludeSelf = value, ref isDirty);
            ImGui.SameLine();
            ImGuiUtils.AddCheckbox("Ignore friends self", "Don't change the nameplates of friends.", configuration.IgnoreFriends, value => configuration.IgnoreFriends = value, ref isDirty);
            ImGuiUtils.AddCheckbox("Include duties", "Will colour the entire names of FC members when inside a duty", configuration.IncludeDuties, value => configuration.IncludeDuties = value, ref isDirty);
            ImGui.SameLine();
            ImGuiUtils.AddCheckbox("Only duties", "Disable the plugin outside of duties. This helps with conflicts with other plugins.", configuration.OnlyDuties, value => configuration.OnlyDuties = value, ref isDirty);

            ImGuiUtils.AddCheckbox("Enable glow", "Makes outline of the nameplates thicker.", configuration.Glow, value => configuration.Glow = value, ref isDirty);
            ImGuiUtils.AddCheckbox("Hide other nameplates",
                $"""
                 Hide other player nameplates

                 If checked, this will automatically hide any nameplates that aren't tracked.
                 Note that this requires you to change the settings in Character Configuration -> Display Name Settings.

                 Any nameplates that are already hidden by these settings will remain hidden.
                 """,
                configuration.HideOtherNameplates,
                value => configuration.HideOtherNameplates = value,
                ref isDirty);


            ImGui.SameLine();
            if (ImGui.Button("Settings"))
            {
                hideNameplatesSettingsWindow.Toggle();
            }
            
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Settings related to the conditions for hiding nameplates");
            }
            
            ImGui.Separator();

            ImGui.Text("Groups");
            ImGui.SameLine();

            if (ImGuiComponents.IconButton(FontAwesomeIcon.Plus))
            {
                addNewGroupWindow.Toggle();
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Add a new group which you can assign FCs to.");
            }

            using (var groupsPanel = ImRaii.Child("###GroupsPanel", new Vector2(ImGui.GetContentRegionAvail().X, 300.0f * ImGuiHelpers.GlobalScale)))
            {
                if (groupsPanel.Success)
                {
                    foreach (var (groupName, group) in configuration.Groups)
                    {
                        var groupColor = group.Color;
                        if (ImGui.ColorEdit4(groupName, ref groupColor, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoAlpha))
                        {
                            var newGroup = configuration.Groups[groupName];
                            newGroup.Color = groupColor;
                            configuration.Groups[groupName] = newGroup;
                            configuration.Save();
                        }

                        if (groupName is "Default" or "Other FC") continue;
                        
                        ImGui.SameLine();
                        using var id = ImRaii.PushId(groupName);
                        if (ImGuiComponents.IconButton(FontAwesomeIcon.Trash, new Vector4(0.8f, 0, 0, 1f), new Vector4(1f, 0, 0, 1f), new Vector4(0.9f, 0, 0, 1f)))
                        {
                            pluginLog.Debug($"Deleting group {groupName}");
                            configuration.Groups.Remove(groupName);

                            foreach (var playerConfigs in configuration.FCGroups)
                            {
                                foreach (var fcGroup in playerConfigs.Value)
                                {
                                    if (fcGroup.Value == groupName)
                                    {
                                        configuration.FCGroups[playerConfigs.Key][fcGroup.Key] = "Other FC";
                                    }
                                }
                            }

                            configuration.Save();
                        }

                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip($"Delete group {groupName}.\nThe groups Default and Other FC cannot be removed.");
                        }
                    }
                }
            }            

            ImGui.Separator();
            ImGui.Spacing();
            if (ImGui.Button("Ignore List"))
            {
                ignoreListWindow.Toggle();
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Select which players shouldn’t be affected.");
            }

            ImGui.SameLine();
            if (ImGui.Button("FC tracking"))
            {
                showAdditionalFCsWindow.Toggle();
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Change the group settings for your FCs and track additional FCs that aren’t your own");
            }

            ImGui.SameLine();
            if (plugin.Cooldown > 0)
            {
                ImGuiComponents.DisabledButton($"Clear & Retry ({plugin.Cooldown})");
            }
            else
            {
                if (ImGui.Button("Clear & Retry"))
                {
                    configuration.PlayerFCIDs = new Dictionary<string, string?>();
                    configuration.PlayerIDs = new Dictionary<string, string>();
                    configuration.Save();
                    showAdditionalFCsWindow.IsOpen = false;
                    plugin.SearchingFC = false;
                    plugin.Reload();
                }
            }
            
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(
                    $"""
                     Pressing this will clear the list of FC members and attempt to fetch all the necessary data from Lodestone.
                     This can be especially useful if something went wrong when loading from Lodestone, or if you’ve joined a different FC.

                     If something goes wrong trying to fetch the data, you can try again after {(plugin.Cooldown > 0 ? plugin.Cooldown : Plugin.CooldownTime)} seconds.
                     """);
            }

            if (!configuration.FCGroups.ContainsKey(plugin.PlayerKey))
            {
                configuration.FCGroups.Add(plugin.PlayerKey, new Dictionary<string, string>());
            }

            if (isDirty)
            {
                configuration.Save();
            }
        }
    }
}