using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Party;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Hooking;
using Dalamud.IoC;
using Dalamud.Logging;
using Dalamud.Plugin;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

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
        public static GameGui GUI { get; private set; }

        [PluginService]
        public static Condition Condition { get; private set; }

        [PluginService]
        public static ObjectTable Objects { get; private set; }

    [PluginService]
        public static CommandManager Commands { get; private set; }

        [PluginService]
        public static Framework Framework { get; private set; }

        [PluginService]
        public static PartyList PartyList { get; private set; }

        private PluginAddressResolver address;
        private PluginUI ui { get; }
        private Dictionary<uint, PlayerPointer> cache;
        private HttpClient client;
        private int lastSettings;
        private int characterId;
        private Hook<SetNamePlateDelegate> SetNamePlateHook;
        private bool loggingIn;
        private bool firstTime = false;
        public static bool Loading;

        public Plugin(SigScanner scanner, GameGui gui, DataManager dataManager)
        {
            cache = new Dictionary<uint, PlayerPointer>();
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

            address = new PluginAddressResolver();
            address.Setup(scanner);

            XivApi.Initialize(address);
            SetNamePlateHook = new Hook<SetNamePlateDelegate>(address.AddonNamePlate_SetNamePlatePtr, new SetNamePlateDelegate(SetNamePlateDetour));
            SetNamePlateHook.Enable();

            ui = new PluginUI(config, dataManager);

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
            ui.Visible = true;
        }

        private void DrawConfigUI()
        {
            ui.Visible = true;
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
            ui.Draw();
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

        internal IntPtr SetNamePlateDetour(IntPtr namePlateObjectPtr, bool isPrefixTitle, bool displayTitle, IntPtr title, IntPtr name, IntPtr fcName, int iconID)
        {
            try
            {
                return SetNamePlate(namePlateObjectPtr, isPrefixTitle, displayTitle, title, name, fcName, iconID);
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, $"SetNamePlateDetour encountered a critical error");
            }

            return SetNamePlateHook.Original(namePlateObjectPtr, isPrefixTitle, displayTitle, title, name, fcName, iconID);
        }

        internal IntPtr SetNamePlate(IntPtr namePlateObjectPtr, bool isPrefixTitle, bool displayTitle, IntPtr title, IntPtr name, IntPtr fcName, int iconID)
        {
            IntPtr original() => SetNamePlateHook.Original(namePlateObjectPtr, isPrefixTitle, displayTitle, title, name, fcName, iconID);
            if (!config.Enabled || config.FcMembers == null)
            {
                return original();
            }

            var npObject = new XivApi.SafeNamePlateObject(namePlateObjectPtr);
            if (npObject == null)
            {
                return original();
            }

            var npInfo = npObject.NamePlateInfo;
            if (npInfo == null)
            {
                return original();
            }

            var objectID = npInfo.Data.ObjectID.ObjectID;
            if (!npInfo.IsPlayerCharacter())  // Only PlayerCharacters should have their colors changed.
            {
                return original();
            }

            var isLocalPlayer = npObject.IsLocalPlayer;
            var isPartyMember = npInfo.IsPartyMember();
            var isInDuty = Condition[ConditionFlag.BoundByDuty];

            if (isLocalPlayer && !config.IncludeSelf)
            {
                return original();
            }

            PlayerCharacter target = (PlayerCharacter)Enumerable.FirstOrDefault(Objects, actor => actor.ObjectId == objectID);
            if (target == default(PlayerCharacter) || target.HomeWorld.Id != ClientState.LocalPlayer.HomeWorld.Id)
            {
                return original();
            }

            if (!config.FcMembers.Exists(member => member.Name == target.Name.TextValue))
            {
                return original();
            }

            if (lastSettings != config.GetHashCode())
            {
                PluginLog.Debug($"Settings changed. Clearing cache.");
                foreach (var cacheItem in cache)
                {
                    cacheItem.Value.Dispose();
                }
                cache.Clear();
            }

            var tag = SeString.Parse(XivApi.ReadSeStringBytes(npInfo.FcNameAddress)).TextValue;

            if (cache.ContainsKey(objectID))
            {
                var cacheItem = cache[objectID];
                if (cacheItem.FC != tag || (!isLocalPlayer && !isPartyMember && cacheItem.Title != npInfo.Title))
                {
                    cacheItem.Dispose();
                    cache.Remove(objectID);
                }
            }

            if (!cache.ContainsKey(objectID))
            {
                var playerPointer = new PlayerPointer
                {
                    Name = npInfo.Name,
                    Title = npInfo.Title
                };

                if (!isInDuty)
                {
                    var newFCString = BuildSEString(tag);
                    var newFcNamePtr = XivApi.SeStringToSeStringPtr(newFCString);
                    playerPointer.FcPtr = newFcNamePtr;
                    playerPointer.FC = tag;
                }

                var shouldReplaceName = isInDuty ? (config.IncludeDuties && !isLocalPlayer) : (!config.OnlyColorFCTag && !isPartyMember && !isLocalPlayer);
                PluginLog.Debug($"Name: {npInfo.Name}, shouldReplaceName: {shouldReplaceName}, IsInDuty: {isInDuty}, OnlyColorFCTag: {config.OnlyColorFCTag}, isPartyMember: {isPartyMember}, isLocalPlayer: {isLocalPlayer}");
                if (shouldReplaceName)
                {
                    var newNameString = BuildSEString(SeString.Parse(XivApi.ReadSeStringBytes(npInfo.NameAddress)).TextValue);
                    var newNamePtr = XivApi.SeStringToSeStringPtr(newNameString);
                    playerPointer.NamePtr = newNamePtr;

                    if (displayTitle && !string.IsNullOrEmpty(npInfo.Title))
                    {
                        var newTitleString = BuildSEString($"《{SeString.Parse(XivApi.ReadSeStringBytes(npInfo.TitleAddress)).TextValue}》");
                        var newTitlePtr = XivApi.SeStringToSeStringPtr(newTitleString);
                        playerPointer.TitlePtr = newTitlePtr;
                    }
                }
                cache.Add(objectID, playerPointer);
                PluginLog.Debug($"Overriding player nameplate for {npInfo.Name} (ActorID {objectID})");
            }

            var isDead = target.CurrentHp == 0;

            var entry = cache[objectID];
            var newName = entry.NamePtr != IntPtr.Zero ? entry.NamePtr : name;
            var newTitle = entry.TitlePtr != IntPtr.Zero ? entry.TitlePtr : title;
            var newFCName = entry.FcPtr != IntPtr.Zero && !isInDuty ? entry.FcPtr : fcName;

            lastSettings = config.GetHashCode();
            return SetNamePlateHook.Original(namePlateObjectPtr, isPrefixTitle, displayTitle, newTitle, newName, newFCName, iconID);
        }

        protected virtual void Dispose(bool disposing)
        {
            try
            {
                if (disposing)
                {
                    ui.Dispose();
                    client.Dispose();

                    Commands.RemoveHandler(commandName);
                    Framework.Update -= OnFrameworkUpdate;
                    ClientState.Login -= OnLogin;
                    Pi.Dispose();

                    SetNamePlateHook.Disable();
                    SetNamePlateHook.Dispose();

                    XivApi.DisposeInstance();
                    foreach (var ptr in cache)
                    {
                        ptr.Value.Dispose();
                    }
                    cache.Clear();
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
