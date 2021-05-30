using ImGuiNET;
using System;
using System.Numerics;
using Dalamud.Plugin;
using Lumina.Excel.GeneratedSheets;
using Lumina.Excel;
using Dalamud.Interface.Components;
using System.Collections.Generic;

namespace FCNameColor
{
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

        public PluginUI(Configuration config, DalamudPluginInterface pi)
        {
            configuration = config;
            var list = new List<UIColor>(pi.Data.Excel.GetSheet<UIColor>());
            list.Sort((a, b) => {
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

        public void Dispose() {}

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

            ImGui.SetNextWindowSize(new Vector2(375, 470), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSizeConstraints(new Vector2(375, 470), new Vector2(375, float.MaxValue));
            if (ImGui.Begin("FC Name Color Config", ref visible, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoResize))
            {
                var enabled = configuration.Enabled;
                if (ImGui.Checkbox("Enabled", ref enabled))
                {
                    configuration.Enabled = enabled;
                    configuration.Save();
                }
                ImGui.SameLine();
                ImGui.Text(" * Changes may apply immediately.");

                // can't ref a property, so use a local copy
                var onlyColorFCTag = configuration.OnlyColorFCTag;
                if (ImGui.Checkbox("Only color the FC tag", ref onlyColorFCTag))
                {
                    configuration.OnlyColorFCTag = onlyColorFCTag;
                    configuration.Save();
                }

                var includeSelf = configuration.IncludeSelf;
                if (ImGui.Checkbox("Include self (Only affects FC tag)", ref includeSelf))
                {
                    configuration.IncludeSelf = includeSelf;
                    configuration.Save();
                }

                ImGui.ColorButton($"Nameplate color. Click on a color below to select a new one.", configuration.Color);
                ImGui.SameLine();
                ImGui.Text("Nameplate color. Click on a color below to set a new one.");
                ImGui.Separator();
                ImGui.Columns(12, "columns", false);
                foreach (var z in uiColors)
                {
                    if (z.UIForeground == 0 || z.UIForeground == 255) { 
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
