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
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.IoC;
using Dalamud.Logging;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using NetStone;
using NetStone.Model.Parseables.FreeCompany.Members;
using NetStone.Search.Character;
using XivCommon;
using XivCommon.Functions.NamePlates;

namespace FCNameColor
{
    public class Plugin : IDalamudPlugin
    {
        public string Name => "FC Name Color";
        private const string CommandName = "/fcnc";

        [PluginService] public static DalamudPluginInterface Pi { get; private set; }

        private readonly Configuration config;

        [PluginService] public static ClientState ClientState { get; private set; }
        [PluginService] public static ChatGui Chat { get; private set; }
        [PluginService] public static Condition Condition { get; private set; }
        [PluginService] public static ObjectTable Objects { get; private set; }
        [PluginService] public static CommandManager Commands { get; private set; }
        [PluginService] public static Framework Framework { get; private set; }


        private readonly XivCommonBase xivCommonBase;
        private LodestoneClient lodestoneClient;
        private readonly FCNameColorProvider fcNameColorProvider;
        private PluginUI UI { get; }
        private bool loggingIn;
        private bool firstTime;
        private readonly Timer timer = new() {Interval = 1000};
        private List<FCMember> members;
        private uint worldId;
        private bool initialized;

        public bool Loading;
        public const int CooldownTime = 10;
        public int Cooldown;
        public bool NotInFC;
        public bool Error;
        public FC FC;
        public string PlayerName;
        public string WorldName;
        public bool SearchingFC;
        public bool SearchingFCError;

        public Plugin(DataManager dataManager, GameGui g)
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
                firstTime = true;
                config = new Configuration();
            }

            config.Initialize(Pi);

            xivCommonBase = new XivCommonBase(Hooks.NamePlates);
            xivCommonBase.Functions.NamePlates.OnUpdate += NamePlates_OnUpdate;

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

        private void OnLogin(object sender, EventArgs e)
        {
            // LocalPlayer is still null at this point, so we just set a flag that indicates we're logging in.
            loggingIn = true;
            initialized = false;
            members = null;
        }

        private void OnFrameworkUpdate(Framework framework)
        {
            if (ClientState.LocalPlayer == null)
            {
                return;
            }

            var lp = ClientState.LocalPlayer;
            PlayerName = lp.Name.TextValue;
            WorldName = lp.HomeWorld.GameData.Name;
            worldId = lp.HomeWorld.Id;

            if (!loggingIn && initialized)
            {
                return;
            }

            loggingIn = false;
            PluginLog.Debug(
                $"Logged in as {PlayerName} @ {WorldName}.");
            _ = FetchData();
        }

        private void DrawUI()
        {
            UI.Draw();
        }

        public void Reload()
        {
            _ = FetchData();
        }

        private void HandleError()
        {
            PluginLog.Debug("Running HandleError");
            Error = true;
            Cooldown = CooldownTime * 2;
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

        public async Task<FCConfig> SearchFC(string id)
        {
            SearchingFC = true;
            lodestoneClient ??= await LodestoneClient.GetClientAsync();
            var fc = await lodestoneClient.GetFreeCompany(id);
            PluginLog.Debug($"Fetched FC {id}: {fc?.Name ?? "(Not found)"}");
            if (fc == null)
            {
                SearchingFCError = true;
                SearchingFC = false;
                return null;
            }

            var result = new FCConfig
            {
                UiColor = "52",
                Color = new Vector4(0.07450981f,
                    0.8f,
                    0.6392157f,
                    1f),
                FC = new FC
                {
                    ID = id, Name = fc.Name, Members = Array.Empty<FCMember>(),
                    LastUpdated = DateTime.Now
                }
            };
            var playerKey = $"{PlayerName}@{WorldName}";
            config.AdditionalFCs[playerKey].Add(result);
            config.Save();
            SearchingFC = false;

            // We don’t immediately need the list of members, we can fetch this in the background.
            // All we need to know for this method to work is whether the FC exists or not.
            _ = UpdateFCMembers(id);

            return result;
        }

        private async Task UpdateFCMembers(string id)
        {
            var playerKey = $"{PlayerName}@{WorldName}";
            var index = config.AdditionalFCs[playerKey].FindIndex(f => f.FC.ID == id);
            if (index < 0)
            {
                return;
            }

            var fc = config.AdditionalFCs[playerKey][index];
            var m = await FetchFCMembers(id);
            fc.FC.Members = m.ToArray();
            config.AdditionalFCs[playerKey][index] = fc;
            config.Save();
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
            newMembers.AddRange(fcMemberResult.Members.Select(res => new FCMember {ID = res.Id, Name = res.Name}));

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
                    task.Result.Members.Select(res => new FCMember {ID = res.Id, Name = res.Name})));

            return newMembers;
        }

        private async Task FetchData()
        {
            if (string.IsNullOrEmpty(PlayerName))
            {
                return;
            }

            initialized = true;
            Loading = true;
            Cooldown = CooldownTime;
            timer.Start();
            PluginLog.Debug("Fetching data");

            if (firstTime)
            {
                Chat.Print(
                    "[FCNameColor]: First-time setup - Fetching FC members from Lodestone. Plugin will work once this is done.");
            }

            lodestoneClient ??= await LodestoneClient.GetClientAsync();

            var playerCacheName = $"{PlayerName}@{WorldName}";
            PluginLog.Debug($"Fetching data for {playerCacheName}");
            config.PlayerIDs.TryGetValue(playerCacheName, out var playerId);

            if (string.IsNullOrEmpty(playerId))
            {
                PluginLog.Debug("Fetching character ID");
                var playerSearch = await lodestoneClient.SearchCharacter(
                    new CharacterSearchQuery
                    {
                        World = WorldName,
                        CharacterName = $"\"{PlayerName}\""
                    });
                playerId = playerSearch?.Results
                    .FirstOrDefault(entry => entry.Name == PlayerName)?.Id;
                if (string.IsNullOrEmpty(playerId))
                {
                    PluginLog.Error("Could not find player on Lodestone");
                    HandleError();
                    return;
                }

                config.PlayerIDs[playerCacheName] = playerId;
            }

            var fcFetched = config.PlayerFCs.TryGetValue(playerId, out var cachedFC);
            FC = cachedFC;
            if (fcFetched)
            {
                PluginLog.Debug($"Loading {cachedFC.Members.Length} cached FC members");
                members = cachedFC.Members.ToList();
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

            var fc = new FC {ID = player.FreeCompany.Id, Name = player.FreeCompany.Name, LastUpdated = DateTime.Now};

            try
            {
                var newMembers = await FetchFCMembers(fc.ID);
                fc.Members = newMembers.ToArray();
                members = newMembers;
                config.PlayerFCs[playerId] = fc;
                config.Save();
                Loading = false;
                PluginLog.Debug($"Finished fetching data. Fetched {members.Count} members.");
                FC = fc;

                if (firstTime)
                {
                    Chat.Print($"[FCNameColor]: First-time setup finished. Fetched {members.Count} members.");
                    firstTime = false;
                }

                var playerKey = $"{PlayerName}@{WorldName}";
                if (!config.AdditionalFCs.ContainsKey(playerKey))
                {
                    config.AdditionalFCs.Add(playerKey, new List<FCConfig>());
                    config.Save();
                }

                var additionalFCs = config.AdditionalFCs[playerKey];

                async void ScheduleFCUpdates()
                {
                    PluginLog.Debug("Scheduling additional FC updates");
                    foreach (var additionalFC in additionalFCs.ToArray())
                    {
                        if ((DateTime.Now - additionalFC.FC.LastUpdated).TotalHours < 1)
                        {
                            PluginLog.Debug(
                                $"Skipping updating {additionalFC.FC.Name}, it was updated less than 2 hours ago.");
                            continue;
                        }

                        PluginLog.Debug($"Waiting 30 seconds before updating {additionalFC.FC.Name}");
                        await Task.Delay(30000);

                        PluginLog.Debug($"Updating {additionalFC.FC.Name}");
                        await UpdateFCMembers(additionalFC.FC.ID);
                    }
                }

                new Task(ScheduleFCUpdates).Start();
            }
            catch (Exception)
            {
                HandleError();
            }
        }

        private SeString BuildSeString(string content, string uiColor)
        {
            return new SeString(new UIForegroundPayload(Convert.ToUInt16(uiColor)),
                new UIGlowPayload(config.Glow ? Convert.ToUInt16(uiColor) : (ushort) 0),
                new TextPayload(content), UIGlowPayload.UIGlowOff, UIForegroundPayload.UIForegroundOff);
        }

        private unsafe void NamePlates_OnUpdate(NamePlateUpdateEventArgs args)
        {
            if (!config.Enabled || members == null || ClientState.IsPvP)
            {
                return;
            }

            if (args.Type != PlateType.Player || args.ObjectId == 0)
            {
                return;
            }

            var objectID = args.ObjectId;
            var target = (PlayerCharacter) Objects.SearchById(objectID);

            var isLocalPlayer = ClientState?.LocalPlayer?.ObjectId == objectID;
            var isPartyMember = GroupManager.Instance()->IsObjectIDInAlliance(objectID);
            var isInDuty = Condition[ConditionFlag.BoundByDuty56];

            if (target is null)
            {
                return;
            }

            if (isInDuty && isLocalPlayer)
            {
                return;
            }

            if (!isInDuty && isLocalPlayer && !config.IncludeSelf)
            {
                return;
            }

            // Skip any player who is dead, colouring the name of dead characters makes them harder to recognize.
            if (target.CurrentHp == 0)
            {
                return;
            }

            if (target.HomeWorld.Id != worldId)
            {
                return;
            }

            var color = config.Color;
            var uiColor = config.UiColor;

            if (!members.Exists(member => member.Name == target.Name.TextValue))
            {
                var additionalFCs = config.AdditionalFCs[$"{PlayerName}@{WorldName}"];
                var additionalFCIndex =
                    additionalFCs.FindIndex(f => f.FC.Members.Any(m => m.Name == target.Name.TextValue));

                if (additionalFCIndex < 0)
                {
                    return;
                }

                color = additionalFCs[additionalFCIndex].Color;
                uiColor = additionalFCs[additionalFCIndex].UiColor;
            }

            if (
                config.IgnoredPlayers.ContainsKey(target.Name.TextValue))
            {
                return;
            }

            if (isInDuty && config.IncludeDuties)
            {
                PluginLog.Debug($"Overriding player nameplate for {args.Name.TextValue} (ObjectID {objectID})");
                args.Colour = new RgbaColour
                {
                    A = (byte) (color.W * 255), R = (byte) (color.X * 255),
                    G = (byte) (color.Y * 255), B = (byte) (color.Z * 255)
                };
                return;
            }

            var shouldReplaceName = !config.OnlyColorFCTag && !isPartyMember && !isLocalPlayer;
            PluginLog.Debug(
                $"Name: {args.Name.TextValue}, shouldReplaceName: {shouldReplaceName}, IsInDuty: {isInDuty}, OnlyColorFCTag: {config.OnlyColorFCTag}, isPartyMember: {isPartyMember}, isLocalPlayer: {isLocalPlayer}");

            if (!isInDuty && !shouldReplaceName)
            {
                var newFCString = BuildSeString(args.FreeCompany.TextValue, uiColor);
                args.FreeCompany = newFCString;
            }

            if (shouldReplaceName)
            {
                args.Colour = new RgbaColour
                {
                    A = (byte) (color.W * 255), R = (byte) (color.X * 255),
                    G = (byte) (color.Y * 255), B = (byte) (color.Z * 255)
                };
            }
            else
            {
                var newFCString = BuildSeString(args.FreeCompany.TextValue, uiColor);
                args.FreeCompany = newFCString;
            }

            PluginLog.Debug($"Overriding player nameplate for {args.Name.TextValue} (ObjectID {objectID})");
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

                xivCommonBase.Functions.NamePlates.OnUpdate -= NamePlates_OnUpdate;
                xivCommonBase.Dispose();
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