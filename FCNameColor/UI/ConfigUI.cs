using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using FCNameColor.Config;
using FCNameColor.UI.Tabs;
using FCNameColor.Utils;

namespace FCNameColor.UI;

public class ConfigUI : Window
{
    private readonly ConfigurationV1 config;

    private bool dirty;

    private readonly List<ConfigTab> tabs = [];

    public ConfigUI(Plugin plugin) : base("FC Name Color Config###TabsConfig")
    {
        config = plugin.Config;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = ImGuiHelpers.ScaledVector2(520, 455),
        };
        
        tabs.Add(new TabGeneral(plugin));
        tabs.Add(new TabGroups(plugin));
        tabs.Add(new TabFCs(plugin));
        // tabs.Add(new TabLinkshells(plugin));
        tabs.Add(new TabIgnored(plugin));
    }

    public override void Draw()
    {
        dirty = false;

        using var tabBar = ImRaii.TabBar("Tab Bar Label", ImGuiTabBarFlags.Reorderable);
        if (!tabBar) return;

        var markDirty = () => dirty = true;

        foreach (var tab in tabs)
        {
            tab.Draw(markDirty);
        }

        if (dirty)
        {
            config.Save();
        }
    }
}