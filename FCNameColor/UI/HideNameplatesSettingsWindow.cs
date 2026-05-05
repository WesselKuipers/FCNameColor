using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using FCNameColor.Config;
using FCNameColor.Utils;

namespace FCNameColor.UI
{
    internal class HideNameplatesSettingsWindow(ConfigurationV1 configuration, Plugin plugin)
        : Window("Hide nameplate settings", ImGuiWindowFlags.AlwaysAutoResize)
    {
        public override bool DrawConditions() => plugin.ConfigOpen;

        public override void Draw()
        {
            var isDirty = false;
            
            ImGuiUtils.AddCheckbox("Hide inside duties", "Whether nameplates are hidden during duties", configuration.HideInDuties, value => configuration.HideInDuties = value, ref isDirty);
            ImGuiUtils.AddCheckbox("Hide friends", "Whether friends are hidden", configuration.HideFriends, value => configuration.HideFriends = value, ref isDirty);
            ImGuiUtils.AddCheckbox("Hide party members", "Whether party members are hidden", configuration.HidePartyMembers, value => configuration.HidePartyMembers = value, ref isDirty);
            ImGuiUtils.AddCheckbox("Hide alliance raid members", "Whether alliance members are hidden", configuration.HideAllianceMembers, value => configuration.HideAllianceMembers = value, ref isDirty);
            ImGuiUtils.AddCheckbox("Hide hovered players", "Whether nameplates should be hidden when hovering over their character", configuration.HideOnHover, value => configuration.HideOnHover = value, ref isDirty);
            ImGuiUtils.AddCheckbox("Hide soft targeted players", "Whether nameplates should hidden when soft targeting their character", configuration.HideOnSoftTarget, value => configuration.HideOnSoftTarget = value, ref isDirty);
            ImGuiUtils.AddCheckbox("Hide targeted player", "Whether nameplates should hidden when targeting their character", configuration.HideOnTarget, value => configuration.HideOnTarget = value, ref isDirty);

            if (isDirty)
            {
                configuration.Save();
            }
        }
    }
}