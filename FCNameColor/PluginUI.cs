using ImGuiNET;
using System;
using System.Numerics;
using Lumina.Excel.GeneratedSheets;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Data;
using Dalamud.Interface;
using Dalamud.Logging;
using Lumina;

namespace FCNameColor
{
    class UIColorComparer : IEqualityComparer<UIColor>
    {
        public bool Equals(UIColor x, UIColor y)
        {
            return x.UIForeground == y.UIForeground; // based on variable i
        }
        public int GetHashCode(UIColor obj)
        {
            return obj.UIForeground.GetHashCode(); // hashcode of variable to compare
        }
    }

    class PluginUI : IDisposable
    {
        private readonly Configuration configuration;
        private readonly List<UIColor> uiColors;

        // this extra bool exists for ImGui, since you can't ref a property
        private bool visible = false;
        public bool Visible
        {
            get { return visible; }
            set { visible = value; }
        }

        private bool showIgnoreList;
        private FCMember currentIgnoredPlayer;

        public PluginUI(Configuration config, DataManager data)
        {
            configuration = config;

            var list = new List<UIColor>(data.GetExcelSheet<UIColor>().Distinct(new UIColorComparer()));
            list.Sort((a, b) =>
            {
                var colorA = ConvertUIColorToColor(a);
                var colorB = ConvertUIColorToColor(b);
                ImGui.ColorConvertRGBtoHSV(colorA.X, colorA.Y, colorA.Z, out float aH, out float aS, out float aV);
                ImGui.ColorConvertRGBtoHSV(colorB.X, colorB.Y, colorB.Z, out float bH, out float bS, out float bV);

                var hue = aH.CompareTo(bH);
                if (hue != 0) { return hue; }

                var saturation = aS.CompareTo(bS);
                if (saturation != 0) { return saturation; }

                var value = aV.CompareTo(bV);
                if (value != 0) { return value; }

                return 0;
            });
            uiColors = list;
        }

        public void Dispose() { }

        public void Draw()
        {
            DrawMainWindow();
        }

        public void DrawMainWindow()
        {
            if (!Visible)
            {
                return;
            }

            ImGui.SetNextWindowSize(new Vector2(375, 500), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSizeConstraints(new Vector2(375, 500), new Vector2(375, float.MaxValue));
            if (ImGui.Begin("FC Name Color Config", ref visible, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
            {
                var enabled = configuration.Enabled;
                if (ImGui.Checkbox("Enabled", ref enabled))
                {
                    configuration.Enabled = enabled;
                    configuration.Save();
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Changes may take a couple of seconds to apply.");
                }

                if (Plugin.Loading)
                {
                    ImGui.SameLine();
                    ImGui.Text(" Fetching FC members from Lodestone...");
                }

                var onlyColorFCTag = configuration.OnlyColorFCTag;
                if (ImGui.Checkbox("Only color the FC tag", ref onlyColorFCTag))
                {
                    configuration.OnlyColorFCTag = onlyColorFCTag;
                    configuration.Save();
                }

                var includeSelf = configuration.IncludeSelf;
                if (ImGui.Checkbox("Include self", ref includeSelf))
                {
                    configuration.IncludeSelf = includeSelf;
                    configuration.Save();
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("This will colour your own FC tag.");
                }

                var includeDuties = configuration.IncludeDuties;
                if (ImGui.Checkbox("Include duties", ref includeDuties))
                {
                    configuration.IncludeDuties = includeDuties;
                    configuration.Save();
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Will colour the entire names of FC members when inside a duty.");
                }

                var glow = configuration.Glow;
                if (ImGui.Checkbox("Enable glow", ref glow))
                {
                    configuration.Glow = glow;
                    configuration.Save();
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Makes outline of the nameplates thicker.");
                }

                ImGui.ColorButton($"Nameplate color. Click on a color below to select a new one.", configuration.Color);
                ImGui.SameLine();
                ImGui.Text("Nameplate color. Click on a color below to set a new one.");
                ImGui.Separator();
                ImGui.Columns(12, "columns", false);
                foreach (var z in uiColors)
                {
                    if (z.UIForeground == 0 || z.UIForeground == 255)
                    {
                        continue;
                    }

                    var color = ConvertUIColorToColor(z);
                    var id = z.RowId.ToString();
                    var oldCursor = ImGui.GetCursorPos();
                    
                    if (ImGui.ColorButton(id, color))
                    {
                        configuration.UiColor = id;
                        configuration.Color = color;
                        configuration.Save();
                    }

                    if (id == configuration.UiColor)
                    {
                        // For the selected colour, render a transparent checkmark on top
                        var newCursor = ImGui.GetCursorPos();
                        ImGui.SetCursorPos(oldCursor);
                        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, Vector4.Zero);
                        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, Vector4.Zero);
                        ImGui.PushStyleColor(ImGuiCol.FrameBg, Vector4.Zero);
                        var selected = true;
                        ImGui.Checkbox("Selected", ref selected);
                        ImGui.PopStyleColor(3);
                        ImGui.SetCursorPos(newCursor);
                    }
                    ImGui.NextColumn();
                }
                ImGui.Columns(1);
            }

            ImGui.Separator();
            ImGui.Spacing();
            if (ImGui.SmallButton("Ignore List"))
            {
                this.showIgnoreList = !this.showIgnoreList;
            }

            if (this.showIgnoreList)
            {
                ImGui.SetNextWindowSize(new Vector2(270, 200), ImGuiCond.FirstUseEver);
                ImGui.Begin("FC Name Color Config - Ignore List", ref showIgnoreList);
                ImGui.TextWrapped("Don't update nameplates for these players.");
                ImGui.Spacing();
                ImGui.SetNextItemWidth(150f * ImGuiHelpers.GlobalScale);
                var fcMembers = this.GetFCMembers();
                var playerNames = fcMembers.Select(member => member.Name).ToArray();
                var playerIndex = Array.IndexOf(playerNames, this.currentIgnoredPlayer.Name);
                ImGui.SetNextItemWidth(170f * ImGuiHelpers.GlobalScale);
                if (ImGui.Combo(
                    "###AddPlayerToIgnoreList",
                    ref playerIndex,
                    playerNames,
                    playerNames.Length))
                {
                    this.currentIgnoredPlayer = fcMembers[playerIndex];
                }
                ImGui.SameLine();
                if (ImGui.SmallButton("Add Player"))
                {
                    if (this.configuration.IgnoredPlayers.ContainsKey(this.currentIgnoredPlayer.Name))
                    {
                        ImGui.OpenPopup("###AddPlayerToIgnoreListDupe");
                    }
                    else
                    {
                        this.configuration.IgnoredPlayers.Add(this.currentIgnoredPlayer.Name, this.currentIgnoredPlayer.ID);
                        this.configuration.Save();
                    }
                }

                if (ImGui.BeginPopup("###AddPlayerToIgnoreListDupe"))
                {
                    ImGui.Text("You've already added this player!");
                    ImGui.EndPopup();
                }
                
                foreach (var (key, _) in this.configuration.IgnoredPlayers.ToList())
                {
                    ImGui.Spacing();
                    ImGui.Text(key);
                    ImGui.SameLine();
                    ImGui.BeginGroup();
                    ImGui.PushFont(UiBuilder.IconFont);
                    ImGui.Text(FontAwesomeIcon.Times.ToIconString());
                    ImGui.PopFont();
                    ImGui.EndGroup();
                    if (!ImGui.IsItemClicked(ImGuiMouseButton.Left)) continue;
                    this.configuration.IgnoredPlayers.Remove(key);
                    this.configuration.Save();
                }
                ImGui.End();
            }
            ImGui.End();
        }

        private Vector4 ConvertUIColorToColor(UIColor uiColor)
        {
            var temp = BitConverter.GetBytes(uiColor.UIForeground);
            return new Vector4((float)temp[3] / 255,
                (float)temp[2] / 255,
                (float)temp[1] / 255,
                (float)temp[0] / 255);
        }

        private List<FCMember> GetFCMembers()
        {
            var fcMembers = new List<FCMember>();
            var playersFCs = this.configuration.PlayerFCs;
            foreach (var playerFC in playersFCs)
            {
                fcMembers.AddRange(playerFC.Value.Members);
            }

            fcMembers = fcMembers.Distinct().OrderBy(member => member.Name).ToList();

            return fcMembers;
        }
    }
}
