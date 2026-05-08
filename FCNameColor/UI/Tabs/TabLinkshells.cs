using System;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace FCNameColor.UI.Tabs;

public class TabLinkshells(Plugin plugin) : ConfigTab(plugin)
{
    public override void Draw(Func<bool> markDirty)
    {
        using var tab = ImRaii.TabItem("Linkshells###TabLinkshells");
        if (!tab) return;

        ImGui.Text("In progress umu");
    }
}