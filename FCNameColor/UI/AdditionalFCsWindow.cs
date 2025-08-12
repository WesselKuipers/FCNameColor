using Dalamud.Interface.Components;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Dalamud.Bindings.ImGui;
using FCNameColor.Config;
using System;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Utility.Raii;

namespace FCNameColor.UI
{
    internal class AdditionalFCsWindow : Window
    {
        private readonly ConfigurationV1 configuration;
        private readonly Plugin plugin;
        private readonly IPluginLog pluginLog;
        private readonly AddAdditionalFCWindow addAdditionalFCWindow;

        public AdditionalFCsWindow(ConfigurationV1 configuration, Plugin plugin, IPluginLog pluginLog, AddAdditionalFCWindow addAdditionalFCWindow) : base("FC Name Color Config - Additional FCs")
        {
            Size = new Vector2(320, 250);
            SizeCondition = ImGuiCond.FirstUseEver;
            SizeConstraints = new WindowSizeConstraints() { MinimumSize = new Vector2(320, 250), MaximumSize = new Vector2(320, 1000f) };

            this.configuration = configuration;
            this.plugin = plugin;
            this.pluginLog = pluginLog;
            this.addAdditionalFCWindow = addAdditionalFCWindow;
        }

        public override bool DrawConditions() => plugin.ConfigOpen;

        public override void OnClose()
        {
            addAdditionalFCWindow.IsOpen = false;
            base.OnClose();
        }

        public override void Draw()
        {
            ImGui.Spacing();
            ImGui.TextWrapped("Track FCs that aren’t your own.");

            if (ImGui.Button("Add FC"))
            {
                addAdditionalFCWindow.IsOpen = true;
                plugin.SearchingFCError = "";
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

                using var imguiId = ImRaii.PushId(id);
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
            }
        }
    }
}
