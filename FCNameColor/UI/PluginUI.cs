using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using FCNameColor.Config;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;

namespace FCNameColor
{
    internal class UIColorComparer : IEqualityComparer<UIColor>
    {
        public bool Equals(UIColor x, UIColor y)
        {
            return x?.UIForeground == y?.UIForeground; // based on variable i
        }

        public int GetHashCode(UIColor obj)
        {
            return obj.UIForeground.GetHashCode(); // hashcode of variable to compare
        }
    }

    internal class PluginUI : IDisposable
    {
        private readonly Plugin plugin;
        private readonly ConfigurationV1 configuration;
        private bool showIgnoreList;
        private bool showAdditionalFCConfig;
        private bool showAddAdditionalFC;
        private string fcUrl = "";
        private FCMember currentIgnoredPlayer;
        private readonly IClientState clientState;
        private readonly IPluginLog pluginLog;

        private readonly Regex fcUrlPattern =
            new Regex(@"https:\/\/(eu|na|jp).finalfantasyxiv.com\/lodestone\/freecompany\/(\d{19})\/*");

        // this extra bool exists for ImGui, since you can't ref a property
        private bool visible;
        private string newGroup;
        private bool showAddNewGroup;

        public bool Visible
        {
            get => visible;
            set => visible = value;
        }

        public PluginUI(ConfigurationV1 config, IDataManager data, Plugin plugin, IClientState clientState, IPluginLog pluginLog)
        {
            configuration = config;
            this.clientState = clientState;
            this.plugin = plugin;
            this.pluginLog = pluginLog;
        }

        public void Dispose()
        {
        }

        public void Draw()
        {
            DrawMainWindow();
        }

        private void DrawMainWindow()
        {
            if (!Visible)
            {
                return;
            }

            ImGui.SetNextWindowSize(new Vector2(380, 300), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSizeConstraints(new Vector2(380, 300), new Vector2(float.MaxValue, float.MaxValue));
            if (ImGui.Begin("FC Name Color Config", ref visible,
                    ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
                    ImGuiWindowFlags.AlwaysAutoResize))
            {
                if (plugin.FirstTime)
                {
                    ImGui.TextColored(ImGuiColors.DalamudYellow,
                        "Plugin is setting up for the first time, please wait a moment.");
                    ImGui.End();
                    return;
                }

                var playerKey = plugin.PlayerKey;
                var enabled = configuration.Enabled;
                if (ImGui.Checkbox("Enabled", ref enabled))
                {
                    configuration.Enabled = enabled;
                    configuration.Save();
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Changes may take a couple of seconds to apply.");
                }

                if (clientState.IsPvP)
                {
                    ImGui.SameLine();
                    ImGui.TextColored(ImGuiColors.DalamudOrange, "Plugin is disabled during PvP");
                }

                if (plugin.NotInFC)
                {
                    ImGui.SameLine();
                    ImGui.TextColored(ImGuiColors.DalamudRed, "Character not in FC");
                }
                else if (plugin.Loading && !plugin.Error)
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

                var onlyColorFCTag = configuration.OnlyColorFCTag;
                if (ImGui.Checkbox("Only color the FC tag", ref onlyColorFCTag))
                {
                    configuration.OnlyColorFCTag = onlyColorFCTag;
                    configuration.Save();
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("This will only colour the FC tag instead of the entire name.");
                }

                var includeSelf = configuration.IncludeSelf;
                if (ImGui.Checkbox("Include self", ref includeSelf))
                {
                    configuration.IncludeSelf = includeSelf;
                    configuration.Save();
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("This will colour your own FC tag.");
                }

                var ignoreFriends = configuration.IgnoreFriends;
                ImGui.SameLine();
                if (ImGui.Checkbox("Ignore friends", ref ignoreFriends))
                {
                    configuration.IgnoreFriends = ignoreFriends;
                    configuration.Save();
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Don't change the nameplates of friends.");
                }

                var includeDuties = configuration.IncludeDuties;
                if (ImGui.Checkbox("Include duties", ref includeDuties))
                {
                    if (!includeDuties)
                    {
                        configuration.OnlyDuties = false;
                    }

                    configuration.IncludeDuties = includeDuties;
                    configuration.Save();
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Will colour the entire names of FC members when inside a duty.");
                }

                ImGui.SameLine();
                var onlyDuties = configuration.OnlyDuties;
                if (ImGui.Checkbox("Only duties", ref onlyDuties))
                {
                    if (onlyDuties)
                    {
                        configuration.IncludeDuties = true;
                    }

                    configuration.OnlyDuties = onlyDuties;
                    configuration.Save();
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Disable the plugin outside of duties. This helps with conflicts with other plugins.");
                }

                var glow = configuration.Glow;
                if (ImGui.Checkbox("Enable glow", ref glow))
                {
                    configuration.Glow = glow;
                    configuration.Save();
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Makes outline of the nameplates thicker.");
                }

                ImGui.Separator();

                var groups = configuration.Groups.Keys.Where(a => a != "Other FC" && a != "Default").Prepend("Other FC").Prepend("Default").ToArray();

                ImGui.Text("Groups");
                ImGui.SameLine();

                if (ImGuiComponents.IconButton(FontAwesomeIcon.Plus))
                {
                    newGroup = "";
                    showAddNewGroup = true;
                }

                using (var groupsPanel = ImRaii.Child("###GroupsPanel", new Vector2(ImGui.GetContentRegionAvail().X, 300.0f * ImGuiHelpers.GlobalScale)))
                {
                    if (groupsPanel)
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

                            if (groupName != "Default" && groupName != "Other FC")
                            {
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
                }

                if (showAddNewGroup)
                {
                    ImGui.Begin("FC Name Color Config - Add new group", ref showAddNewGroup,
                        ImGuiWindowFlags.AlwaysAutoResize);
                    var exists = groups.Contains(newGroup);
                    var add = ImGui.InputTextWithHint("###NewGroup", "Your group name", ref newGroup, 50,
                        ImGuiInputTextFlags.EnterReturnsTrue) && newGroup.Length > 1;

                    ImGui.SameLine();

                    if (newGroup.Length == 0 || exists)
                    {
                        ImGuiComponents.DisabledButton("Add Group");
                    }
                    else
                    {
                        if (ImGui.Button("Add Group"))
                        {
                            add = true;
                        }
                    }

                    if (exists)
                    {
                        ImGui.TextColored(ImGuiColors.DalamudRed, "Group names must be unique.");
                    }

                    if (add)
                    {
                        configuration.Groups.Add(newGroup, new Group
                        {
                            UiColor = "52",
                            Color = new Vector4(0.07450981f, 0.8f, 0.6392157f, 1f)
                        });
                        configuration.Save();
                        showAddNewGroup = false;
                    }

                    ImGui.End();
                }
            }

            ImGui.Separator();
            ImGui.Spacing();
            if (ImGui.Button("Ignore List"))
            {
                showIgnoreList = !showIgnoreList;
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Select which players shouldn’t be affected.");
            }

            ImGui.SameLine();
            if (ImGui.Button("FC management"))
            {
                showAdditionalFCConfig = !showAdditionalFCConfig;
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
                    configuration.PlayerFCIDs = new();
                    configuration.PlayerIDs = new();
                    configuration.Save();
                    showAddAdditionalFC = false;
                    plugin.SearchingFC = false;
                    plugin.Reload();
                }
            }


            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(
                    $@"Pressing this will clear the list of FC members and attempt to fetch all the necessary data from Lodestone.
This can be especially useful if something went wrong when loading from Lodestone, or if you’ve joined a different FC.

If something goes wrong trying to fetch the data, you can try again after {(plugin.Cooldown > 0 ? plugin.Cooldown : Plugin.CooldownTime)} seconds.");
            }

            if (showIgnoreList)
            {
                ImGui.SetNextWindowSize(new Vector2(270, 200), ImGuiCond.FirstUseEver);
                ImGui.Begin("FC Name Color Config - Ignore List", ref showIgnoreList);
                ImGui.TextWrapped("Don’t update nameplates for these players.");
                ImGui.Spacing();
                ImGui.SetNextItemWidth(150f * ImGuiHelpers.GlobalScale);
                var fcMembers = GetFCMembers();
                var playerNames = fcMembers.Select(member => member.Name).ToArray();
                var playerIndex = Array.IndexOf(playerNames, currentIgnoredPlayer.Name);
                ImGui.SetNextItemWidth(170f * ImGuiHelpers.GlobalScale);

                if (ImGui.Combo(
                        "###AddPlayerToIgnoreList",
                        ref playerIndex,
                        playerNames,
                        playerNames.Length))
                {
                    currentIgnoredPlayer = fcMembers[playerIndex];
                }

                ImGui.SameLine();
                if (ImGui.SmallButton("Add Player"))
                {
                    if (configuration.IgnoredPlayers.ContainsKey(currentIgnoredPlayer.Name))
                    {
                        ImGui.OpenPopup("###AddPlayerToIgnoreListDupe");
                    }
                    else
                    {
                        configuration.IgnoredPlayers.Add(currentIgnoredPlayer.Name,
                            currentIgnoredPlayer.ID);
                        configuration.Save();
                    }
                }

                if (ImGui.BeginPopup("###AddPlayerToIgnoreListDupe"))
                {
                    ImGui.Text("You’ve already added this player!");
                    ImGui.EndPopup();
                }

                foreach (var (key, _) in configuration.IgnoredPlayers.ToList())
                {
                    ImGui.Spacing();
                    ImGui.Text(key);
                    ImGui.SameLine();
                    ImGui.BeginGroup();
                    ImGui.PushFont(UiBuilder.IconFont);
                    ImGui.Text(FontAwesomeIcon.Times.ToIconString());
                    ImGui.PopFont();
                    ImGui.EndGroup();
                    if (!ImGui.IsItemClicked(ImGuiMouseButton.Left)) continue;
                    configuration.IgnoredPlayers.Remove(key);
                    configuration.Save();
                }

                ImGui.End();
            }

            if (!configuration.FCGroups.ContainsKey(plugin.PlayerKey))
            {
                configuration.FCGroups.Add(plugin.PlayerKey, new());
            }

            if (showAdditionalFCConfig)
            {
                ImGui.SetNextWindowSize(new Vector2(320, 250), ImGuiCond.FirstUseEver);
                ImGui.SetNextWindowSizeConstraints(new Vector2(320, 250), new Vector2(320, 1000f));
                ImGui.Begin("FC Name Color Config - Additional FCs", ref showAdditionalFCConfig);
                ImGui.Spacing();
                ImGui.TextWrapped("Track FCs that aren’t your own.");

                if (ImGui.Button("Add FC"))
                {
                    fcUrl = "";
                    plugin.SearchingFCError = "";
                    showAddAdditionalFC = true;
                }

                ImGui.Separator();

                if (configuration.FCGroups[plugin.PlayerKey].Count == 0)
                {
                    if (plugin.FC.HasValue)
                    {
                        configuration.FCGroups[plugin.PlayerKey][plugin.FC.Value.ID] = "Default";
                    }

                    ImGui.Text("There are currently no additional FCs being tracked.");
                }

                foreach (var fcConfigEntry in configuration.FCGroups[plugin.PlayerKey])
                {
                    var id = fcConfigEntry.Key;
                    var groupName = fcConfigEntry.Value;

                    if (!configuration.FCs.ContainsKey(id))
                    {
                        ImGui.Text($"Fetching FC {id}...");
                        continue;
                    }

                    var fc = configuration.FCs[id];

                    ImGui.PushID(fcConfigEntry.Key);
                    ImGui.Text("Settings for");
                    ImGui.SameLine();
                    ImGui.TextColored(configuration.Groups[groupName].Color, fc.Name);
                    ImGui.ColorButton("", configuration.Groups[groupName].Color);
                    ImGui.SameLine();
                    var groups = configuration.Groups.Keys.ToArray();
                    var groupIndex = Array.IndexOf(groups, groupName);
                    if (ImGui.Combo("###AdditionalFCGroup", ref groupIndex, groups, groups.Length))
                    {
                        configuration.FCGroups[plugin.PlayerKey][fc.ID] = groups[groupIndex];
                        configuration.Save();
                    }

                    ImGui.SameLine();
                    if (ImGuiComponents.IconButton(FontAwesomeIcon.Trash, new Vector4(0.8f, 0, 0, 1f),
                            new Vector4(1f, 0, 0, 1f), new Vector4(0.9f, 0, 0, 1f)))
                    {
                        pluginLog.Debug("Deleting additional FC {fc}", fc.Name);
                        configuration.FCGroups[plugin.PlayerKey].Remove(id);
                        var shouldDeleteFC = !configuration.FCGroups.Any(character => character.Value.ContainsValue(groupName));
                        if (shouldDeleteFC)
                        {
                            configuration.FCs.Remove(fc.ID);
                            pluginLog.Debug("Removing FC {name} altogether, no settings found anymore.", fc.Name);
                        }
                        configuration.Save();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip($"Delete {fc.Name}.");
                    }

                    ImGui.PopID();
                }

                ImGui.End();

                if (showAddAdditionalFC || plugin.SearchingFC)
                {
                    ImGui.Begin("FC Name Color Config - Adding Additional FC", ref showAddAdditionalFC, ImGuiWindowFlags.AlwaysAutoResize);
                    ImGui.Spacing();

                    ImGui.Text("Please enter the lodestone URL of the FC.");
                    ImGui.SameLine();
                    if (ImGui.SmallButton("Open Lodestone"))
                    {
                        Process.Start("explorer", "https://eu.finalfantasyxiv.com/lodestone/community/search/");
                    }

                    ImGui.Text(
                        "It should look like this: https://eu.finalfantasyxiv.com/lodestone/freecompany/1234567890123456789");
                    ImGui.SetNextItemWidth(500f * ImGuiHelpers.GlobalScale);
                    ImGui.InputTextWithHint("###FCUrl",
                        "https://eu.finalfantasyxiv.com/lodestone/freecompany/1234567890123456789",
                        ref fcUrl, 100);

                    ImGui.SameLine();
                    if (plugin.SearchingFC)
                    {
                        ImGuiComponents.DisabledButton("Searching FC");
                    }
                    else
                    {
                        var isMatch = fcUrl.Length > 0 && fcUrlPattern.IsMatch(fcUrl);

                        if (!isMatch)
                        {
                            ImGuiComponents.DisabledButton("Search FC");
                        }
                        else if (isMatch && ImGui.Button("Search FC"))
                        {
                            var match = fcUrlPattern.Match(fcUrl);
                            var id = match.Groups[2].Value;
                            var shouldContinue = true;

                            if (configuration.PlayerIDs.TryGetValue(plugin.PlayerKey, out var currentPlayerID))
                            {
                                if (configuration.PlayerFCIDs.TryGetValue(currentPlayerID, out var playerFC))
                                {
                                    if (playerFC == id)
                                    {
                                        ImGui.OpenPopup("###SameFC");
                                        shouldContinue = false;
                                    }
                                }
                            }

                            if (shouldContinue)
                            {
                                if (configuration.FCGroups[plugin.PlayerKey].ContainsKey(id))
                                {
                                    ImGui.OpenPopup("###AddFCDupe");
                                }
                                else
                                {
                                    plugin.SearchFC(id, "Other FC").ContinueWith(async success =>
                                    {
                                        var result = await success;
                                        if (result)
                                        {
                                            showAddAdditionalFC = false;
                                        }
                                    });
                                }
                            }
                        }
                    }

                    if (fcUrl.Length > 0 && !fcUrlPattern.IsMatch(fcUrl))
                    {
                        ImGui.TextColored(ImGuiColors.DalamudRed, "Url doesn’t match the FC url format.");
                    }

                    if (plugin.SearchingFCError.Length > 0)
                    {
                        ImGui.TextColored(ImGuiColors.DalamudRed, plugin.SearchingFCError);
                    }

                    if (ImGui.BeginPopup("###SameFC"))
                    {
                        ImGui.Text("This is your own FC, it’s already being tracked.");
                        ImGui.EndPopup();
                    }

                    if (ImGui.BeginPopup("###AddFCDupe"))
                    {
                        ImGui.Text("You’ve already added this FC!");
                        ImGui.EndPopup();
                    }

                    ImGui.End();
                }

                ImGui.End();
            }
        }

        private static Vector4 ConvertUIColorToColor(UIColor uiColor)
        {
            var temp = BitConverter.GetBytes(uiColor.UIForeground);
            return new Vector4((float)temp[3] / 255,
                (float)temp[2] / 255,
                (float)temp[1] / 255,
                (float)temp[0] / 255);
        }

        private List<FCMember> GetFCMembers()
        {
            var fcMembers = new List<FCMember>();
            var playersFCs = configuration.PlayerFCIDs;
            // TODO: Should this fetch *every* tracked FC or just the player's FCs?
            foreach (var playerFCID in playersFCs)
            {
                var exists = configuration.FCs.TryGetValue(playerFCID.Value, out var fc);
                fcMembers.AddRange(fc.Members);
            }

            fcMembers = fcMembers.Distinct().OrderBy(member => member.Name).ToList();

            return fcMembers;
        }
    }
}