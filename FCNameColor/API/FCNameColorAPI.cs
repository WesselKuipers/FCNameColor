using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using FCNameColor.Config;

namespace FCNameColor.API
{
    public class FCNameColorAPI(ConfigurationV1 configuration, IPluginLog pluginLog) : IFCNameColorAPI
    {
        private readonly bool initialized = true;

        public int APIVersion => 1;
        
        public IEnumerable<string> GetLocalPlayers()
        {
            CheckInitialized();
            return configuration.PlayerIDs.Distinct().ToList().Select(player => $"{player.Key} {player.Value}");
        }

        public IEnumerable<string> GetPlayerFCs()
        {
            CheckInitialized();
            return configuration.PlayerFCIDs.Distinct().ToList().Select(fc => $"{fc.Key} {fc.Value} {configuration.FCs[fc.Value].Name}");
        }

        public IEnumerable<string> GetFCMembers(string id)
        {
            CheckInitialized();
            var fcMembers = new List<string>();
            var fc = configuration.FCs.FirstOrDefault(pair => pair.Value.Equals(id));
            try
            {
                fcMembers.AddRange(fc.Value.Members.Select(member => $"{member.ID} {member.Name}"));
            }
            catch (Exception)
            {
                pluginLog.Error("Free Company ID not found.");
            }

            return fcMembers.Distinct();
        }

        public IEnumerable<string> GetIgnoredPlayers()
        {
            CheckInitialized();
            var ignoredPlayers = new List<string>();
            ignoredPlayers.AddRange(configuration.IgnoredPlayers.Select(player => $"{player.Value} {player.Key}"));
            return ignoredPlayers;
        }

        public void AddPlayerToIgnoredPlayers(string id, string name)
        {
            CheckInitialized();
            if (!configuration.IgnoredPlayers.TryAdd(name, id)) return;
            configuration.Save();
        }

        public void RemovePlayerFromIgnoredPlayers(string id)
        {
            CheckInitialized();
            try
            {
                var ignoredPlayerKey = configuration.IgnoredPlayers.FirstOrDefault(player => player.Key.Equals(id)).Key;
                configuration.IgnoredPlayers.Remove(ignoredPlayerKey);
                configuration.Save();
            }
            catch (Exception)
            {
                pluginLog.Error("Ignored Player ID not found.");
            }
        }

        private void CheckInitialized()
        {
            if (!initialized)
            {
                throw new Exception("API is not initialized.");
            }
        }
    }
}