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
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Interface.Windowing;
using FCNameColor.UI;
using NetStone.Model.Parseables.Character;
using Dalamud.Game.Gui.NamePlate;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Utility;
using FCNameColor.API;

namespace FCNameColor
{
    public class Plugin : IDalamudPlugin
    {
        [PluginService] public static IDalamudPluginInterface Pi { get; private set; } = null!;
        [PluginService] public static IClientState ClientState { get; private set; } = null!;
        [PluginService] public static IChatGui Chat { get; private set; } = null!;
        [PluginService] public static ICondition Condition { get; private set; } = null!;
        [PluginService] public static ICommandManager Commands { get; private set; } = null!;
        [PluginService] public static IFramework Framework { get; private set; } = null!;
        [PluginService] public static IPluginLog PluginLog { get; private set; } = null!;
        [PluginService] public static INamePlateGui NamePlateGui { get; private set; } = null!;
        [PluginService] public static IObjectTable ObjectTable { get; private set; } = null!;
        [PluginService] public static ITargetManager TargetManager { get; private set; } = null!;
        
        public string Name => "FC Name Color";
        private const string CommandName = "/fcnc";
        public readonly ConfigurationV1 Config;

        private readonly WindowSystem windowSystem = new("FC Name Color");
        private LodestoneClient? lodestoneClient;
        private readonly FCNameColorProvider fcNameColorProvider;

        private ConfigUI UI { get; }
        private bool loggingIn;
        private readonly Timer timer = new() { Interval = 1000 };
        private bool initialized;
        private string? playerName;
        private string? worldName;
        private readonly HashSet<uint> skipCache = [];

        public bool FirstTime;
        public bool Loading;
        public const int CooldownTime = 10;
        public int Cooldown;

        /// <summary>
        /// Used to indicate whether the player is currently in an FC themselves.
        /// </summary>
        public bool NotInFC = true;

        /// <summary>
        /// Used to indicate whether the current player character currently exists on Lodestone.
        /// </summary>
        public bool NotFound;
        public bool Error;
        public FC? FC;
        public Group FCGroup;
        private List<FC> trackedFCs = [];
        public string? PlayerKey;
        public bool SearchingFC;
        public string? SearchingFCError = "";
        public bool ConfigOpen => UI.IsOpen;

        public Plugin(IDataManager dataManager)
        {
            Config = new ConfigurationMigrator().GetConfig(Pi, PluginLog, Chat);

            if (Config.FirstTime)
            {
                FirstTime = true;
            }

            Config.Initialize(Pi);

            if (!Config.Groups.ContainsKey("Default"))
            {
                Config.Groups.Add(ConfigurationV1.DefaultGroups[0].Key, ConfigurationV1.DefaultGroups[0].Value);
                Config.Save();
                PluginLog.Info("Added missing group Default");
            }

            if (!Config.Groups.ContainsKey("Other FC"))
            {
                Config.Groups.Add(ConfigurationV1.DefaultGroups[1].Key, ConfigurationV1.DefaultGroups[1].Value);
                Config.Save();
                PluginLog.Info("Added missing group Other FC");
            }

            foreach (var character in Config.FCGroups)
            {
                foreach (var (fc, group) in character.Value)
                {
                    if (Config.Groups.ContainsKey(group)) continue;
                    Config.FCGroups[character.Key][fc] = "Default";
                    PluginLog.Info("Set group for FC {fc} to Default because the configured group wasn't found.", fc);
                }
            }


            UI = new ConfigUI( this);
            windowSystem.AddWindow(UI);

            UI.IsOpen = true;

            Commands.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Opens the FCNameColor Config."
            });

            NamePlateGui.OnNamePlateUpdate += NamePlateGui_OnNamePlateUpdate;
            NamePlateGui.OnDataUpdate += NamePlateGuiOnOnDataUpdate;

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
            Pi.UiBuilder.Draw += windowSystem.Draw;
            Pi.UiBuilder.OpenConfigUi += ToggleConfigUI;
            Pi.UiBuilder.OpenMainUi += ToggleConfigUI;

            fcNameColorProvider = new FCNameColorProvider(Pi, new FCNameColorAPI(Config, PluginLog), PluginLog);
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
            trackedFCs = [];
        }

        private void OnFrameworkUpdate(IFramework framework)
        {
            if (ObjectTable[0] is not IPlayerCharacter)
            {
                return;
            }

            if (!loggingIn && initialized)
            {
                return;
            }

            var lp = (ObjectTable[0] as IPlayerCharacter);
            playerName = lp?.Name.TextValue;
            worldName = lp?.HomeWorld.Value.Name.ToString();
            PlayerKey = $"{playerName}@{worldName}";

            loggingIn = false;
            PluginLog.Debug($"Logged in as {PlayerKey}.");
            _ = FetchData();
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

            timer.Elapsed += OnFinish;
            return;

            void OnFinish(object? sender, ElapsedEventArgs elapsedEventArgs)
            {
                if (Cooldown > 0) return;

                PluginLog.Debug("HandleError: Retrying FetchData");
                _ = FetchData();
                timer.Elapsed -= OnFinish;
            }
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

                if (PlayerKey != null && !Config.FCGroups.ContainsKey(PlayerKey))
                {
                    Config.FCGroups.Add(PlayerKey, new());
                }

                if (PlayerKey != null) Config.FCGroups[PlayerKey][id] = group;

                Config.Save();
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
                var fcExists = Config.FCs.TryGetValue(id, out var fc);
                if (!fcExists)
                {
                    var fetchedFC = await lodestoneClient?.GetFreeCompany(id);
                    fc = new FC
                    {
                        ID = fetchedFC?.Id,
                        Name = fetchedFC?.Name,
                        LastUpdated = DateTime.Now,
                        World = fetchedFC?.World,
                    };
                }
                var m = await FetchFCMembers(id);
                fc.Members = m.ToArray();
                if (fc.ID != null)
                {
                    fc.LastUpdated = DateTime.Now;
                    Config.FCs[fc.ID] = fc;

                    var trackedFCIndex = trackedFCs.FindIndex(f => fc.ID == f.ID);
                    if (trackedFCIndex >= 0)
                    {
                        trackedFCs[trackedFCIndex] = fc;
                    }
                    else
                    {
                        trackedFCs.Add(fc);
                    }
                }
                
                Config.Save();
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
            var fcMemberResult = await lodestoneClient?.GetFreeCompanyMembers(id)!;
            if (fcMemberResult == null)
            {
                return [];
            }

            var newMembers = new List<FCMember>();
            newMembers.AddRange(fcMemberResult.Members.Select(res => new FCMember { ID = res.Id, Name = res.Name }));

            // Fire off async requests for fetching members for each remaining page
            if (fcMemberResult.NumPages <= 1) return newMembers;

            var taskList = new List<Task<FreeCompanyMembers>>();
            foreach (var index in Enumerable.Range(2, fcMemberResult.NumPages - 1))
            {
                PluginLog.Debug($"Fetching FC {id} members page {index}");
                taskList.Add(lodestoneClient.GetFreeCompanyMembers(id, index)!);
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
            if (PlayerKey != null && !Config.FCGroups.ContainsKey(PlayerKey))
            {
                Config.FCGroups.Add(PlayerKey, new Dictionary<string, string>());
            }

            {
                var updatedTrackedFCs = new List<FC>();
                if (PlayerKey != null)
                    foreach (var fcConfig in Config.FCGroups[PlayerKey])
                    {
                        var foundTrackedFc = Config.FCs.TryGetValue(fcConfig.Key, out var trackedFC);
                        if (foundTrackedFc)
                        {
                            updatedTrackedFCs.Add(trackedFC);
                        }
                    }

                if (updatedTrackedFCs.Count > 0)
                {
                    PluginLog.Debug($"Loaded {updatedTrackedFCs.Count} cached FCs");
                }

                trackedFCs = updatedTrackedFCs;
            }

            if (PlayerKey != null)
            {
                Config.PlayerIDs.TryGetValue(PlayerKey, out var playerId);
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
                    }
                    else
                    {
                        Config.PlayerIDs[PlayerKey] = playerId;
                        Config.Save();
                    }
                }


                try
                {
                    LodestoneCharacter? player = null;
                    if (!NotFound)
                    {
                        var cachedFCExists = Config.PlayerFCIDs.TryGetValue(playerId, out var cachedFCId);
                        if (cachedFCExists)
                        {
                            var cachedFCFetched = Config.FCs.TryGetValue(cachedFCId, out var cachedFC);
                            FC = cachedFC;
                            NotInFC = false;
                            if (cachedFCFetched)
                            {
                                PluginLog.Debug($"Loaded {cachedFC.Members.Length} cached FC members");
                            }
                        }

                        PluginLog.Debug("Fetching FC ID via character page");
                        player = await lodestoneClient.GetCharacter(playerId);
                        if (player?.FreeCompany == null)
                        {
                            PluginLog.Debug("Player is not in an FC.");
                            NotInFC = true;
                        }
                        else
                        {
                            NotInFC = false;
                        }
                    }

                    if (player != null && !NotInFC)
                    {
                        var fc = new FC
                        {
                            ID = player.FreeCompany?.Id,
                            Name = player.FreeCompany?.Name,
                            World = worldName,
                            LastUpdated = DateTime.Now
                        };

                        if (fc.ID != null)
                        {
                            var newMembers = await FetchFCMembers(fc.ID);
                            fc.Members = newMembers.ToArray();
                        }

                        if (playerId != null) Config.PlayerFCIDs[playerId] = fc.ID;
                        if (fc.ID != null)
                        {
                            Config.FCs[fc.ID] = fc;
                            if (fc.Members != null)
                                PluginLog.Debug("Finished fetching data. Fetched {length} members.", fc.Members.Length);
                            FC = fc;

                            if (!Config.FCGroups[PlayerKey].ContainsKey(fc.ID))
                            {
                                PluginLog.Debug("Added missing FC Config for own FC.");
                                Config.FCGroups[PlayerKey][fc.ID] = "Default";
                            }
                        }
                    }

                    if (FirstTime)
                    {
                        Chat.Print("[FCNameColor]: First-time setup finished.");
                        FirstTime = false;
                    }

                    Config.Save();
                    Loading = false;

                    var fcIDs = Config.FCGroups[PlayerKey].Where(f => !FC.HasValue || FC.Value.ID != f.Key).Select(fc => fc.Key).ToArray();
                    async void ScheduleFCUpdates()
                    {
                        try
                        {
                            PluginLog.Debug("Scheduling additional FC updates");

                            foreach (var fc in fcIDs)
                            {
                                var additionalFCFetched = Config.FCs.TryGetValue(fc, out var additionalFC);
                                if (additionalFCFetched && (DateTime.Now - additionalFC.LastUpdated).TotalHours < 11)
                                {
                                    PluginLog.Debug(
                                        $"Skipping updating {additionalFC.Name}, it was updated less than 12 hours ago.");
                                    continue;
                                }

                                PluginLog.Debug($"Waiting 30 seconds before updating FC {fc}");
                                await Task.Delay(30000);

                                PluginLog.Debug($"Updating FC {fc}");
                                await UpdateFCMembers(fc);
                                skipCache.Clear();
                            }

                            PluginLog.Debug("Finished loading all FC data.");
                        }
                        catch (Exception e)
                        {
                            PluginLog.Error(e, "Something went wrong when updating the FCs");
                        }
                    }

                    skipCache.Clear();
                    new Task(ScheduleFCUpdates).Start();
                }
                catch (Exception e)
                {
                    HandleError(e);
                }
            }
        }

        private (SeString, SeString) CreateTextWrap(Vector4 color)
        {
            using var left = new RentedSeStringBuilder();
            using var right = new RentedSeStringBuilder();
            
            left.Builder.PushColorRgba(color);
            right.Builder.PopColor();

            if (!Config.Glow) return (left.Builder.ToReadOnlySeString().ToDalamudString(), right.Builder.ToReadOnlySeString().ToDalamudString());
            
            left.Builder.PushEdgeColorRgba(color);
            right.Builder.PopEdgeColor();

            return (left.Builder.ToReadOnlySeString().ToDalamudString(), right.Builder.ToReadOnlySeString().ToDalamudString());
        }

        private void NamePlateGui_OnNamePlateUpdate(INamePlateUpdateContext context, IReadOnlyList<INamePlateUpdateHandler> handlers)
        {
            if (!Config.Enabled || ClientState.IsPvPExcludingDen)
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
                    if (Config.IgnoredPlayers.ContainsKey(name)) { continue; }

                    var isLocalPlayer = (ObjectTable[0] as IPlayerCharacter).EntityId == entityId;
                    var isInDuty = Condition[ConditionFlag.BoundByDuty56];

                    if (isInDuty && isLocalPlayer) { continue; }
                    if (!isInDuty && Config.OnlyDuties) { continue; }
                    if (!isInDuty && isLocalPlayer && !Config.IncludeSelf) { continue; }
                    // Skip any player who is dead, colouring the name of dead characters makes them harder to recognize.
                    if (playerCharacter.CurrentHp == 0) { continue; }
                    
                    var isFriend = playerCharacter.StatusFlags.HasFlag(StatusFlags.Friend);

                    if (Config.IgnoreFriends && isFriend) { continue; }

                    var world = playerCharacter.HomeWorld.Value.Name.ToString();
                    if (PlayerKey != null)
                    {
                        var group = NotInFC ? Config.Groups.First().Value : Config.Groups.GetValueOrDefault(Config.FCGroups[PlayerKey][FC?.ID ?? ""], ConfigurationV1.DefaultGroups[0].Value);
                        var color = group.Color;

                        if (NotFound || NotInFC || (FC.HasValue && FC.Value.Members.All(member => member.Name != name)))
                        {
                            var additionalFCIndex = trackedFCs.FindIndex(f => f.World == world && f.Members.Any(m => m.Name == name));
                            if (additionalFCIndex < 0)
                            {
                                // This player isn’t an FC member or in one of the tracked FCs.
                                // We can skip it in future calls.
                                PluginLog.Debug("Adding {name} ({id}) to skip cache", name, entityId);
                                skipCache.Add(entityId);
                                continue;
                            }

                            var id = trackedFCs[additionalFCIndex].ID;
                            var groupName = id != null && Config.FCGroups[PlayerKey].TryGetValue(id, out var value1) ? value1 : "Default";
                            if (!Config.Groups.TryGetValue(groupName, out var value))
                            {
                                value = ConfigurationV1.DefaultGroups[1].Value;
                                Config.Groups.Add(groupName, value);
                            }

                            var trackedGroup = value;
                            color = trackedGroup.Color;
                        }

                        var shouldReplaceName = !Config.OnlyColorFCTag && !isLocalPlayer;
                        var wrapper = CreateTextWrap(color);

                        if (!isInDuty && !shouldReplaceName)
                        {
                            handler.FreeCompanyTagParts.OuterWrap = wrapper;
                        }

                        if ((isInDuty && Config.IncludeDuties) || shouldReplaceName)
                        {
                            handler.NameParts.TextWrap = wrapper;

                            if (handler is { DisplayTitle: true, Title.TextValue.Length: > 0 })
                            {
                                handler.TitleParts.OuterWrap = wrapper;
                            }

                            if (!isInDuty)
                            {
                                handler.FreeCompanyTagParts.OuterWrap = wrapper;
                            }
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
                }
            }
        }
        
        private void NamePlateGuiOnOnDataUpdate(INamePlateUpdateContext context, IReadOnlyList<INamePlateUpdateHandler> handlers)
        {
            if (!Config.Enabled || ClientState.IsPvPExcludingDen || !Config.HideOtherNameplates)
            {
                return;
            }
            
            var isInDuty = Condition[ConditionFlag.BoundByDuty56];
            if (isInDuty && !Config.HideInDuties)
            {
                return; 
            }
            
            foreach (var handler in handlers)
            {
                if (handler is not { NamePlateKind: NamePlateKind.PlayerCharacter }) continue;
                var playerCharacter = handler.PlayerCharacter;
                if (playerCharacter == null) continue;
                
                var entityId = playerCharacter.EntityId;

                if ((ObjectTable[0] as IPlayerCharacter).EntityId == entityId) continue;
                if (!Config.HideOnTarget && TargetManager.Target != null && TargetManager.Target.EntityId == entityId) continue;
                if (!Config.HideOnSoftTarget && TargetManager.SoftTarget != null && TargetManager.SoftTarget.EntityId == entityId) continue;
                if (!Config.HideOnHover && TargetManager.MouseOverTarget != null && TargetManager.MouseOverTarget.EntityId == entityId) continue;
                if (!Config.HideFriends && playerCharacter.StatusFlags.HasFlag(StatusFlags.Friend)) continue;
                if (!Config.HidePartyMembers && playerCharacter.StatusFlags.HasFlag(StatusFlags.PartyMember)) continue;
                if (!Config.HideAllianceMembers && playerCharacter.StatusFlags.HasFlag(StatusFlags.AllianceMember)) continue;
                
                if (!skipCache.Contains(entityId)) continue;
#if DEBUG
                PluginLog.Verbose("Hiding {name}", playerCharacter.Name.TextValue);
#endif
                handler.VisibilityFlags = 0;
                handler.MarkerIconId = 0;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            try
            {
                if (!disposing) return;
                windowSystem.RemoveAllWindows();

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