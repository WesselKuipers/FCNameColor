using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using FCNameColor.Config;
using System;
using System.Linq;
using System.Numerics;

namespace FCNameColor.UI
{
    internal class AddNewGroupWindow : Window
    {
        private readonly ConfigurationV1 configuration;
        private readonly Plugin plugin;
        private string? newGroup;

        public AddNewGroupWindow(ConfigurationV1 configuration, Plugin plugin) : base("FC Name Color Config - Add new group", ImGuiWindowFlags.AlwaysAutoResize)
        {
            this.configuration = configuration;
            this.plugin = plugin;
        }

        public override void OnOpen()
        {
            base.OnOpen();
            newGroup = "";
        }

        public override bool DrawConditions() => plugin.ConfigOpen;

        public override void Draw()
        {
            var groups = configuration.Groups.Keys.Where(a => a != "Other FC" && a != "Default").Prepend("Other FC").Prepend("Default").ToArray();
            var exists = groups.Contains(newGroup);
            var add = newGroup != null && ImGui.InputTextWithHint("###NewGroup", "Your group name", ref newGroup, 50,
                ImGuiInputTextFlags.EnterReturnsTrue) && newGroup.Length > 1;

            ImGui.SameLine();

            if (newGroup != null && (newGroup.Length == 0 || exists))
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
                if (newGroup != null)
                    configuration.Groups.Add(newGroup, new Group
                    {
                        UiColor = "52",
                        Color = new Vector4(0.07450981f, 0.8f, 0.6392157f, 1f)
                    });
                configuration.Save();
                IsOpen = false;
            }
        }
    }
}
