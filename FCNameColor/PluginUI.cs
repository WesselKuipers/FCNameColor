using ImGuiNET;
using System;
using System.Numerics;
using Lumina.Excel.GeneratedSheets;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Data;

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

    // It is good to have this be disposable in general, in case you ever need it
    // to do any cleanup
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

            ImGui.SetNextWindowSize(new Vector2(375, 440), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSizeConstraints(new Vector2(375, 470), new Vector2(375, float.MaxValue));
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

                // can't ref a property, so use a local copy
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
                    ImGui.SetTooltip("Will color the entire names of FC members when inside a duty.");
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
                    if (ImGui.ColorButton(z.RowId.ToString(), color))
                    {
                        configuration.UiColor = z.RowId.ToString();
                        configuration.Color = color;
                        configuration.Save();
                    }
                    ImGui.NextColumn();
                }
                ImGui.Columns(1);
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
    }
}
