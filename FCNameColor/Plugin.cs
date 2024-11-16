using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using NetStone;
using NetStone.Model.Parseables.FreeCompany.Members;
using NetStone.Search.Character;
using Dalamud.Plugin.Services;
using Dalamud.Game.ClientState.Objects.Enums;
using FCNameColor.Config;
using System.Numerics;
using Dalamud.Interface.Windowing;
using FCNameColor.UI;
using NetStone.Model.Parseables.Character;
using Dalamud.Game.Gui.NamePlate;

namespace FCNameColor
{
    public class Plugin : IDalamudPlugin
    {
        public string Name => "FC Name Color";
        private const string CommandName = "/fcnc";
        private readonly ConfigurationV1 config;

        [PluginService] public static IDalamudPluginInterface Pi { get; private set; }
        [PluginService] public static ISigScanner SigScanner { get; private set; }
        [PluginService] public static IClientState ClientState { get; private set; }
        [PluginService] public static IChatGui Chat { get; private set; }
        [PluginService] public static ICondition Condition { get; private set; }
        [PluginService] public static IObjectTable Objects { get; private set; }
        [PluginService] public static ICommandManager Commands { get; private set; }
        [PluginService] public static IFramework Framework { get; private set; }
        [PluginService] public static IGameInteropProvider GameInteropProvider { get; private set; }
        [PluginService] public static IPluginLog PluginLog { get; private set; }
        [PluginService] public static INamePlateGui NamePlateGui { get; private set; }

        public readonly WindowSystem WindowSystem = new("FC Name Color");

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

        public bool FirstTime;
        public bool Loading;
        public const int CooldownTime = 10;
        public int Cooldown;
        public bool NotInFC;
        public bool NotFound;
        public bool Error;
        public FC? FC;
        public Group FCGroup;
        public List<FC> TrackedFCs = new();
        public string PlayerKey;
        public bool SearchingFC;
        public string SearchingFCError = "";
        public bool ConfigOpen => UI.IsOpen;

        public Plugin(IDataManager dataManager)
        {
            config = new ConfigurationMigrator().GetConfig(Pi, PluginLog, Chat);

            if (config.FirstTime)
            {
                FirstTime = true;
            }

            config.Initialize(Pi);

            if (!config.Groups.ContainsKey("Default"))
            {
                config.Groups.Add(ConfigurationV1.DefaultGroups[0].Key, ConfigurationV1.DefaultGroups[0].Value);
                config.Save();
                PluginLog.Info("Added missing group Default");
            }

            if (!config.Groups.ContainsKey("Other FC"))
            {
                config.Groups.Add(ConfigurationV1.DefaultGroups[1].Key, ConfigurationV1.DefaultGroups[1].Value);
                config.Save();
                PluginLog.Info("Added missing group Other FC");
            }

            foreach (var character in config.FCGroups)
            {
                foreach (var (fc, group) in character.Value)
                {
                    if (!config.Groups.ContainsKey(group))
                    {
                        config.FCGroups[character.Key][fc] = "Default";
                        PluginLog.Info("Set group for FC {fc} to Default because the configured group wasn't found.", fc);
                    }
                }
            }

            var addNewGroupWindow = new AddNewGroupWindow(config, this);
            var ignoreListWindow = new IgnoreListWindow(config, this);
            var addAdditionalFCWindow = new AddAdditionalFCWindow(config, this);
            var additionalFCsWindow = new AdditionalFCsWindow(config, this, PluginLog, addAdditionalFCWindow);

            UI = new PluginUI(config, dataManager, this, ClientState, PluginLog, addNewGroupWindow, ignoreListWindow, additionalFCsWindow);
            WindowSystem.AddWindow(UI);
            WindowSystem.AddWindow(addNewGroupWindow);
            WindowSystem.AddWindow(ignoreListWindow);
            WindowSystem.AddWindow(addAdditionalFCWindow);
            WindowSystem.AddWindow(additionalFCsWindow);


            Commands.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Opens the FCNameColor Config."
            });

            NamePlateGui.OnNamePlateUpdate += NamePlateGui_OnNamePlateUpdate;

            timer.Elapsed += delegate
            {
                Cooldown -= 1;
                if (Cooldown <= 0)
                {
                    timer.Stop();
                }
            };

            ClientState.Login += OnLogin;
            Framework.Update += OnFrameworkUpdate;
            Pi.UiBuilder.Draw += WindowSystem.Draw;
            Pi.UiBuilder.OpenConfigUi += ToggleConfigUI;

            fcNameColorProvider = new FCNameColorProvider(Pi, new FCNameColorAPI(config, PluginLog), PluginLog);
        }

        private void OnCommand(string command, string args)
        {
            UI.Toggle();
        }

        private void ToggleConfigUI()
        {
            UI.Toggle();
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
            worldName = lp.HomeWorld.Value.Name.ToString();
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

        private void HandleError(Exception e)
        {
            PluginLog.Debug("Running HandleError");
            PluginLog.Error(e, "Exception caught in HandleError");
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

        public async Task<bool> SearchFC(string id, string group)
        {
            SearchingFC = true;
            lodestoneClient ??= await LodestoneClient.GetClientAsync();
            try
            {
                var fc = await lodestoneClient.GetFreeCompany(id);
                PluginLog.Debug($"Fetched FC {id}: {fc?.Name ?? "(Not found)"}");
                if (fc?.Name == null)
                {
                    SearchingFCError = "FC could not be found, please make sure it exists.";
                    SearchingFC = false;
                    return false;
                }

                if (!config.FCGroups.ContainsKey(PlayerKey))
                {
                    config.FCGroups.Add(PlayerKey, new());
                }

                config.FCGroups[PlayerKey][id] = group;

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
                return false;
            }

            return true;
        }

        private async Task UpdateFCMembers(string id)
        {
            try
            {
                var fcExists = config.FCs.TryGetValue(id, out var fc);
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

                var trackedFCIndex = TrackedFCs.FindIndex(f => fc.ID == f.ID);
                if (trackedFCIndex >= 0)
                {
                    TrackedFCs[trackedFCIndex] = fc;
                }
                else
                {
                    TrackedFCs.Add(fc);
                }

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
            if (!config.FCGroups.ContainsKey(PlayerKey))
            {
                config.FCGroups.Add(PlayerKey, new());
            }

            {
                var trackedFCs = new List<FC>();
                foreach (var fcConfig in config.FCGroups[PlayerKey])
                {
                    var foundTrackedFc = config.FCs.TryGetValue(fcConfig.Key, out var trackedFC);
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
                    NotFound = true;
                    NotInFC = true;
                } else
                {
                    config.PlayerIDs[PlayerKey] = playerId;
                    config.Save();
                }
            }


            try
            {
                LodestoneCharacter player = null;
                if (!NotFound)
                {
                    var cachedFCEXists = config.PlayerFCIDs.TryGetValue(playerId, out var cachedFCId);
                    if (cachedFCEXists)
                    {
                        var cachedFCFetched = config.FCs.TryGetValue(cachedFCId, out var cachedFC);
                        FC = cachedFC;
                        if (cachedFCFetched)
                        {
                            PluginLog.Debug($"Loaded {cachedFC.Members.Length} cached FC members");
                        }
                    }

                    PluginLog.Debug("Fetching FC ID via character page");
                    player = await lodestoneClient.GetCharacter(playerId);
                    if (player.FreeCompany == null)
                    {
                        PluginLog.Debug("Player is not in an FC.");
                        NotInFC = true;
                    }
                }

                if (player != null && !NotInFC)
                {
                    var fc = new FC
                    {
                        ID = player.FreeCompany.Id,
                        Name = player.FreeCompany.Name,
                        World = worldName,
                        LastUpdated = DateTime.Now
                    };

                    var newMembers = await FetchFCMembers(fc.ID);
                    fc.Members = newMembers.ToArray();
                    config.PlayerFCIDs[playerId] = fc.ID;
                    config.FCs[fc.ID] = fc;
                    PluginLog.Debug("Finished fetching data. Fetched {length} members.", fc.Members.Length);
                    FC = fc;

                    if (!config.FCGroups[PlayerKey].ContainsKey(fc.ID))
                    {
                        PluginLog.Debug("Added missing FC Config for own FC.");
                        config.FCGroups[PlayerKey][fc.ID] = "Default";
                    }
                }

                if (FirstTime)
                {
                    Chat.Print("[FCNameColor]: First-time setup finished.");
                    FirstTime = false;
                }

                config.Save();
                Loading = false;

                var fcGroups = config.FCGroups[PlayerKey];

                async void ScheduleFCUpdates()
                {
                    PluginLog.Debug("Scheduling additional FC updates");
                    foreach (var fcGroup in fcGroups.Where(f => FC.HasValue ? FC.Value.ID != f.Key : true))
                    {
                        var additionalFCFetched = config.FCs.TryGetValue(fcGroup.Key, out var additionalFC);
                        if (additionalFCFetched && (DateTime.Now - additionalFC.LastUpdated).TotalHours < 1)
                        {
                            PluginLog.Debug(
                                $"Skipping updating {additionalFC.Name}, it was updated less than 2 hours ago.");
                            continue;
                        }

                        PluginLog.Debug($"Waiting 30 seconds before updating FC {fcGroup.Key}");
                        await Task.Delay(30000);

                        PluginLog.Debug($"Updating FC {fcGroup.Key}");
                        await UpdateFCMembers(fcGroup.Key);
                    }
                    PluginLog.Debug("Finished loading all FC data.");
                }

                skipCache.Clear();
                new Task(ScheduleFCUpdates).Start();
            }
            catch (Exception e)
            {
                HandleError(e);
            }
        }

        private (Dalamud.Game.Text.SeStringHandling.SeString, Dalamud.Game.Text.SeStringHandling.SeString) CreateTextWrap(Vector4 color)
        {
            var left = new Lumina.Text.SeStringBuilder();
            var right = new Lumina.Text.SeStringBuilder();

            left.PushColorRgba(color);
            right.PopColor();

            if (config.Glow)
            {
                left.PushEdgeColorRgba(color);
                right.PopEdgeColor();
            }

            return ((Dalamud.Game.Text.SeStringHandling.SeString)left.ToSeString(), (Dalamud.Game.Text.SeStringHandling.SeString)right.ToSeString());
        }

        private void NamePlateGui_OnNamePlateUpdate(INamePlateUpdateContext context, IReadOnlyList<INamePlateUpdateHandler> handlers)
        {
            if (!config.Enabled || (!NotInFC || !NotFound) && (!FC.HasValue || FC?.Members == null || FC?.Members.Length == 0) || ClientState.IsPvPExcludingDen)
            {
                return;
            }

            foreach (var handler in handlers)
            {
                if (handler.NamePlateKind != NamePlateKind.PlayerCharacter) { continue; };

                try
                {
                    var playerCharacter = handler.PlayerCharacter;
                    if (playerCharacter == null) { continue; }

                    var entityId = playerCharacter.EntityId;
                    var name = playerCharacter.Name.TextValue;

                    if (skipCache.Contains(entityId)) { continue; }
                    if (config.IgnoredPlayers.ContainsKey(name)) { continue; }

                    var isLocalPlayer = ClientState?.LocalPlayer?.EntityId == entityId;
                    var isInDuty = Condition[ConditionFlag.BoundByDuty56];

                    if (isInDuty && isLocalPlayer) { continue; }
                    if (!isInDuty && config.OnlyDuties) { continue; }
                    if (!isInDuty && isLocalPlayer && !config.IncludeSelf) { continue; }
                    // Skip any player who is dead, colouring the name of dead characters makes them harder to recognize.
                    if (playerCharacter.CurrentHp == 0) { continue; }


                    var isInParty = playerCharacter.StatusFlags.HasFlag(StatusFlags.PartyMember);
                    var isInAlliance = playerCharacter.StatusFlags.HasFlag(StatusFlags.AllianceMember);
                    var isFriend = playerCharacter.StatusFlags.HasFlag(StatusFlags.Friend);

                    if (config.IgnoreFriends && isFriend) { continue; }

                    var world = playerCharacter.HomeWorld.Value.Name.ToString();
                    var group = NotInFC ? config.Groups.First().Value : config.Groups.GetValueOrDefault(config.FCGroups[PlayerKey][FC.Value.ID], ConfigurationV1.DefaultGroups[0].Value);
                    var color = group.Color;

                    if (NotFound || NotInFC || (FC.HasValue && !FC.Value.Members.Any(member => member.Name == name)))
                    {
                        var additionalFCIndex = TrackedFCs.FindIndex(f => f.World == world && f.Members.Any(m => m.Name == name));
                        if (additionalFCIndex < 0)
                        {
                            // This player isn’t an FC member or in one of the tracked FCs.
                            // We can skip it in future calls.
                            PluginLog.Debug("Adding {name} ({id}) to skip cache", name, entityId);
                            skipCache.Add(entityId);
                            continue;
                        }

                        var id = TrackedFCs[additionalFCIndex].ID;
                        var groupName = config.FCGroups[PlayerKey].ContainsKey(id) ? config.FCGroups[PlayerKey][id] : "Default";
                        if (!config.Groups.TryGetValue(groupName, out Group value))
                        {
                            value = ConfigurationV1.DefaultGroups[1].Value;
                            config.Groups.Add(groupName, value);
                        }

                        var trackedGroup = value;
                        color = trackedGroup.Color;
                    }

                    var shouldReplaceName = !config.OnlyColorFCTag && !isLocalPlayer;
                    var wrapper = CreateTextWrap(color);
                    if (!isInDuty && !shouldReplaceName)
                    {
                        handler.FreeCompanyTagParts.OuterWrap = wrapper;
                    }

                    if ((isInDuty && config.IncludeDuties) || shouldReplaceName)
                    {
                        handler.NameParts.TextWrap = wrapper;

                        if (handler.DisplayTitle && handler.Title.TextValue.Length > 0)
                        {
                            handler.TitleParts.OuterWrap = wrapper;
                        }

                        if (!isInDuty)
                        {
                            handler.FreeCompanyTagParts.OuterWrap = wrapper;
                        }
                    }

#if DEBUG
                    PluginLog.Verbose("Overriding player nameplate for {name} (ObjectID {objectID})", name, entityId);
#endif
                }
                catch (Exception e)
                {
                    PluginLog.Error("Something went wrong when trying to run the nameplate logic.");
                    PluginLog.Error("Error message: {e}", e.Message);
                    continue;
                }
            }


        }

        protected virtual void Dispose(bool disposing)
        {
            try
            {
                if (!disposing) return;
                WindowSystem.RemoveAllWindows();

                fcNameColorProvider.Dispose();

                Commands.RemoveHandler(CommandName);
                Framework.Update -= OnFrameworkUpdate;
                ClientState.Login -= OnLogin;
                NamePlateGui.OnNamePlateUpdate -= NamePlateGui_OnNamePlateUpdate;
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