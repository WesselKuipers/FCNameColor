using Dalamud.Game.ClientState.Actors.Types;
using Dalamud.Game.Command;
using Dalamud.Game.Internal;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Hooking;
using Dalamud.Plugin;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using static FCNameColor.XivApi;

namespace FCNameColor
{
    public class Plugin : IDalamudPlugin
    {
        public string Name => "FC Name Color";

        private const string commandName = "/fcnc";

        private DalamudPluginInterface pi;
        private Configuration configuration;
        private PluginUI ui;
        private PluginAddressResolver address;
        private Dictionary<int, PlayerPointer> cache;
        private HttpClient client;
        private string lastColor;
        private bool lastOnlyColorFCTag;
        private int characterId;
        private Hook<SetNamePlateDelegate> SetNamePlateHook;
        private bool loggingIn;

        // When loaded by LivePluginLoader, the executing assembly will be wrong.
        // Supplying this property allows LivePluginLoader to supply the correct location, so that
        // you have full compatibility when loaded normally and through LPL.
        public string AssemblyLocation { get => assemblyLocation; set => assemblyLocation = value; }
        private string assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            pi = pluginInterface;
            cache = new Dictionary<int, PlayerPointer>();
            client = new HttpClient();
            characterId = -1;

            configuration = pi.GetPluginConfig() as Configuration ?? new Configuration();
            configuration.Initialize(pi);
            lastColor = configuration.UiColor;

            address = new PluginAddressResolver();
            address.Setup(pi.TargetModuleScanner);

            XivApi.Initialize(pi, address);
            SetNamePlateHook = new Hook<SetNamePlateDelegate>(address.AddonNamePlate_SetNamePlatePtr, new SetNamePlateDelegate(SetNamePlateDetour));
            SetNamePlateHook.Enable();

            ui = new PluginUI(configuration, pi);

            pi.CommandManager.AddHandler(commandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Opens the FCNameColor configuration."
            });

            pi.ClientState.OnLogin += OnLogin;
            pi.Framework.OnUpdateEvent += OnFrameworkUpdate;
            pi.UiBuilder.OnBuildUi += DrawUI;
            pi.UiBuilder.OnOpenConfigUi += DrawConfigUI;

            if (pi.ClientState.IsLoggedIn && pi.ClientState.LocalPlayer.CompanyTag != null)
            {
                _ = FetchData();
            }
        }

        public void Dispose()
        {
            ui.Dispose();
            client.Dispose();

            pi.CommandManager.RemoveHandler(commandName);
            pi.Framework.OnUpdateEvent -= OnFrameworkUpdate;
            pi.ClientState.OnLogin -= OnLogin;
            pi.Dispose();

            SetNamePlateHook.Disable();
            SetNamePlateHook.Dispose();

            XivApi.DisposeInstance();
            foreach (var ptr in cache)
            {
                ptr.Value.Dispose();
            }
            cache.Clear();
        }

        private void OnCommand(string command, string args)
        {
            // In response to the slash command, just display our main ui
            ui.Visible = true;
        }

        private void DrawConfigUI(object sender, EventArgs e)
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
            if (loggingIn && pi.ClientState.LocalPlayer != null)
            {
                loggingIn = false;

                if (pi.ClientState.LocalPlayer.CompanyTag != null)
                {
                    PluginLog.Debug($"Logged in as {pi.ClientState.LocalPlayer.Name} @ {pi.ClientState.LocalPlayer.HomeWorld.GameData.Name}.");
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
            if (characterId == -1)
            {
                var playerSearchRequest = await client.GetAsync($"https://xivapi.com/character/search?name={pi.ClientState.LocalPlayer.Name}&server={pi.ClientState.LocalPlayer.HomeWorld.GameData.Name}");
                if (!playerSearchRequest.IsSuccessStatusCode) { return; }

                var playerSearchContent = await playerSearchRequest.Content.ReadAsStringAsync();
                var playerSearchResponse = JsonConvert.DeserializeObject<XivApiCharacterSearchResponse>(playerSearchContent);
                if (playerSearchResponse.Results.Count == 0) { return; }

                characterId = playerSearchResponse.Results.FirstOrDefault(player => player.Name == pi.ClientState.LocalPlayer.Name).ID;

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
                configuration.FcMembers = memberSearchResponse.FreeCompanyMembers;
                configuration.Save();
                PluginLog.Debug($"Fetched {configuration.FcMembers.Count} members");
            }
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
            Func<IntPtr> original = () => SetNamePlateHook.Original(namePlateObjectPtr, isPrefixTitle, displayTitle, title, name, fcName, iconID);
            if (!configuration.Enabled || configuration.FcMembers == null)
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

            var actorID = npInfo.Data.ActorID;
            if (actorID == -1)
            {
                return original();
            }

            if (!npInfo.IsPlayerCharacter())  // Only PlayerCharacters should have their colors changed.
            {
                return original();
            }

            var isLocalPlayer = npObject.IsLocalPlayer;
            var isPartyMember = npInfo.IsPartyMember();
            var isInDuty = pi.ClientState.Condition[Dalamud.Game.ClientState.ConditionFlag.BoundByDuty];

            if (isLocalPlayer && !configuration.IncludeSelf)
            {
                return original();
            }

            PlayerCharacter target = (PlayerCharacter)Enumerable.FirstOrDefault(pi.ClientState.Actors, actor => actor.ActorId == actorID);
            if (target == default(PlayerCharacter) || target.HomeWorld.Id != pi.ClientState.LocalPlayer.HomeWorld.Id)
            {
                return original();
            }

            if (!configuration.FcMembers.Exists(member => member.Name == target.Name))
            {
                return original();
            }

            if (!lastColor.Equals(configuration.UiColor) || lastOnlyColorFCTag != configuration.OnlyColorFCTag)
            {
                PluginLog.Debug($"Settings changed. Clearing cache.");
                foreach (var cacheItem in cache)
                {
                    cacheItem.Value.Dispose();
                }
                cache.Clear();
            }

            var tag = pi.SeStringManager.Parse(XivApi.ReadSeStringBytes(npInfo.FcNameAddress)).TextValue;

            if (cache.ContainsKey(actorID))
            {
                var cacheItem = cache[actorID];
                if (cacheItem.FC != tag || (!isLocalPlayer && !isPartyMember && cacheItem.Title != npInfo.Title))
                {
                    cacheItem.Dispose();
                    cache.Remove(actorID);
                }
            }

            if (!cache.ContainsKey(actorID))
            {
                var playerPointer = new PlayerPointer
                {
                    Name = npInfo.Name,
                    Title = npInfo.Title
                };

                if (!isInDuty)
                {


                    var newFCString = new SeString(new List<Payload>())
                        .Append(new UIForegroundPayload(pi.Data, Convert.ToUInt16(configuration.UiColor)))
                        .Append(new TextPayload(tag))
                        .Append(UIForegroundPayload.UIForegroundOff);
                    var newFcNamePtr = SeStringToSeStringPtr(newFCString);
                    playerPointer.FcPtr = newFcNamePtr;
                    playerPointer.FC = tag;
                }

                var shouldReplaceName = isInDuty ? (configuration.IncludeDuties && !isLocalPlayer) : (!configuration.OnlyColorFCTag && !isPartyMember && !isLocalPlayer);
                PluginLog.Debug($"shouldReplaceName: {shouldReplaceName}, OnlyColorFCTag: {configuration.OnlyColorFCTag}, isPartyMember: {isPartyMember}, isLocalPlayer: {isLocalPlayer}");
                if (shouldReplaceName)
                {
                    var newNameString = new SeString(new List<Payload>())
                        .Append(new UIForegroundPayload(pi.Data, Convert.ToUInt16(configuration.UiColor)))
                        .Append(new TextPayload(npInfo.Name))
                        .Append(UIForegroundPayload.UIForegroundOff);
                    var newNamePtr = SeStringToSeStringPtr(newNameString);
                    playerPointer.NamePtr = newNamePtr;

                    if (displayTitle && !string.IsNullOrEmpty(npInfo.Title))
                    {
                        var newTitleString = new SeString(new List<Payload>())
                          .Append(new UIForegroundPayload(pi.Data, Convert.ToUInt16(configuration.UiColor)))
                          .Append(new TextPayload($"《{npInfo.Title}》"))
                          .Append(UIForegroundPayload.UIForegroundOff);
                        var newTitlePtr = SeStringToSeStringPtr(newTitleString);
                        playerPointer.TitlePtr = newTitlePtr;
                    }
                }
                cache.Add(actorID, playerPointer);
                PluginLog.Debug($"Overriding player nameplate for {npInfo.Name} (ActorID {actorID})");
            }

            var isDead = target.CurrentHp == 0;

            var entry = cache[actorID];
            var newName = entry.NamePtr != IntPtr.Zero ? entry.NamePtr : name;
            var newTitle = entry.TitlePtr != IntPtr.Zero ? entry.TitlePtr : title;
            var newFCName = entry.FcPtr != IntPtr.Zero && !isInDuty ? entry.FcPtr : fcName;

            lastColor = configuration.UiColor;
            lastOnlyColorFCTag = configuration.OnlyColorFCTag;
            return SetNamePlateHook.Original(namePlateObjectPtr, isPrefixTitle, displayTitle, newTitle, newName, newFCName, iconID);
        }
    }
}
