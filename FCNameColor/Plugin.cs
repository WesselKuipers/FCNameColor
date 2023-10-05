using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using System.Timers;
using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Hooking;
using Dalamud.IoC;
using Dalamud.Logging;
using Dalamud.Memory;
using Dalamud.Plugin;
using FCNameColor.Utils;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;
using static FCNameColor.Utils.Utils;
using NetStone;
using NetStone.Model.Parseables.FreeCompany.Members;
using NetStone.Search.Character;
using static FFXIVClientStructs.FFXIV.Client.UI.RaptureAtkModule;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Dalamud.Plugin.Services;

namespace FCNameColor
{
    public class Plugin : IDalamudPlugin
    {
        public string Name => "FC Name Color";
        private const string CommandName = "/fcnc";
        private readonly Configuration config;

        [PluginService] public static DalamudPluginInterface Pi { get; private set; }
        [PluginService] public static SigScanner SigScanner { get; private set; }
        [PluginService] public static IClientState ClientState { get; private set; }
        [PluginService] public static IChatGui Chat { get; private set; }
        [PluginService] public static ICondition Condition { get; private set; }
        [PluginService] public static IObjectTable Objects { get; private set; }
        [PluginService] public static ICommandManager Commands { get; private set; }
        [PluginService] public static IFramework Framework { get; private set; }
        [PluginService] public static IGameInteropProvider GameInteropProvider { get; private set; }
        [PluginService] public static IPluginLog PluginLog { get; private set; }

        private Dictionary<uint, string> WorldNames;
        private LodestoneClient lodestoneClient;
        private readonly FCNameColorProvider fcNameColorProvider;
        private PluginUI UI { get; }
        private bool loggingIn;
        private readonly Timer timer = new() { Interval = 1000 };
        private bool initialized;
        private string playerName;
        private string worldName;
        private readonly HashSet<uint> skipCache = new();

        private unsafe delegate void* UpdateNameplateDelegate(RaptureAtkModule* raptureAtkModule, NamePlateInfo* namePlateInfo, NumberArrayData* numArray, StringArrayData* stringArray, GameObject* gameObject, int numArrayIndex, int stringArrayIndex);
        private Hook<UpdateNameplateDelegate> updateNameplateHook;

        public bool FirstTime;
        public bool Loading;
        public const int CooldownTime = 10;
        public int Cooldown;
        public bool NotInFC;
        public bool Error;
        public FC? FC;
        public List<FC> TrackedFCs = new();
        public string PlayerKey;
        public bool SearchingFC;
        public string SearchingFCError = "";

        public Plugin(IDataManager dataManager)
        {
            config = Pi.GetPluginConfig() as Configuration;
            timer.Elapsed += delegate
            {
                Cooldown -= 1;
                if (Cooldown <= 0)
                {
                    timer.Stop();
                }
            };

            if (config == null)
            {
                FirstTime = true;
                config = new Configuration();
            }

            config.Initialize(Pi);
            if (!config.Groups.ContainsKey("Other FC"))
            {
                config.Groups.Add("Other FC", new Group
                {
                    UiColor = "52",
                    Color = new Vector4(0.07450981f, 0.8f, 0.6392157f, 1f)
                });
                config.Save();
            }

            var invalidFCs = config.FCs.Where(fc => fc.Value.Name == null);
            if (invalidFCs.Any())
            {
                // Remove any invalid entry from the list of invalid FCs.
                // This prevents unexpected behaviour if an FC got added without proper data.
                foreach (var fc in invalidFCs)
                {
                    config.FCs.Remove(fc.Key);
                }

                config.Save();
            }

            var worlds = dataManager.GetExcelSheet<World>();
            WorldNames = new Dictionary<uint, string>(worlds.Select(w => new KeyValuePair<uint, string>(w.RowId, w.Name)));

            unsafe
            {
                var npAddress = SigScanner.ScanText("40 55 56 57 41 56 48 81 EC ?? ?? ?? ?? 48 8B 84 24");
                updateNameplateHook = GameInteropProvider.HookFromAddress<UpdateNameplateDelegate>(npAddress, UpdateNameplatesDetour);
                updateNameplateHook.Enable();
            }

            UI = new PluginUI(config, dataManager, this, ClientState);

            Commands.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Opens the FCNameColor Config."
            });

            ClientState.Login += OnLogin;
            Framework.Update += OnFrameworkUpdate;
            Pi.UiBuilder.Draw += DrawUI;
            Pi.UiBuilder.OpenConfigUi += DrawConfigUI;

            fcNameColorProvider = new FCNameColorProvider(Pi, new FCNameColorAPI(config));
        }

        private void OnCommand(string command, string args)
        {
            UI.Visible = true;
        }

        private void DrawConfigUI()
        {
            UI.Visible = true;
        }

        private void OnLogin()
        {
            // LocalPlayer is still null at this point, so we just set a flag that indicates we're logging in.
            loggingIn = true;
            initialized = false;
            FC = null;
            TrackedFCs = new List<FC>();
        }

        private void OnFrameworkUpdate(IFramework framework)
        {
            if (ClientState.LocalPlayer == null)
            {
                return;
            }

            if (!loggingIn && initialized)
            {
                return;
            }

            var lp = ClientState.LocalPlayer;
            playerName = lp.Name.TextValue;
            worldName = lp.HomeWorld.GameData.Name;
            PlayerKey = $"{playerName}@{worldName}";

            loggingIn = false;
            PluginLog.Debug($"Logged in as {PlayerKey}.");
            _ = FetchData();
        }

        private void DrawUI()
        {
            UI.Draw();
        }

        public void Reload()
        {
            _ = FetchData();
            SearchingFC = false;
            SearchingFCError = null;
        }

        private void HandleError()
        {
            PluginLog.Debug("Running HandleError");
            Error = true;
            Cooldown = CooldownTime * 6;
            timer.Start();

            void OnFinish(object sender, ElapsedEventArgs e)
            {
                if (Cooldown > 0) return;

                PluginLog.Debug("HandleError: Retrying FetchData");
                _ = FetchData();
                timer.Elapsed -= OnFinish;
            }

            timer.Elapsed += OnFinish;
        }

        public async Task<FCConfig> SearchFC(string id, string group)
        {
            SearchingFC = true;
            lodestoneClient ??= await LodestoneClient.GetClientAsync();
            FCConfig result = null;
            try
            {
                var fc = await lodestoneClient.GetFreeCompany(id);
                PluginLog.Debug($"Fetched FC {id}: {fc?.Name ?? "(Not found)"}");
                if (fc?.Name == null)
                {
                    SearchingFCError = "FC could not be found, please make sure it exists.";
                    SearchingFC = false;
                    return null;
                }

                result = new FCConfig
                {
                    Group = group,
                    ID = fc.Id,
                };

                var existingConfig = config.FCConfigs.ContainsKey(PlayerKey);
                if (!existingConfig)
                {
                    config.FCConfigs.Add(PlayerKey, new List<FCConfig>() { result });
                } else
                {
                    config.FCConfigs[PlayerKey].Add(result);
                }

                config.Save();
                SearchingFC = false;

                // We don’t immediately need the list of members, we can fetch this in the background.
                // All we need to know for this method to work is whether the FC exists or not.
                _ = UpdateFCMembers(id);
            }
            catch
            {
                SearchingFC = false;
                SearchingFCError = "Something wrong when fetching the FC. Is Lodestone down?";
            }

            return result;
        }

        private async Task UpdateFCMembers(string id)
        {
            var configs = config.FCConfigs[PlayerKey];
            var index = configs.FindIndex(f => f.ID == id);
            if (index < 0)
            {
                return;
            }

            try
            {
                var fcExists = config.FCs.TryGetValue(configs[index].ID, out var fc);
                if (!fcExists)
                {
                    var fetchedFC = await lodestoneClient.GetFreeCompany(id);
                    fc = new FC
                    {
                        ID = fetchedFC.Id,
                        Name = fetchedFC.Name,
                        LastUpdated = DateTime.Now,
                        World = fetchedFC.World,
                    };
                }
                var m = await FetchFCMembers(id);
                fc.Members = m.ToArray();
                config.FCs[fc.ID] = fc;
                config.Save();
                PluginLog.Debug("Finished fetching FC members for {fc}. Fetched {members} members.", fc.Name, m.Count);
            }
            catch
            {
                PluginLog.Error("Something went wrong when trying to fetch and update the FC members for FC ID {id}.", id);
            }

            skipCache.Clear();
        }

        private async Task<List<FCMember>> FetchFCMembers(string id)
        {
            // Fetch the first page of FC members.
            // This will also contain the amount of additional pages of members that may have to be retrieved.
            PluginLog.Debug($"Fetching FC {id} members page 1");
            var fcMemberResult = await lodestoneClient.GetFreeCompanyMembers(id);
            if (fcMemberResult == null)
            {
                return new List<FCMember>(Array.Empty<FCMember>());
            }

            var newMembers = new List<FCMember>();
            newMembers.AddRange(fcMemberResult.Members.Select(res => new FCMember { ID = res.Id, Name = res.Name }));

            // Fire off async requests for fetching members for each remaining page
            if (fcMemberResult.NumPages <= 1) return newMembers;

            var taskList = new List<Task<FreeCompanyMembers>>();
            foreach (var index in Enumerable.Range(2, fcMemberResult.NumPages - 1))
            {
                PluginLog.Debug($"Fetching FC {id} members page {index}");
                taskList.Add(lodestoneClient.GetFreeCompanyMembers(id, index));
            }

            await Task.WhenAll(taskList);
            taskList.ForEach(task =>
                newMembers.AddRange(
                    task.Result.Members.Select(res => new FCMember { ID = res.Id, Name = res.Name })));

            return newMembers;
        }

        private async Task FetchData()
        {
            if (string.IsNullOrEmpty(playerName))
            {
                return;
            }

            initialized = true;
            Loading = true;
            Error = false;
            Cooldown = CooldownTime;
            timer.Start();
            PluginLog.Debug("Fetching data");

            if (FirstTime)
            {
                Chat.Print(
                    "[FCNameColor]: First-time setup - Fetching FC members from Lodestone. Plugin will work once this is done.");
            }

            lodestoneClient ??= await LodestoneClient.GetClientAsync();

            PluginLog.Debug($"Fetching data for {PlayerKey}");
            if (!config.FCConfigs.ContainsKey(PlayerKey))
            {
                config.FCConfigs.Add(PlayerKey, new List<FCConfig>());
            } else
            {
                var trackedFCs = new List<FC>();
                var fcConfigs = config.FCConfigs[PlayerKey];
                foreach (var fcConfig in fcConfigs)
                {
                    var foundTrackedFc = config.FCs.TryGetValue(fcConfig.ID, out var trackedFC);
                    if (foundTrackedFc)
                    {
                        trackedFCs.Add(trackedFC);
                    }
                }

                if (trackedFCs.Count > 0)
                {
                    PluginLog.Debug($"Loaded {trackedFCs.Count} cached FCs");
                }

                TrackedFCs = trackedFCs;
            }

            config.PlayerIDs.TryGetValue(PlayerKey, out var playerId);

            if (string.IsNullOrEmpty(playerId))
            {
                PluginLog.Debug("Fetching character ID");
                var playerSearch = await lodestoneClient.SearchCharacter(
                    new CharacterSearchQuery
                    {
                        World = worldName,
                        CharacterName = $"\"{playerName}\""
                    });
                playerId = playerSearch?.Results
                    .FirstOrDefault(entry => entry.Name == playerName)?.Id;
                if (string.IsNullOrEmpty(playerId))
                {
                    PluginLog.Error("Could not find player on Lodestone");
                    HandleError();
                    return;
                }

                config.PlayerIDs[PlayerKey] = playerId;
            }

            config.PlayerFCs.TryGetValue(playerId, out var cachedFCId);
            var cachedFCFetched = config.FCs.TryGetValue(cachedFCId, out var cachedFC);
            FC = cachedFC;
            if (cachedFCFetched)
            {
                PluginLog.Debug($"Loaded {cachedFC.Members.Length} cached FC members");
            }

            if (!config.FCConfigs[PlayerKey].Any(f => f.ID == cachedFC.ID))
            {
                PluginLog.Debug($"Added missing FC Config for own FC.");
                config.FCConfigs[PlayerKey].Add(new FCConfig() { ID = cachedFC.ID, Group = "Own FC" });
            }

            PluginLog.Debug("Fetching FC ID via character page");
            var player = await lodestoneClient.GetCharacter(playerId);
            if (player == null)
            {
                PluginLog.Debug(
                    "Player does not exist on Lodestone. If it’s a new character, try again in a couple of hours.");
                NotInFC = true;
                return;
            }

            if (player.FreeCompany == null)
            {
                PluginLog.Debug("Player is not in an FC.");
                NotInFC = true;
                return;
            }

            var fc = new FC
            {
                ID = player.FreeCompany.Id,
                Name = player.FreeCompany.Name,
                World = worldName,
                LastUpdated = DateTime.Now
            };

            try
            {
                var newMembers = await FetchFCMembers(fc.ID);
                fc.Members = newMembers.ToArray();
                config.PlayerFCs[playerId] = fc.ID;
                config.FCs[fc.ID] = fc;
                config.Save();
                Loading = false;
                PluginLog.Debug($"Finished fetching data. Fetched {fc.Members.Length} members.");
                FC = fc;

                if (FirstTime)
                {
                    Chat.Print($"[FCNameColor]: First-time setup finished. Fetched {fc.Members.Length} members.");
                    FirstTime = false;
                }

                var fcConfigs = config.FCConfigs[PlayerKey];

                async void ScheduleFCUpdates()
                {
                    PluginLog.Debug("Scheduling additional FC updates");
                    foreach (var fcConfig in fcConfigs.ToList())
                    {
                        var additionalFCFetched = config.FCs.TryGetValue(fcConfig.ID, out var additionalFC);
                        if (additionalFCFetched && (DateTime.Now - additionalFC.LastUpdated).TotalHours < 1)
                        {
                            PluginLog.Debug(
                                $"Skipping updating {additionalFC.Name}, it was updated less than 2 hours ago.");
                            continue;
                        }

                        PluginLog.Debug($"Waiting 30 seconds before updating FC {fcConfig.ID}");
                        await Task.Delay(30000);

                        PluginLog.Debug($"Updating FC {fcConfig.ID}");
                        await UpdateFCMembers(fcConfig.ID);
                    }
                }

                skipCache.Clear();
                new Task(ScheduleFCUpdates).Start();
            }
            catch (Exception)
            {
                HandleError();
            }
        }

        private SeString ModifySeString(SeString content, string uiColor)
        {
            content.Payloads.Insert(0, new UIForegroundPayload(Convert.ToUInt16(uiColor)));
            content.Payloads.Insert(1, new UIGlowPayload(config.Glow ? Convert.ToUInt16(uiColor) : (ushort)0));
            content.Payloads.Add(UIGlowPayload.UIGlowOff);
            content.Payloads.Add(UIForegroundPayload.UIForegroundOff);
            return content;
        }

        private unsafe void* UpdateNameplatesDetour(RaptureAtkModule* raptureAtkModule, NamePlateInfo* namePlateInfo, NumberArrayData* numArray, StringArrayData* stringArray, GameObject* gameObject, int numArrayIndex, int stringArrayIndex)
        {
            var original = () => updateNameplateHook.Original(raptureAtkModule, namePlateInfo, numArray, stringArray, gameObject, numArrayIndex, stringArrayIndex);
            if (!config.Enabled || !FC.HasValue || FC?.Members == null || FC?.Members.Length == 0 || ClientState.IsPvPExcludingDen)
            {
                return original();
            }

            if (gameObject->ObjectKind != 1) { return original(); }

            var battleChara = (BattleChara*)gameObject;
            var objectID = battleChara->Character.GameObject.ObjectID;

            if (skipCache.Contains(objectID))
            {
                return original();
            }

            var isLocalPlayer = ClientState?.LocalPlayer?.ObjectId == objectID;
            var isInDuty = Condition[ConditionFlag.BoundByDuty56];

            if (isInDuty && isLocalPlayer)
            {
                return original();
            }

            if (!isInDuty && config.OnlyDuties)
            {
                return original();
            }

            if (!isInDuty && isLocalPlayer && !config.IncludeSelf)
            {
                return original();
            }

            // Skip any player who is dead, colouring the name of dead characters makes them harder to recognize.
            if (battleChara->Character.CharacterData.Health == 0)
            {
                return original();
            }

            var owner = battleChara->GetStatusManager->Owner;
            var isInParty = owner->IsPartyMember;
            var isInAlliance = owner->IsAllianceMember;
            var isFriend = owner->IsFriend;

            if (config.IgnoreFriends && isFriend)
            {
                return original();
            }

            var name = MemoryHelper.ReadStringNullTerminated((IntPtr)gameObject->Name);
            if (config.IgnoredPlayers.ContainsKey(name))
            {
                return original();
            }

            var world = WorldNames[battleChara->Character.HomeWorld];
            var color = config.Color;
            var uiColor = config.UiColor;
            var fc = FC.Value;

            if (!fc.Members.Any(member => member.Name == name))
            {
                var additionalFCIndex = TrackedFCs.FindIndex(f => f.World == world && f.Members.Any(m => m.Name == name));
                if (additionalFCIndex < 0)
                {
                    // This player isn’t an FC member or in one of the tracked FCs.
                    // We can skip it in future calls.
                    PluginLog.Debug("Adding {name} ({id}) to skip cache", name, objectID);
                    skipCache.Add(objectID);
                    return original();
                }

                var id = TrackedFCs[additionalFCIndex].ID;
                var configs = config.FCConfigs[PlayerKey];
                var fcConfig = configs.FindIndex(c => c.ID == id);
                var group = fcConfig > 0 && config.Groups.ContainsKey(configs[fcConfig].Group)
                    ? config.Groups[configs[fcConfig].Group]
                    : config.Groups["Other FC"];
                color = group.Color;
                uiColor = group.UiColor;
            }

            // Everyone loves the triple ternary check...
            var nameType = isInParty
              ? ConfigOption.NamePlateNameTypeParty
              : isInAlliance
                ? ConfigOption.NamePlateNameTypeAlliance
                : isFriend
                  ? ConfigOption.NamePlateNameTypeFriend
                  : ConfigOption.NamePlateNameTypeOther;

            if (isInDuty && config.IncludeDuties)
            {
                var nameString = new SeString(new TextPayload(BuildPlayername(name, nameType)));
                namePlateInfo->Name.SetSeString(ModifySeString(nameString, uiColor));

                var title = namePlateInfo->Title.ToString();
                if (title.Length > 0)
                {
                    var titleString = new SeString(new TextPayload($"《{title}》"));
                    namePlateInfo->DisplayTitle.SetSeString(ModifySeString(titleString, uiColor));
                }
            }

            var shouldReplaceName = !config.OnlyColorFCTag && !isLocalPlayer;
            if (!isInDuty && !shouldReplaceName)
            {
                var tag = MemoryHelper.ReadStringNullTerminated((IntPtr)battleChara->Character.FreeCompanyTag);
                if (tag.Length > 0)
                {
                    var newFCString = ModifySeString(new SeString(new TextPayload($" «{tag}»")), uiColor);
                    namePlateInfo->FcName.SetSeString(ModifySeString(newFCString, uiColor));
                }
            }

            if (shouldReplaceName)
            {
                var nameString = new SeString(new TextPayload(BuildPlayername(name, nameType)));
                namePlateInfo->Name.SetSeString(ModifySeString(nameString, uiColor));

                var title = namePlateInfo->Title.ToString();
                if (title.Length > 0)
                {
                    var titleString = new SeString(new TextPayload($"《{title}》"));
                    namePlateInfo->DisplayTitle.SetSeString(ModifySeString(titleString, uiColor));
                }

                var tag = MemoryHelper.ReadStringNullTerminated((IntPtr)battleChara->Character.FreeCompanyTag);
                if (tag.Length > 0)
                {
                    var newFCString = ModifySeString(new SeString(new TextPayload($" «{tag}»")), uiColor);
                    namePlateInfo->FcName.SetSeString(ModifySeString(newFCString, uiColor));
                }
            }

#if DEBUG
            PluginLog.Verbose("Overriding player nameplate for {name} (ObjectID {objectID})", name, objectID);
#endif

            return original();
        }

        protected virtual void Dispose(bool disposing)
        {
            try
            {
                if (!disposing) return;
                UI.Dispose();

                fcNameColorProvider.Dispose();

                Commands.RemoveHandler(CommandName);
                Framework.Update -= OnFrameworkUpdate;
                ClientState.Login -= OnLogin;

                updateNameplateHook.Disable();
                updateNameplateHook.Dispose();
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