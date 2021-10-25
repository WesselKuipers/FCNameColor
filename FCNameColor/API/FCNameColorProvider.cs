using System;
using System.Collections.Generic;
using Dalamud.Logging;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;

namespace FCNameColor
{
    public class FCNameColorProvider
    {
        private const string LabelProviderApiVersion = "FCNameColor.APIVersion";
        private const string LabelProviderGetLocalPlayers = "FCNameColor.GetLocalPlayers";
        private const string LabelProviderGetPlayerFCs = "FCNameColor.GetPlayerFCs";
        private const string LabelProviderGetFCMembers = "FCNameColor.GetFCMembers";
        private const string LabelProviderGetIgnoredPlayers = "FCNameColor.GetIgnoredPlayers";
        private const string LabelProviderAddPlayerToIgnoredPlayers = "FCNameColor.AddPlayerToIgnoredPlayers";
        private const string LabelProviderRemovePlayerFromIgnoredPlayers = "FCNameColor.RemovePlayerFromIgnoredPlayers";

        private readonly ICallGateProvider<int> providerAPIVersion;
        private readonly ICallGateProvider<IEnumerable<string>> providerGetLocalPlayers;
        private readonly ICallGateProvider<IEnumerable<string>> providerGetPlayerFCs;
        private readonly ICallGateProvider<string, IEnumerable<string>> providerGetFCMembers;
        private readonly ICallGateProvider<IEnumerable<string>> providerGetIgnoredPlayers;
        private readonly ICallGateProvider<string, string, object> providerAddPlayerToIgnoredPlayers;
        private readonly ICallGateProvider<string, object> providerRemovePlayerFromIgnoredPlayers;

        public FCNameColorProvider(DalamudPluginInterface pluginInterface, IFCNameColorAPI api)
        {
            try
            {
                this.providerAPIVersion = pluginInterface.GetIpcProvider<int>(LabelProviderApiVersion);
                this.providerAPIVersion.RegisterFunc(() => api.APIVersion);
                
                this.providerGetLocalPlayers =
                    pluginInterface.GetIpcProvider<IEnumerable<string>>(LabelProviderGetLocalPlayers);
                this.providerGetLocalPlayers.RegisterFunc(api.GetLocalPlayers);
                
                this.providerGetPlayerFCs =
                    pluginInterface.GetIpcProvider<IEnumerable<string>>(LabelProviderGetPlayerFCs);
                this.providerGetPlayerFCs.RegisterFunc(api.GetPlayerFCs);
                
                this.providerGetFCMembers =
                    pluginInterface.GetIpcProvider<string, IEnumerable<string>>(LabelProviderGetFCMembers);
                this.providerGetFCMembers.RegisterFunc(api.GetFCMembers);
                
                this.providerGetIgnoredPlayers =
                    pluginInterface.GetIpcProvider<IEnumerable<string>>(LabelProviderGetIgnoredPlayers);
                this.providerGetIgnoredPlayers.RegisterFunc(api.GetIgnoredPlayers);
                
                this.providerAddPlayerToIgnoredPlayers =
                    pluginInterface.GetIpcProvider<string, string, object>(LabelProviderAddPlayerToIgnoredPlayers);
                this.providerAddPlayerToIgnoredPlayers.RegisterAction(api.AddPlayerToIgnoredPlayers);
                
                this.providerRemovePlayerFromIgnoredPlayers =
                    pluginInterface.GetIpcProvider<string, object>(LabelProviderRemovePlayerFromIgnoredPlayers);
                this.providerRemovePlayerFromIgnoredPlayers.RegisterAction(api.RemovePlayerFromIgnoredPlayers);
            }
            catch (Exception ex)
            {
                PluginLog.LogError($"Error registering IPC provider:\n{ex}");
            }
        }
        
        public void Dispose()
        {
            this.providerAPIVersion?.UnregisterFunc();
            this.providerGetLocalPlayers?.UnregisterFunc();
            this.providerGetPlayerFCs?.UnregisterFunc();
            this.providerGetFCMembers?.UnregisterFunc();
            this.providerGetIgnoredPlayers?.UnregisterFunc();
            this.providerAddPlayerToIgnoredPlayers?.UnregisterAction();
            this.providerRemovePlayerFromIgnoredPlayers?.UnregisterAction();
        }
    }
}
