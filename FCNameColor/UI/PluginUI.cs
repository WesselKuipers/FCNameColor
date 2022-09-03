using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using Dalamud.Data;
using Dalamud.Game.ClientState;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Logging;
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
        private readonly Configuration configuration;
        private readonly List<UIColor> uiColors;
        private bool showIgnoreList;
        private bool showAdditionalFCConfig;
        private bool showAddAdditionalFC;
        private string fcUrl = "";
        private FCMember currentIgnoredPlayer;
        private readonly ClientState clientState;
        private bool editingFC = true;
        private string currentGroup;

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

        public PluginUI(Configuration config, DataManager data, Plugin plugin, ClientState clientState)
        {
            configuration = config;
            this.clientState = clientState;
            this.plugin = plugin;
            currentGroup = config.Groups.First().Key;

            var list = new List<UIColor>(data.GetExcelSheet<UIColor>()!.Distinct(new UIColorComparer()));
            list.Sort((a, b) =>
            {
                var colorA = ConvertUIColorToColor(a);
                var colorB = ConvertUIColorToColor(b);
                ImGui.ColorConvertRGBtoHSV(colorA.X, colorA.Y, colorA.Z, out var aH, out var aS, out var aV);
                ImGui.ColorConvertRGBtoHSV(colorB.X, colorB.Y, colorB.Z, out var bH, out var bS, out var bV);

                var hue = aH.CompareTo(bH);
                if (hue != 0)
                {
                    return hue;
                }

                var saturation = aS.CompareTo(bS);
                if (saturation != 0)
                {
                    return saturation;
                }

                var value = aV.CompareTo(bV);
                return value != 0 ? value : 0;
            });
            uiColors = list;
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

            ImGui.SetNextWindowSize(new Vector2(380, 550), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSizeConstraints(new Vector2(380, 550), new Vector2(float.MaxValue, float.MaxValue));
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

                var includeDuties = configuration.IncludeDuties;
                if (ImGui.Checkbox("Include duties", ref includeDuties))
                {
                    configuration.IncludeDuties = includeDuties;
                    configuration.Save();
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Will colour the entire names of FC members when inside a duty.");
                }

                var glow = configuration.Glow;
                if (ImGui.Checkbox("Enable glow", ref glow))
                {
                    configuration.Glow = glow;
                    configuration.Save();
                    plugin.XivCommonBase.Functions.NamePlates.ForceRedraw = true;
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Makes outline of the nameplates thicker.");
                }

                ImGui.Separator();

                var groups = configuration.Groups.Keys.ToArray();
                var groupIndex = Array.IndexOf(groups, currentGroup);

                if (ImGui.RadioButton("Own FC", editingFC))
                {
                    editingFC = true;
                }

                ImGui.SameLine();
                if (ImGui.RadioButton("Group: ", !editingFC))
                {
                    editingFC = false;
                }

                ImGui.SameLine();
                ImGui.SetNextItemWidth(150f * ImGuiHelpers.GlobalScale);
                if (ImGui.Combo("###AdditionalFCGroup", ref groupIndex, groups, groups.Length))
                {
                    currentGroup = groups[groupIndex];
                }

                ImGui.SameLine();
                if (ImGuiComponents.IconButton(FontAwesomeIcon.Plus))
                {
                    newGroup = "";
                    showAddNewGroup = true;
                }

                ImGui.SameLine();
                if (ImGuiComponents.IconButton(FontAwesomeIcon.Trash, new Vector4(0.8f, 0, 0, 1f),
                        new Vector4(1f, 0, 0, 1f), new Vector4(0.9f, 0, 0, 1f)) && currentGroup != "Other FC")
                {
                    PluginLog.Debug($"Deleting group {currentGroup}");
                    configuration.Groups.Remove(currentGroup);
                    if (configuration.AdditionalFCs.ContainsKey(plugin.PlayerKey))
                    {
                        for (var i = 0; i < configuration.AdditionalFCs[plugin.PlayerKey].Count; i++)
                        {
                            if (configuration.AdditionalFCs[plugin.PlayerKey][i].Group == currentGroup)
                            {
                                configuration.AdditionalFCs[plugin.PlayerKey][i].Group = "Other FC";
                            }
                        }
                    }

                    currentGroup = groups[0];
                    configuration.Save();
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip($"Delete group {currentGroup}. The group Other FC cannot be removed.");
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
                        currentGroup = newGroup;
                        showAddNewGroup = false;
                        editingFC = false;
                    }

                    ImGui.End();
                }

                ImGui.Text("Settings for");
                ImGui.SameLine();
                ImGui.TextColored(editingFC ? configuration.Color : configuration.Groups[currentGroup].Color,
                    editingFC
                        ? plugin?.FC.Name ?? "your FC"
                        : currentGroup);
                ImGui.ColorButton("Nameplate color. Click on a color below to select a new one.",
                    editingFC ? configuration.Color : configuration.Groups[currentGroup].Color);
                ImGui.SameLine();
                ImGui.Text("Nameplate color. Click on a color below to set a new one.");

                ImGui.Columns(12, "columns", false);
                foreach (var z in uiColors)
                {
                    if (z.UIForeground is 0 or 255)
                    {
                        continue;
                    }

                    var color = ConvertUIColorToColor(z);
                    var id = z.RowId.ToString();
                    var oldCursor = ImGui.GetCursorPos();

                    if (ImGui.ColorButton(id, color))
                    {
                        if (editingFC)
                        {
                            configuration.UiColor = id;
                            configuration.Color = color;
                        }
                        else
                        {
                            var group = configuration.Groups[currentGroup];
                            group.Color = color;
                            group.UiColor = id;
                            configuration.Groups[currentGroup] = group;
                        }

                        configuration.Save();
                    }

                    if (id == (editingFC ? configuration.UiColor : configuration.Groups[currentGroup].UiColor))
                    {
                        // For the selected colour, render a transparent checkmark on top
                        var newCursor = ImGui.GetCursorPos();
                        ImGui.SetCursorPos(oldCursor);
                        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, Vector4.Zero);
                        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, Vector4.Zero);
                        ImGui.PushStyleColor(ImGuiCol.FrameBg, Vector4.Zero);
                        var selected = true;
                        ImGui.Checkbox("", ref selected);
                        ImGui.PopStyleColor(3);
                        ImGui.SetCursorPos(newCursor);
                    }

                    ImGui.NextColumn();
                }

                ImGui.Columns(1);
            }

            ImGui.Separator();
            ImGui.Spacing();
            if (ImGui.Button("Ignore List"))
            {
                showIgnoreList = !showIgnoreList;
            }

            ImGui.SameLine();
            if (ImGui.Button("Additional FCs"))
            {
                showAdditionalFCConfig = !showAdditionalFCConfig;
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Track additional FCs that aren’t your own");
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
                    configuration.PlayerFCs = new Dictionary<string, FC>();
                    configuration.PlayerIDs = new Dictionary<string, string>();
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

            if (!configuration.AdditionalFCs.TryGetValue(plugin.PlayerKey, out var additionalFCs))
            {
                additionalFCs = new List<FCConfig>();
                configuration.AdditionalFCs.TryAdd(plugin.PlayerKey, additionalFCs);
            }

            if (showAdditionalFCConfig)
            {
                ImGui.SetNextWindowSize(new Vector2(325, 250), ImGuiCond.FirstUseEver);
                ImGui.SetNextWindowSizeConstraints(new Vector2(325, 250), new Vector2(float.MaxValue, float.MaxValue));
                ImGui.Begin("FC Name Color Config - Additional FCs", ref showAdditionalFCConfig,
                    ImGuiWindowFlags.AlwaysAutoResize);
                ImGui.Spacing();
                ImGui.TextWrapped("Track FCs that aren’t your own.");

                if (ImGui.Button("Add FC"))
                {
                    fcUrl = "";
                    plugin.SearchingFCError = "";
                    showAddAdditionalFC = true;
                }

                ImGui.Separator();

                foreach (var fc in additionalFCs.ToList())
                {
                    ImGui.PushID(fc.FC.ID);
                    ImGui.Text("Settings for");
                    ImGui.SameLine();
                    ImGui.TextColored(configuration.Groups[fc.Group].Color, fc.FC.Name);
                    ImGui.ColorButton("", configuration.Groups[fc.Group].Color);
                    ImGui.SameLine();
                    var groups = configuration.Groups.Keys.ToArray();
                    var groupIndex = Array.IndexOf(groups, fc.Group);
                    if (ImGui.Combo("###AdditionalFCGroup", ref groupIndex, groups, groups.Length))
                    {
                        configuration.AdditionalFCs[plugin.PlayerKey][additionalFCs.IndexOf(fc)]
                            .Group = groups[groupIndex];
                        configuration.Save();
                    }

                    ImGui.SameLine();
                    if (ImGuiComponents.IconButton(FontAwesomeIcon.Trash, new Vector4(0.8f, 0, 0, 1f),
                            new Vector4(1f, 0, 0, 1f), new Vector4(0.9f, 0, 0, 1f)))
                    {
                        PluginLog.Debug($"Deleting additional FC {fc.FC.Name}");
                        configuration.AdditionalFCs[plugin.PlayerKey].Remove(fc);
                        configuration.Save();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip($"Delete {fc.FC.Name}.");
                    }

                    ImGui.PopID();
                }

                if (additionalFCs.Count == 0)
                {
                    ImGui.Text("There are currently no additional FCs.");
                }


                ImGui.End();

                if (showAddAdditionalFC || plugin.SearchingFC)
                {
                    ImGui.Begin("FC Name Color Config - Adding Additional FC", ref showAddAdditionalFC,
                        ImGuiWindowFlags.AlwaysAutoResize);
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
                                if (configuration.PlayerFCs.TryGetValue(currentPlayerID, out var playerFC))
                                {
                                    if (playerFC.ID == id)
                                    {
                                        ImGui.OpenPopup("###SameFC");
                                        shouldContinue = false;
                                    }
                                }
                            }

                            if (shouldContinue)
                            {
                                if (additionalFCs.Exists(fc => fc.FC.ID == id))
                                {
                                    ImGui.OpenPopup("###AddFCDupe");
                                }
                                else
                                {
                                    plugin.SearchFC(id, "Other FC").ContinueWith(async fc =>
                                    {
                                        var result = await fc;
                                        if (result != null)
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
            return new Vector4((float) temp[3] / 255,
                (float) temp[2] / 255,
                (float) temp[1] / 255,
                (float) temp[0] / 255);
        }

        private List<FCMember> GetFCMembers()
        {
            var fcMembers = new List<FCMember>();
            var playersFCs = configuration.PlayerFCs;
            foreach (var playerFC in playersFCs)
            {
                fcMembers.AddRange(playerFC.Value.Members);
            }

            fcMembers = fcMembers.Distinct().OrderBy(member => member.Name).ToList();

            return fcMembers;
        }
    }
}