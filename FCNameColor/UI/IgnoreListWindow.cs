using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Utility;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using FCNameColor.Config;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace FCNameColor.UI
{
    internal class IgnoreListWindow : Window
    {
        private readonly ConfigurationV1 configuration;
        private readonly Plugin plugin;
        private FCMember currentIgnoredPlayer;

        public IgnoreListWindow(ConfigurationV1 configuration, Plugin plugin) : base("FC Name Color Config - Ignore List")
        {
            this.configuration = configuration;
            this.plugin = plugin;

            Size = new Vector2(270, 200);
            SizeCondition = ImGuiCond.FirstUseEver;
        }

        public override bool DrawConditions()
        {
            return plugin.ConfigOpen && base.DrawConditions();
        }

        public override void Draw()
        {
            ImGui.TextWrapped("Don’t update nameplates for these players.");
            ImGui.Spacing();
            ImGui.SetNextItemWidth(150f * ImGuiHelpers.GlobalScale);
            var fcMembers = GetFCMembers();
            var playerNames = fcMembers.Select(member => member.Name).ToArray();
            var playerIndex = Array.IndexOf(playerNames, currentIgnoredPlayer.Name);
            ImGui.SetNextItemWidth(170f * ImGuiHelpers.GlobalScale);

            if (ImGui.Combo(
                    "###AddPlayerToIgnoreList",
                    ref playerIndex,
                    playerNames,
                    playerNames.Length))
            {
                currentIgnoredPlayer = fcMembers[playerIndex];
            }

            ImGui.SameLine();
            if (ImGui.SmallButton("Add Player"))
            {
                if (configuration.IgnoredPlayers.ContainsKey(currentIgnoredPlayer.Name))
                {
                    ImGui.OpenPopup("###AddPlayerToIgnoreListDupe");
                }
                else
                {
                    configuration.IgnoredPlayers.Add(currentIgnoredPlayer.Name,
                        currentIgnoredPlayer.ID);
                    configuration.Save();
                }
            }

            using (var visible = ImRaii.Popup("###AddPlayerToIgnoreListDupe"))
            {
                if (visible)
                {
                    ImGui.Text("You’ve already added this player!");
                }
            }

            foreach (var (key, _) in configuration.IgnoredPlayers.ToList())
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
                configuration.IgnoredPlayers.Remove(key);
                configuration.Save();
            }
        }

        private List<FCMember> GetFCMembers()
        {
            var fcMembers = new List<FCMember>();
            var playersFCs = configuration.PlayerFCIDs;
            // TODO: Should this fetch *every* tracked FC or just the player's FCs?
            foreach (var playerFCID in playersFCs)
            {
                var exists = configuration.FCs.TryGetValue(playerFCID.Value, out var fc);
                fcMembers.AddRange(fc.Members);
            }

            fcMembers = fcMembers.Distinct().OrderBy(member => member.Name).ToList();

            return fcMembers;
        }
    }
}
