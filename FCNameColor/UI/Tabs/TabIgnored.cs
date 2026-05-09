using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace FCNameColor.UI.Tabs;

public class TabIgnored(Plugin plugin) : ConfigTab(plugin)
{
    private string ignoredPlayerFilter = "";
    private readonly List<FCMember> selectedPlayers = [];

    public override void Draw(Func<bool> markDirty)
    {
        using var tab = ImRaii.TabItem("Ignored###TabIgnored");
        if (!tab) return;

        ImGui.TextWrapped("Don't update nameplates for these players.");
        ImGui.Spacing();

        var ignoredPlayers = Config.IgnoredPlayers.ToList();
        var fcMembers = Config.FCs.SelectMany(fc => fc.Value.Members).OrderBy(member => member.Name).ToList();

        using (var tabChild = ImRaii.Child("Tab Child",
                   new Vector2(ImGui.GetContentRegionAvail().X,
                       ImGui.GetContentRegionAvail().Y - 26f * ImGuiHelpers.GlobalScale)))
        {
            if (!tabChild) return;
            using (var child = ImRaii.Child("###IgnoredPlayers",
                       new Vector2(ImGui.GetContentRegionAvail().X,
                           ImGui.GetContentRegionAvail().Y / 2 - ImGui.GetStyle().ItemSpacing.Y / 2)))
            {
                if (child)
                {
                    if (ignoredPlayers.Count == 0)
                    {
                        ImGui.Text("You're a friendly person, you don't have anyone ignored.");
                    }

                    foreach (var (key, _) in Config.IgnoredPlayers.ToList())
                    {
                        ImGui.Spacing();
                        using (var group = ImRaii.Group())
                        {
                            if (group.Alive)
                            {
                                ImGui.PushFont(UiBuilder.IconFont);
                                ImGui.Text(FontAwesomeIcon.Times.ToIconString());
                                ImGui.PopFont();
                            }
                        }

                        if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
                        {
                            Config.IgnoredPlayers.Remove(key);
                            markDirty();
                        }

                        ImGui.SameLine();
                        ImGui.Text(key);
                    }
                }
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.Text("Search by player name");
            ImGui.InputTextWithHint("###NewIgnoredPlayer", "Player name", ref ignoredPlayerFilter, 50);
            ImGui.Spacing();
            using (var child = ImRaii.Child("###FilteredPlayers",
                       new Vector2(ImGui.GetContentRegionAvail().X,
                           ImGui.GetContentRegionAvail().Y - ImGui.GetStyle().ItemSpacing.Y / 2)))
            {
                if (child)
                {
                    foreach (var member in fcMembers.Where(m =>
                                 m.Name.Contains(ignoredPlayerFilter, StringComparison.OrdinalIgnoreCase)))
                    {
                        var selected = selectedPlayers.Contains(member);
                        if (ImGui.Selectable(member.Name, selected))
                        {
                            if (selected)
                            {
                                selectedPlayers.Remove(member);
                            }
                            else
                            {
                                selectedPlayers.Add(member);
                            }
                        }
                    }
                }
            }
        }


        if (selectedPlayers.Count == 0)
        {
            ImGuiComponents.DisabledButton("Ignore Players");
        }
        else if (ImGui.Button("Ignore Players"))
        {
            foreach (var player in selectedPlayers)
            {
                Config.IgnoredPlayers.TryAdd(player.Name, player.ID);
            }

            selectedPlayers.Clear();
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(
                "Add selected players to the ignore list.\nDoes nothing if the player is already ignored.");
        }

        ImGui.SameLine();
        if (selectedPlayers.Count == 0)
        {
            ImGuiComponents.DisabledButton("Unignore Players");
        }
        else if (ImGui.Button("Unignore Players"))
        {
            Config.IgnoredPlayers =
                Config.IgnoredPlayers.Where(p => !selectedPlayers.Select(s => s.Name).Contains(p.Key)).ToDictionary();
            selectedPlayers.Clear();
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Remove selected players from the ignore list.");
        }

        ImGui.SameLine();
        if (Plugin.TargetManager.Target != null && Plugin.TargetManager.Target.ObjectKind == ObjectKind.Pc)
        {
            if (ImGui.Button("Ignore targeted player"))
            {
                var player = fcMembers.FindAll(player => player.Name == Plugin.TargetManager.Target.Name.TextValue);
                if (player.Count == 1)
                {
                    Config.IgnoredPlayers.TryAdd(player[0].Name, player[0].ID);
                }
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(
                    $"Add {Plugin.TargetManager.Target.Name.TextValue} to the ignore list.\nThis will do nothing if the player isn't included in any tracked FCs or Linkshells.");
            }
        }
        else
        {
            ImGuiComponents.DisabledButton("Ignore targeted player");
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Add targeted player to the ignore list.\nYou currently have no one targeted.");
            }
        }
    }
}