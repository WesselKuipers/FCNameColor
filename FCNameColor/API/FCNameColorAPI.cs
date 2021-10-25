using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Logging;

namespace FCNameColor
{
    public class FCNameColorAPI : IFCNameColorAPI
    {
        private readonly bool initialized;
        private readonly Configuration configuration;
        
        public FCNameColorAPI(Configuration configuration)
        {
            this.configuration = configuration;
            this.initialized = true;
        }

        public int APIVersion => 1;
        
        public IEnumerable<string> GetLocalPlayers()
        {
            this.CheckInitialized();
            return this.configuration.PlayerIDs.Distinct().ToList().Select(player => $"{player.Key} {player.Value}");
        }

        public IEnumerable<string> GetPlayerFCs()
        {
            this.CheckInitialized();
            return this.configuration.PlayerFCs.Distinct().ToList().Select(fc => $"{fc.Key} {fc.Value.ID} {fc.Value.Name}");
        }

        public IEnumerable<string> GetFCMembers(string id)
        {
            this.CheckInitialized();
            var fcMembers = new List<string>();
            var fc = this.configuration.PlayerFCs.FirstOrDefault(pair => pair.Value.ID.Equals(id));
            try
            {
                fcMembers.AddRange(fc.Value.Members.Select(member => $"{member.ID} {member.Name}"));
            }
            catch (Exception)
            {
                PluginLog.LogError("Free Company ID not found.");
            }

            return fcMembers.Distinct();
        }

        public IEnumerable<string> GetIgnoredPlayers()
        {
            this.CheckInitialized();
            var ignoredPlayers = new List<string>();
            ignoredPlayers.AddRange(this.configuration.IgnoredPlayers.Select(player => $"{player.Value} {player.Key}"));
            return ignoredPlayers;
        }

        public void AddPlayerToIgnoredPlayers(string id, string name)
        {
            this.CheckInitialized();
            if (this.configuration.IgnoredPlayers.ContainsKey(name)) return;
            this.configuration.IgnoredPlayers.Add(name, id);
            this.configuration.Save();
        }

        public void RemovePlayerFromIgnoredPlayers(string id)
        {
            this.CheckInitialized();
            try
            {
                var ignoredPlayerKey = this.configuration.IgnoredPlayers.FirstOrDefault(player => player.Key.Equals(id)).Key;
                this.configuration.IgnoredPlayers.Remove(ignoredPlayerKey);
                this.configuration.Save();
            }
            catch (Exception)
            {
                PluginLog.LogError("Ignored Player ID not found.");
            }
        }

        private void CheckInitialized()
        {
            if (!this.initialized)
            {
                throw new Exception("API is not initialized.");
            }
        }
    }
}