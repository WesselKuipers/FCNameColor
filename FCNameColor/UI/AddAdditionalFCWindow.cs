using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using FCNameColor.Config;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace FCNameColor.UI
{
    internal class AddAdditionalFCWindow : Window
    {
        private readonly ConfigurationV1 configuration;
        private readonly Plugin plugin;
        private readonly Regex fcUrlPattern = new Regex(@"https:\/\/(eu|na|jp).finalfantasyxiv.com\/lodestone\/freecompany\/(\d{19})\/*");

        private string? fcUrl;

        public AddAdditionalFCWindow(ConfigurationV1 configuration, Plugin plugin) : base("FC Name Color Config - Adding Additional FC", ImGuiWindowFlags.AlwaysAutoResize)
        {
            this.configuration = configuration;
            this.plugin = plugin;
        }

        public override void OnOpen()
        {
            fcUrl = "";
            base.OnOpen();
        }

        public override bool DrawConditions() => plugin.ConfigOpen;

        public override void Draw()
        {
            ImGui.Spacing();

            ImGui.Text("Please enter the lodestone URL of the FC.");
            ImGui.SameLine();
            if (ImGui.SmallButton("Open Lodestone"))
            {
                Process.Start("explorer", "https://eu.finalfantasyxiv.com/lodestone/community/search/");
            }

            ImGui.Text(
                "It should look like this: https://eu.finalfantasyxiv.com/lodestone/freecompany/1234567890123456789");
            ImGui.SetNextItemWidth(500f * ImGuiHelpers.GlobalScale);
            ImGui.InputTextWithHint("###FCUrl",
                "https://eu.finalfantasyxiv.com/lodestone/freecompany/1234567890123456789",
                ref fcUrl, 100);

            ImGui.SameLine();
            if (plugin.SearchingFC)
            {
                ImGuiComponents.DisabledButton("Searching FC");
            }
            else
            {
                var isMatch = fcUrl.Length > 0 && fcUrlPattern.IsMatch(fcUrl);

                if (!isMatch)
                {
                    ImGuiComponents.DisabledButton("Search FC");
                }
                else if (isMatch && ImGui.Button("Search FC"))
                {
                    var match = fcUrlPattern.Match(fcUrl);
                    var id = match.Groups[2].Value;
                    var shouldContinue = true;

                    if (plugin.PlayerKey != null && configuration.PlayerIDs.TryGetValue(plugin.PlayerKey, out var currentPlayerID))
                    {
                        if (configuration.PlayerFCIDs.TryGetValue(currentPlayerID, out var playerFC))
                        {
                            if (playerFC == id)
                            {
                                ImGui.OpenPopup("###SameFC");
                                shouldContinue = false;
                            }
                        }
                    }

                    if (shouldContinue)
                    {
                        if (plugin.PlayerKey != null && configuration.FCGroups[plugin.PlayerKey].ContainsKey(id))
                        {
                            ImGui.OpenPopup("###AddFCDupe");
                        }
                        else
                        {
                            plugin.SearchFC(id, "Other FC").ContinueWith(async success =>
                            {
                                var result = await success;
                                if (result)
                                {
                                    IsOpen = false;
                                }
                            });
                        }
                    }
                }
            }

            if (fcUrl.Length > 0 && !fcUrlPattern.IsMatch(fcUrl))
            {
                ImGui.TextColored(ImGuiColors.DalamudRed, "Url doesn’t match the FC url format.");
            }

            if (plugin.SearchingFCError != null && plugin.SearchingFCError.Length > 0)
            {
                ImGui.TextColored(ImGuiColors.DalamudRed, plugin.SearchingFCError);
            }

            if (ImGui.BeginPopup("###SameFC"))
            {
                ImGui.Text("This is your own FC, it’s already being tracked.");
                ImGui.EndPopup();
            }

            if (ImGui.BeginPopup("###AddFCDupe"))
            {
                ImGui.Text("You’ve already added this FC!");
                ImGui.EndPopup();
            }
        }
    }
}
