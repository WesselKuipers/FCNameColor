using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;

namespace FCNameColor.UI.Tabs;

public class TabGroups(Plugin plugin) : ConfigTab(plugin)
{
    private string newGroup = "";

    public override void Draw(Func<bool> markDirty)
    {
        using var tab = ImRaii.TabItem("Groups###TabGroups");
        if (!tab) return;

        using var child = ImRaii.Child("TabChild", ImGui.GetContentRegionAvail());
        if (!child) return;
        
        ImGui.Text(
            "Groups determine which colour is used for the nameplate.\nMultiple FCs or Linkshells can be assigned to the same group.");
        ImGui.Spacing();

        var groups = Config.Groups.Keys.Where(group => group != "Other FC" && group != "Default").Prepend("Other FC")
            .Prepend("Default").ToArray();
        var exists = groups.Contains(newGroup);
        ImGui.InputTextWithHint("###NewGroup", "Your group name", ref newGroup, 50,
            ImGuiInputTextFlags.EnterReturnsTrue);

        ImGui.SameLine();

        if (newGroup.Length == 0 || exists)
        {
            ImGuiComponents.DisabledButton("Add Group");
        }
        else
        {
            if (ImGui.Button("Add Group"))
            { 
                Config.Groups.Add(newGroup, new Group
                {
                    UiColor = "52",
                    Color = new Vector4(0.07450981f, 0.8f, 0.6392157f, 1f)
                });

                newGroup = "";
                markDirty();
            }
        }

        if (newGroup.Length > 0 && exists)
        {
            ImGui.TextColored(ImGuiColors.DalamudRed, "Group names must be unique.");
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.Text("Click on the colours of the groups below to change them");
        foreach (var (groupName, group) in Config.Groups)
        {
            var groupColor = group.Color;
            if (ImGui.ColorEdit4(groupName, ref groupColor, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoAlpha))
            {
                var updatedGroup = Config.Groups[groupName];
                updatedGroup.Color = groupColor;
                Config.Groups[groupName] = updatedGroup;
                markDirty();
            }

            if (groupName is "Default" or "Other FC") continue;

            ImGui.SameLine();
            using var id = ImRaii.PushId(groupName);
            if (ImGuiComponents.IconButton(FontAwesomeIcon.Trash, new Vector4(0.8f, 0, 0, 1f),
                    new Vector4(1f, 0, 0, 1f), new Vector4(0.9f, 0, 0, 1f)))
            {
                Plugin.PluginLog.Debug($"Deleting group {groupName}");
                Config.Groups.Remove(groupName);

                foreach (var playerConfigs in Config.FCGroups)
                {
                    foreach (var fcGroup in playerConfigs.Value)
                    {
                        if (fcGroup.Value == groupName)
                        {
                            Config.FCGroups[playerConfigs.Key][fcGroup.Key] = "Other FC";
                        }
                    }
                }

                markDirty();
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip($"Delete group {groupName}.\nThe groups Default and Other FC cannot be removed.");
            }

            ImGui.Spacing();
        }
    }
}