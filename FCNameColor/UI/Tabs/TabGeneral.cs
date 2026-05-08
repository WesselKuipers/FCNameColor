using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using FCNameColor.Utils;

namespace FCNameColor.UI.Tabs;

public class TabGeneral(Plugin plugin) : ConfigTab(plugin)
{
    public override void Draw(Func<bool> markDirty)
    {
        using var tab = ImRaii.TabItem("General###TabGeneral");
        if (!tab) return;

        using (ImRaii.Child("TabChild",
                   new Vector2(ImGui.GetContentRegionAvail().X,
                       ImGui.GetContentRegionAvail().Y - 26f * ImGuiHelpers.GlobalScale)))
        {
            if (Plugin.FirstTime)
            {
                ImGui.TextColored(ImGuiColors.DalamudYellow,
                    "Plugin is setting up for the first time, please wait a moment.");
                return;
            }

            ImGuiUtils.AddCheckbox("Enabled", "Changes may take a couple of seconds to apply.", Config.Enabled,
                value => Config.Enabled = value, markDirty);

            if (Plugin.ClientState.IsPvP)
            {
                ImGui.SameLine();
                ImGui.TextColored(ImGuiColors.DalamudOrange, "Plugin is disabled during PvP");
            }

            if (Plugin.NotFound)
            {
                ImGui.SameLine();
                using (ImRaii.PushColor(ImGuiCol.TextDisabled, ImGuiColors.DalamudOrange))
                    ImGuiComponents.HelpMarker(
                        "Could not find player character on Lodestone.\nIf your character is new, please wait a couple of hours for it to show up on Lodestone.\nIf your character is set to private, then the automatic FC fetching won’t work.",
                        FontAwesomeIcon.ExclamationTriangle);
            }
            else if (Plugin.NotInFC)
            {
                ImGui.SameLine();
                ImGui.TextColored(ImGuiColors.DalamudRed, "Character not in FC");
            }
            else if (Plugin is { Loading: true, Error: false })
            {
                ImGui.SameLine();
                ImGui.Text(" Fetching FC members from Lodestone...");
            }
            else if (Plugin.Error)
            {
                ImGui.SameLine();
                ImGui.TextColored(ImGuiColors.DalamudRed,
                    $"Error when fetching. Retrying in {Plugin.Cooldown} seconds.");
            }
            
            ImGui.SameLine();
            
            var labelText = plugin.Cooldown > 0 ? $"Clear & Retry ({plugin.Cooldown})" : "Clear & Retry";
            var width = (ImGui.CalcTextSize(labelText).X + ImGui.ImGuiStyle().FramePadding.X + 4f) * ImGuiHelpers.GlobalScale;
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - width);
            if (plugin.Cooldown > 0)
            {
                ImGuiComponents.DisabledButton(labelText);
            }
            else
            {
                if (ImGui.Button("Clear & Retry"))
                {
                    Config.PlayerFCIDs = new Dictionary<string, string?>();
                    Config.PlayerIDs = new Dictionary<string, string>();
                    markDirty();
                
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

            ImGuiUtils.AddCheckbox("Only color the FC tag",
                "This will only colour the FC tag instead of the entire name.",
                Config.OnlyColorFCTag, value => Config.OnlyColorFCTag = value, markDirty);

            using (ImRaii.Table("###OptionsTable", 2, ImGuiTableFlags.SizingFixedFit))
            {
                ImGui.TableNextColumn();
                ImGuiUtils.AddCheckbox("Include self", "This will colour your own FC tag.", Config.IncludeSelf,
                    value => Config.IncludeSelf = value, markDirty);
                ImGui.TableNextColumn();
                ImGuiUtils.AddCheckbox("Ignore friends", "Don't change the nameplates of friends.",
                    Config.IgnoreFriends,
                    value => Config.IgnoreFriends = value, markDirty);

                ImGui.TableNextColumn();
                ImGuiUtils.AddCheckbox("Include duties",
                    "Will colour the entire names of FC members when inside a duty",
                    Config.IncludeDuties, value => Config.IncludeDuties = value, markDirty);
                ImGui.TableNextColumn();
                ImGuiUtils.AddCheckbox("Only duties",
                    "Disable the plugin outside of duties. This helps with conflicts with other plugins.",
                    Config.OnlyDuties,
                    value => Config.OnlyDuties = value, markDirty);
            }

            ImGuiUtils.AddCheckbox("Enable glow", "Makes outline of the nameplates thicker.", Config.Glow,
                value => Config.Glow = value, markDirty);

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGuiUtils.AddCheckbox("Hide other nameplates",
                $"""
                 Hide other player nameplates

                 If checked, this will automatically hide any nameplates that aren't tracked.
                 Note that this requires you to change the settings in Character Configuration -> Display Name Settings.

                 Any nameplates that are already hidden by these settings will remain hidden.
                 """,
                Config.HideOtherNameplates,
                value => Config.HideOtherNameplates = value,
                markDirty);
            ImGui.Spacing();
            using (ImRaii.PushIndent())
            {
                ImGuiUtils.AddCheckbox("Hide inside duties", "Whether nameplates are hidden during duties",
                    Config.HideInDuties,
                    value => Config.HideInDuties = value, markDirty);
                ImGuiUtils.AddCheckbox("Hide friends", "Whether friends are hidden", Config.HideFriends,
                    value => Config.HideFriends = value, markDirty);
                ImGuiUtils.AddCheckbox("Hide party members", "Whether party members are hidden",
                    Config.HidePartyMembers,
                    value => Config.HidePartyMembers = value, markDirty);
                ImGuiUtils.AddCheckbox("Hide alliance raid members", "Whether alliance members are hidden",
                    Config.HideAllianceMembers, value => Config.HideAllianceMembers = value, markDirty);
                ImGuiUtils.AddCheckbox("Hide hovered players",
                    "Whether nameplates should be hidden when hovering over their character", Config.HideOnHover,
                    value => Config.HideOnHover = value, markDirty);
                ImGuiUtils.AddCheckbox("Hide soft targeted players",
                    "Whether nameplates should hidden when soft targeting their character", Config.HideOnSoftTarget,
                    value => Config.HideOnSoftTarget = value, markDirty);
                ImGuiUtils.AddCheckbox("Hide targeted player",
                    "Whether nameplates should hidden when targeting their character", Config.HideOnTarget,
                    value => Config.HideOnTarget = value, markDirty);
            }
        }
    }
}