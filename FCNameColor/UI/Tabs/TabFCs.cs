using System;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;

namespace FCNameColor.UI.Tabs;

public class TabFCs(Plugin plugin) : ConfigTab(plugin)
{
    private readonly Regex fcUrlPattern =
        new Regex(@"https:\/\/(eu|na|jp).finalfantasyxiv.com\/lodestone\/freecompany\/(\d{19})\/*");

    private string fcUrl = "";
    
    public override void Draw(Func<bool> markDirty)
    {
        using var tab = ImRaii.TabItem("Additional FCs###TabFCs");
        if (!tab) return;

        using var child = ImRaii.Child("TabChild", ImGui.GetContentRegionAvail());
        
        ImGui.TextWrapped("Track FCs that aren't your own.");
        ImGui.Spacing();

        ImGui.Text("Please enter the lodestone URL of the FC.");
        ImGui.SameLine();
        if (ImGui.SmallButton("Open Lodestone"))
        {
            Process.Start("explorer", "https://eu.finalfantasyxiv.com/lodestone/community/search/");
        }

        ImGui.Spacing();

        ImGui.TextWrapped(
            "It should look like this:\nhttps://eu.finalfantasyxiv.com/lodestone/freecompany/1234567890123456789");
        ImGui.InputTextWithHint("###FCUrl",
            "https://eu.finalfantasyxiv.com/lodestone/freecompany/1234567890123456789",
            ref fcUrl, 120);
        ImGui.SameLine();
        if (Plugin.SearchingFC)
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

                if (Plugin.PlayerKey != null && Config.PlayerIDs.TryGetValue(Plugin.PlayerKey, out var currentPlayerID))
                {
                    if (Config.PlayerFCIDs.TryGetValue(currentPlayerID, out var playerFC))
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
                    if (Plugin.PlayerKey != null && Config.FCGroups[Plugin.PlayerKey].ContainsKey(id))
                    {
                        ImGui.OpenPopup("###AddFCDupe");
                    }
                    else
                    {
                        Plugin.SearchFC(id, "Other FC").ContinueWith(async success =>
                        {
                            await success;
                            fcUrl = "";
                        });
                    }
                }
            }
        }

        if (fcUrl.Length > 0 && !fcUrlPattern.IsMatch(fcUrl))
        {
            ImGui.TextColored(ImGuiColors.DalamudRed, "Url doesn't match the FC url format.");
        }

        if (Plugin.SearchingFCError is { Length: > 0 })
        {
            ImGui.TextColored(ImGuiColors.DalamudRed, Plugin.SearchingFCError);
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

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (Plugin.PlayerKey != null && Config.FCGroups[Plugin.PlayerKey].Count == 0)
        {
            if (Plugin.FC?.ID != null)
                Config.FCGroups[Plugin.PlayerKey][Plugin.FC.Value.ID] = "Default";

            ImGui.Text("There are currently no additional FCs being tracked.");
        }

        if (Plugin.PlayerKey == null) return;
        foreach (var fcConfigEntry in Config.FCGroups[Plugin.PlayerKey])
        {
            var id = fcConfigEntry.Key;
            var groupName = fcConfigEntry.Value;

            if (!Config.FCs.TryGetValue(id, out var fc))
            {
                ImGui.Text($"Fetching FC {id}...");
                continue;
            }

            using var imguiId = ImRaii.PushId(id);
            ImGui.Text("Settings for");
            ImGui.SameLine();
            ImGui.TextColored(Config.Groups[groupName].Color, fc.Name);
            ImGui.ColorButton("", Config.Groups[groupName].Color);
            ImGui.SameLine();
            var groups = Config.Groups.Keys.ToArray();
            var groupIndex = Array.IndexOf(groups, groupName);
            if (ImGui.Combo("###AdditionalFCGroup", ref groupIndex, groups, groups.Length))
            {
                if (fc.ID != null) Config.FCGroups[Plugin.PlayerKey][fc.ID] = groups[groupIndex];
                markDirty();
            }

            ImGui.SameLine();
            if (ImGuiComponents.IconButton(FontAwesomeIcon.Trash, new Vector4(0.8f, 0, 0, 1f),
                    new Vector4(1f, 0, 0, 1f), new Vector4(0.9f, 0, 0, 1f)))
            {
                if (fc.Name != null)
                {
                    Plugin.PluginLog.Debug("Deleting additional FC {fc}", fc.Name);
                    Config.FCGroups[Plugin.PlayerKey].Remove(id);
                    var shouldDeleteFC =
                        !Config.FCGroups.Any(character => character.Value.ContainsValue(groupName));
                    if (shouldDeleteFC)
                    {
                        if (fc.ID != null) Config.FCs.Remove(fc.ID);
                        Plugin.PluginLog.Debug("Removing FC {name} altogether, no settings found anymore.", fc.Name);
                    }
                }

                markDirty();
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip($"Delete {fc.Name}.");
            }

            ImGui.Spacing();
        }
    }
}