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
            SetNamePlateHook = new Hook<SetNamePlateDelegate>(address.AddonNamePlate_SetNamePlatePtr, new SetNamePlateDelegate(SetNamePlateDetour), this);
            SetNamePlateHook.Enable();

            ui = new PluginUI(configuration, pi);

            pi.CommandManager.AddHandler(commandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Color your FC’s tag or the entire nameplate if they are in your FC. Use /fcnc to open up the config."
            });

            pi.ClientState.OnLogin += OnLogin;
            pi.Framework.OnUpdateEvent += OnFrameworkUpdate;
            pi.UiBuilder.OnBuildUi += DrawUI;
            
            if (pi.ClientState.IsLoggedIn && pi.ClientState.LocalPlayer.CompanyTag != null)
            {
                FetchData();
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
            // in response to the slash command, just display our main ui
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
                    FetchData().Wait();
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

                characterId = playerSearchResponse.Results[0].ID;
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
            if (!configuration.Enabled || string.IsNullOrEmpty(pi.ClientState.LocalPlayer.CompanyTag) || configuration.FcMembers == null || pi.ClientState.LocalPlayer.CompanyTag == null)
            {
                return SetNamePlateHook.Original(namePlateObjectPtr, isPrefixTitle, displayTitle, title, name, fcName, iconID);
            }

            var npObject = new XivApi.SafeNamePlateObject(namePlateObjectPtr);
            if (npObject == null)
            {
                return SetNamePlateHook.Original(namePlateObjectPtr, isPrefixTitle, displayTitle, title, name, fcName, iconID);
            }

            var npInfo = npObject.NamePlateInfo;
            if (npInfo == null)
            {
                return SetNamePlateHook.Original(namePlateObjectPtr, isPrefixTitle, displayTitle, title, name, fcName, iconID);
            }

            var actorID = npInfo.Data.ActorID;
            if (actorID == -1)
            {
                return SetNamePlateHook.Original(namePlateObjectPtr, isPrefixTitle, displayTitle, title, name, fcName, iconID);
            }

            if (!npInfo.IsPlayerCharacter())  // Only PlayerCharacters should have their colors changed.
            {
                return SetNamePlateHook.Original(namePlateObjectPtr, isPrefixTitle, displayTitle, title, name, fcName, iconID);
            }

            var isLocalPlayer = npObject.IsLocalPlayer;
            var isPartyMember = npInfo.IsPartyMember();

            if (isLocalPlayer && !configuration.IncludeSelf)
            {
                return SetNamePlateHook.Original(namePlateObjectPtr, isPrefixTitle, displayTitle, title, name, fcName, iconID);
            }

            PlayerCharacter target = (PlayerCharacter)Enumerable.First(pi.ClientState.Actors, actor => actor.ActorId == actorID);
            if ((target).HomeWorld.Id != pi.ClientState.LocalPlayer.HomeWorld.Id)
            {
                return SetNamePlateHook.Original(namePlateObjectPtr, isPrefixTitle, displayTitle, title, name, fcName, iconID);
            }

            if (!configuration.FcMembers.Exists(member => member.Name == target.Name))
            {
                return SetNamePlateHook.Original(namePlateObjectPtr, isPrefixTitle, displayTitle, title, name, fcName, iconID);
            }

            if (!lastColor.Equals(configuration.UiColor))
            {
                PluginLog.Debug($"Color was set to UIColour {configuration.UiColor}. Clearing cache.");
                foreach (var cacheItem in cache)
                {
                    cacheItem.Value.Dispose();
                }
                cache.Clear();
            }

            if (cache.ContainsKey(actorID))
            {
                var cacheItem = cache[actorID];
                if (cacheItem.FC != target.CompanyTag || (!isLocalPlayer && !isPartyMember && cacheItem.Title != npInfo.Title))
                {
                    cacheItem.Dispose();
                    cache.Remove(actorID);
                }
            }

            if (!cache.ContainsKey(actorID))
            {
                var newFCString = new SeString(new List<Payload>())
                    .Append(new UIForegroundPayload(pi.Data, Convert.ToUInt16(configuration.UiColor)))
                    .Append(new TextPayload($" «{target.CompanyTag}»"))
                    .Append(UIForegroundPayload.UIForegroundOff);
                var newFcNamePtr = SeStringToSeStringPtr(newFCString);
                var playerPointer = new PlayerPointer() { FcPtr = newFcNamePtr, FC = target.CompanyTag };

                if (!configuration.OnlyColorFCTag && !isPartyMember && !isLocalPlayer)
                {
                    var newNameString = new SeString(new List<Payload>())
                        .Append(new UIForegroundPayload(pi.Data, Convert.ToUInt16(configuration.UiColor)))
                        .Append(new TextPayload(npInfo.Name))
                        .Append(UIForegroundPayload.UIForegroundOff);
                    var newNamePtr = SeStringToSeStringPtr(newNameString);
                    playerPointer.NamePtr = newNamePtr;
                    playerPointer.Name = npInfo.Name;

                    if (displayTitle)
                    {
                        var newTitleString = new SeString(new List<Payload>())
                          .Append(new UIForegroundPayload(pi.Data, Convert.ToUInt16(configuration.UiColor)))
                          .Append(new TextPayload($"《{npInfo.Title}》"))
                          .Append(UIForegroundPayload.UIForegroundOff);
                        var newTitlePtr = SeStringToSeStringPtr(newTitleString);
                        playerPointer.TitlePtr = newTitlePtr;
                        playerPointer.Title = npInfo.Title;
                    }
                }
                cache.Add(actorID, playerPointer);
                PluginLog.Debug($"Overriding player tag for {npInfo.Name} (ActorID {actorID})");
            }

            var entry = cache[actorID];
            var newName = !configuration.OnlyColorFCTag && !isPartyMember && !isLocalPlayer && entry.NamePtr != null ? entry.NamePtr : name;
            var newTitle = !configuration.OnlyColorFCTag && !isPartyMember && !isLocalPlayer && entry.TitlePtr != null ? entry.TitlePtr : title;
            var newFCName = entry.FcPtr != null ? entry.FcPtr : fcName;

            lastColor = configuration.UiColor;
            return SetNamePlateHook.Original(namePlateObjectPtr, isPrefixTitle, displayTitle, newTitle, newName, newFCName, iconID);
        }
    }
}
