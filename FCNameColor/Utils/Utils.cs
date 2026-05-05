using System;
using Dalamud.Bindings.ImGui;

namespace FCNameColor.Utils
{
        internal static class ImGuiUtils
        {
            public static void AddCheckbox(string label, string tooltip, bool currentValue, Action<bool> setter, ref bool isDirty)
            {
                var value = currentValue;
                if (ImGui.Checkbox(label, ref value))
                {
                    setter(value);
                    isDirty = true;
                }

                if (tooltip.Length == 0) return;
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(tooltip);
                }

            }
        }
        
}
