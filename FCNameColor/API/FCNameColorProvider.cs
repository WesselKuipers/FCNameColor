using System;
using System.Collections.Generic;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;

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

        private readonly ICallGateProvider<int>? providerAPIVersion;
        private readonly ICallGateProvider<IEnumerable<string>>? providerGetLocalPlayers;
        private readonly ICallGateProvider<IEnumerable<string>>? providerGetPlayerFCs;
        private readonly ICallGateProvider<string, IEnumerable<string>>? providerGetFCMembers;
        private readonly ICallGateProvider<IEnumerable<string>>? providerGetIgnoredPlayers;
        private readonly ICallGateProvider<string, string, object>? providerAddPlayerToIgnoredPlayers;
        private readonly ICallGateProvider<string, object>? providerRemovePlayerFromIgnoredPlayers;

        public FCNameColorProvider(IDalamudPluginInterface pluginInterface, IFCNameColorAPI api, IPluginLog pluginLog)
        {
            try
            {
                providerAPIVersion = pluginInterface.GetIpcProvider<int>(LabelProviderApiVersion);
                providerAPIVersion.RegisterFunc(() => api.APIVersion);
                
                providerGetLocalPlayers =
                    pluginInterface.GetIpcProvider<IEnumerable<string>>(LabelProviderGetLocalPlayers);
                providerGetLocalPlayers.RegisterFunc(api.GetLocalPlayers);
                
                providerGetPlayerFCs =
                    pluginInterface.GetIpcProvider<IEnumerable<string>>(LabelProviderGetPlayerFCs);
                providerGetPlayerFCs.RegisterFunc(api.GetPlayerFCs);
                
                providerGetFCMembers =
                    pluginInterface.GetIpcProvider<string, IEnumerable<string>>(LabelProviderGetFCMembers);
                providerGetFCMembers.RegisterFunc(api.GetFCMembers);
                
                providerGetIgnoredPlayers =
                    pluginInterface.GetIpcProvider<IEnumerable<string>>(LabelProviderGetIgnoredPlayers);
                providerGetIgnoredPlayers.RegisterFunc(api.GetIgnoredPlayers);
                
                providerAddPlayerToIgnoredPlayers =
                    pluginInterface.GetIpcProvider<string, string, object>(LabelProviderAddPlayerToIgnoredPlayers);
                providerAddPlayerToIgnoredPlayers.RegisterAction(api.AddPlayerToIgnoredPlayers);
                
                providerRemovePlayerFromIgnoredPlayers =
                    pluginInterface.GetIpcProvider<string, object>(LabelProviderRemovePlayerFromIgnoredPlayers);
                providerRemovePlayerFromIgnoredPlayers.RegisterAction(api.RemovePlayerFromIgnoredPlayers);
            }
            catch (Exception ex)
            {
                pluginLog.Error($"Error registering IPC provider:\n{ex}");
            }
        }
        
        public void Dispose()
        {
            providerAPIVersion?.UnregisterFunc();
            providerGetLocalPlayers?.UnregisterFunc();
            providerGetPlayerFCs?.UnregisterFunc();
            providerGetFCMembers?.UnregisterFunc();
            providerGetIgnoredPlayers?.UnregisterFunc();
            providerAddPlayerToIgnoredPlayers?.UnregisterAction();
            providerRemovePlayerFromIgnoredPlayers?.UnregisterAction();
        }
    }
}
