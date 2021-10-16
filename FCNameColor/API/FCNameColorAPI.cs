using System;
using System.Collections.Generic;
using System.Linq;

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
        
        public IEnumerable<string> GetPlayers()
        {
            this.CheckInitialized();
            return this.configuration.PlayerIDs.ToList().Select(player => $"{player.Key} {player.Value}");
        }

        public IEnumerable<string> GetPlayerFCs()
        {
            this.CheckInitialized();
            return this.configuration.PlayerFCs.ToList().Select(fc => $"{fc.Key} {fc.Value}");
        }

        public IEnumerable<string> GetFCMembers(string id)
        {
            this.CheckInitialized();
            var fcMembers = new List<string>();
            if (!this.configuration.PlayerFCs.ContainsKey(id)) return fcMembers;
            var fc = this.configuration.PlayerFCs[id];
            fcMembers.AddRange(fc.Members.Select(member => $"{member.ID} {member.Name}"));
            return fcMembers;
        }

        public IEnumerable<string> GetIgnoredPlayers()
        {
            this.CheckInitialized();
            return this.configuration.IgnoredPlayerNames.ToList();
        }

        public void AddPlayerToIgnoredPlayers(string name)
        {
            this.CheckInitialized();
            if (string.IsNullOrEmpty(name) || this.configuration.IgnoredPlayerNames.Contains(name)) return;
            this.configuration.IgnoredPlayerNames.Add(name);
            this.configuration.Save();
        }

        public void RemovePlayerFromIgnoredPlayers(string name)
        {
            this.CheckInitialized();
            if (string.IsNullOrEmpty(name) || !this.configuration.IgnoredPlayerNames.Contains(name)) return;
            this.configuration.IgnoredPlayerNames.Remove(name);
            this.configuration.Save();
        }

        public void SetEnabledState(bool state)
        {
            this.CheckInitialized();
            this.configuration.Enabled = state;
            this.configuration.Save();
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