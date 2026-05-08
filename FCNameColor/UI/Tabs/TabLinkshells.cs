using System;
using System.Diagnostics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;

namespace FCNameColor.UI.Tabs;

public class TabLinkshells(Plugin plugin) : ConfigTab(plugin)
{
    private string linkshellUrl = "";
    
    public override void Draw(Func<bool> markDirty)
    {
        using var tab = ImRaii.TabItem("Linkshells###TabLinkshells");
        if (!tab) return;

        ImGui.TextWrapped("Track players in any linkshell.");
        ImGui.Spacing();

        ImGui.Text("Please enter the lodestone URL of the linkshell.");
        ImGui.SameLine();
        if (ImGui.SmallButton("Open Lodestone"))
        {
            Process.Start("explorer", "https://eu.finalfantasyxiv.com/lodestone/community/search/");
        }
        ImGui.Spacing();
        
        ImGui.TextWrapped(
            "It should look like this:\nhttps://eu.finalfantasyxiv.com/lodestone/linkshell/12345678901234567\nor\nhttps://https://eu.finalfantasyxiv.com/lodestone/crossworld_linkshell/abcdefghijklmnopqrstuvwxyz01234567890123\n");
        ImGui.InputTextWithHint("###LSUrl",
            "https://eu.finalfantasyxiv.com/lodestone/linkshell/12345678901234567",
            ref linkshellUrl, 120);
        ImGui.SameLine();
        if (Plugin.SearchingFC)
        {
            ImGuiComponents.DisabledButton("Searching Linkshell");
        }
    }
}