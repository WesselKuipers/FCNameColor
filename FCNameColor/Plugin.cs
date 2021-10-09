using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.IoC;
using Dalamud.Logging;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using XivCommon;
using XivCommon.Functions.NamePlates;

namespace FCNameColor
{
    public class Plugin : IDalamudPlugin
    {
        public string Name => "FC Name Color";

        private const string commandName = "/fcnc";

        [PluginService]
        public static DalamudPluginInterface Pi { get; private set; }

        private readonly Configuration config;

        [PluginService]
        public static ClientState ClientState { get; private set; }

        [PluginService]
        public static ChatGui Chat { get; private set; }

        [PluginService]
        public static Condition Condition { get; private set; }

        [PluginService]
        public static ObjectTable Objects { get; private set; }

        [PluginService]
        public static CommandManager Commands { get; private set; }

        [PluginService]
        public static Framework Framework { get; private set; }


        private readonly XivCommonBase XivCommonBase;
        private PluginUI UI { get; }
        private readonly HttpClient client;
        private int lastSettings;
        private int characterId;
        private bool loggingIn;
        private bool firstTime = false;
        public static bool Loading;

        public Plugin(SigScanner scanner, GameGui gui, DataManager dataManager)
        {
            client = new HttpClient();

            characterId = -1;

            config = Pi.GetPluginConfig() as Configuration;
            if (config == null)
            {
                firstTime = true;
                config = new Configuration();
            }
            config.Initialize(Pi);
            lastSettings = config.GetHashCode();

            XivCommonBase = new XivCommonBase(Hooks.NamePlates);
            XivCommonBase.Functions.NamePlates.OnUpdate += NamePlates_OnUpdate;

            UI = new PluginUI(config, dataManager);

            Commands.AddHandler(commandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Opens the FCNameColor Config."
            });

            ClientState.Login += OnLogin;
            Framework.Update += OnFrameworkUpdate;
            Pi.UiBuilder.Draw += DrawUI;
            Pi.UiBuilder.OpenConfigUi += DrawConfigUI;

            if (ClientState.IsLoggedIn && ClientState.LocalPlayer.CompanyTag != null)
            {
                _ = FetchData();
            }
        }

        private void OnCommand(string command, string args)
        {
            UI.Visible = true;
        }

        private void DrawConfigUI()
        {
            UI.Visible = true;
        }

        private void OnLogin(object sender, EventArgs e)
        {
            // LocalPlayer is still null at this point, so we just set a flag that indicates we're logging in.
            loggingIn = true;
            characterId = -1;
        }

        private void OnFrameworkUpdate(Framework framework)
        {
            if (loggingIn && ClientState.LocalPlayer != null)
            {
                loggingIn = false;

                if (ClientState.LocalPlayer.CompanyTag != null)
                {
                    PluginLog.Debug($"Logged in as {ClientState.LocalPlayer.Name} @ {ClientState.LocalPlayer.HomeWorld.GameData.Name}.");
                    _ = FetchData();
                }
            }
        }

        private void DrawUI()
        {
            UI.Draw();
        }

        private async Task FetchData()
        {
            Loading = true;

            if (firstTime)
            {
                Chat.Print("[FCNameColor]: First-time setup - Fetching FC members from Lodestone. Plugin will work once this is done.");
            }

            if (characterId == -1)
            {
                var playerSearchRequest = await client.GetAsync($"https://xivapi.com/character/search?name={ClientState.LocalPlayer.Name}&server={ClientState.LocalPlayer.HomeWorld.GameData.Name}");
                if (!playerSearchRequest.IsSuccessStatusCode) { return; }

                var playerSearchContent = await playerSearchRequest.Content.ReadAsStringAsync();
                var playerSearchResponse = JsonConvert.DeserializeObject<XivApiCharacterSearchResponse>(playerSearchContent);
                if (playerSearchResponse.Results.Count == 0) { return; }

                characterId = playerSearchResponse.Results.FirstOrDefault(player => player.Name == ClientState.LocalPlayer.Name.TextValue).ID;

                if (characterId == default)
                {
                    PluginLog.Error("Could not find your character.");
                    return;
                }
            }

            var memberSearchRequest = await client.GetAsync($"https://xivapi.com/character/{characterId}?data=FCM");
            if (!memberSearchRequest.IsSuccessStatusCode) { return; }

            var memberSearchContent = await memberSearchRequest.Content.ReadAsStringAsync();
            var memberSearchResponse = JsonConvert.DeserializeObject<XivApiMemberSearchResponse>(memberSearchContent);
            if (memberSearchResponse.FreeCompanyMembers != null)
            {
                config.FcMembers = memberSearchResponse.FreeCompanyMembers;
                config.Save();
                PluginLog.Debug($"Fetched {config.FcMembers.Count} members");

                if (firstTime)
                {
                    Chat.Print($"[FCNameColor]: First-time setup finished. Fetched {config.FcMembers.Count} members.");
                    firstTime = false;
                }
            }
            Loading = false;
        }

        private SeString BuildSEString(string content)
        {
            return new SeString(new List<Payload>())
                    .Append(new UIForegroundPayload(Convert.ToUInt16(config.UiColor)))
                    .Append(new UIGlowPayload(config.Glow ? Convert.ToUInt16(config.UiColor) : (ushort)0))
                    .Append(new TextPayload(content))
                    .Append(UIGlowPayload.UIGlowOff)
                    .Append(UIForegroundPayload.UIForegroundOff);
        }


        private unsafe void NamePlates_OnUpdate(NamePlateUpdateEventArgs args)
        {
            if (!config.Enabled || config.FcMembers == null)
            {
                return;
            }

            if (args.Type != PlateType.Player || args.ObjectId == 0)
            {
                return;
            }

            var objectID = args.ObjectId;
            var target = (PlayerCharacter)Objects.SearchById(objectID);

            var isLocalPlayer = ClientState.LocalPlayer.ObjectId == objectID;
            var isPartyMember = GroupManager.Instance()->IsObjectIDInAlliance(objectID);
            var isInDuty = Condition[ConditionFlag.BoundByDuty];
            
            if (target == default(PlayerCharacter) || (isLocalPlayer && !config.IncludeSelf))
            {
                return;
            }

            if (target.CurrentHp == 0)
            {
                return;
            }

            if (target.HomeWorld.Id != ClientState.LocalPlayer.HomeWorld.Id)
            {
                return;
            }

            if (!config.FcMembers.Exists(member => member.Name == target.Name.TextValue))
            {
                return;
            }

            if (!isInDuty)
            {
                var newFCString = BuildSEString(args.FreeCompany.TextValue);
                args.FreeCompany = newFCString;
            }

            var shouldReplaceName = isInDuty ? (config.IncludeDuties && !isLocalPlayer) : (!config.OnlyColorFCTag && !isPartyMember && !isLocalPlayer);
            PluginLog.Debug($"Name: {args.Name.TextValue}, shouldReplaceName: {shouldReplaceName}, IsInDuty: {isInDuty}, OnlyColorFCTag: {config.OnlyColorFCTag}, isPartyMember: {isPartyMember}, isLocalPlayer: {isLocalPlayer}");
            if (shouldReplaceName)
            {
                var newNameString = BuildSEString(args.Name.TextValue);
                args.Name = newNameString;

                if (args.Title != null)
                {
                    var newTitleString = BuildSEString(args.Title.TextValue);
                    args.Title = newTitleString;
                }
            }
            PluginLog.Debug($"Overriding player nameplate for {args.Name.TextValue} (ActorID {objectID})");

            lastSettings = config.GetHashCode();
        }

       
        protected virtual void Dispose(bool disposing)
        {
            try
            {
                if (disposing)
                {
                    UI.Dispose();
                    client.Dispose();

                    Commands.RemoveHandler(commandName);
                    Framework.Update -= OnFrameworkUpdate;
                    ClientState.Login -= OnLogin;
                    Pi.Dispose();

                    XivCommonBase.Functions.NamePlates.OnUpdate -= NamePlates_OnUpdate;
                    XivCommonBase.Dispose();
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Failed to dispose properly.");
            }

        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
