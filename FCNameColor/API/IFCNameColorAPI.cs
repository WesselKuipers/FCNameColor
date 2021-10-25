using System.Collections.Generic;

namespace FCNameColor
{
    /// <summary>
    /// Interface to communicate with FCNameColor.
    /// </summary>
    public interface IFCNameColorAPI
    {
        /// <summary>
        /// Gets api version.
        /// </summary>
        public int APIVersion { get; }

        /// <summary>
        /// Get local players.
        /// </summary>
        /// <returns>A collection of strings in the form of (Name@Server PlayerID).</returns>
        public IEnumerable<string> GetLocalPlayers();
        
        /// <summary>
        /// Get Player FCs.
        /// </summary>
        /// <returns>A collection of strings in the form of (PlayerID FCID FCName).</returns>
        public IEnumerable<string> GetPlayerFCs();
        
        /// <summary>
        /// Get Player FCs.
        /// </summary>
        /// <param name="id">FC ID.</param>
        /// <returns>A collection of strings in the form of (PlayerID PlayerName).</returns>
        public IEnumerable<string> GetFCMembers(string id);
        
        /// <summary>
        /// Get ignored players list.
        /// </summary>
        /// <returns>A collection of strings in the form of (PlayerID PlayerName).</returns>
        public IEnumerable<string> GetIgnoredPlayers();

        /// <summary>
        /// Adds player to ignored list.
        /// </summary>
        /// <param name="id">player lodestone id.</param>
        /// <param name="name">player name.</param>
        public void AddPlayerToIgnoredPlayers(string id, string name);

        /// <summary>
        /// Removes player from ignored list.
        /// </summary>
        /// <param name="id">player lodestone id.</param>
        public void RemovePlayerFromIgnoredPlayers(string id);

        /// <summary>
        /// Update enabled state.
        /// </summary>
        /// <param name="state">new enabled state of nameplate updates.</param>
        public void SetEnabledState(bool state);
    }
}
