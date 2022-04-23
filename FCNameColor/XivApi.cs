using System;
using System.Numerics;

namespace FCNameColor
{
    /// <summary>
    /// Internal representation of FCs as used by this plugin.
    /// </summary>
    public struct FC
    {
        /// <summary>
        /// The Lodestone ID if the FC
        /// </summary>
        public string ID;
        
        /// <summary>
        /// The name of the FC
        /// </summary>
        public string Name;
        
        /// <summary>
        /// The list of FC members
        /// </summary>
        public FCMember[] Members;

        /// <summary>
        /// When the FC was last fetched.
        /// </summary>
        public DateTime LastUpdated;
    }
    
    /// <summary>
    /// A configuration specific to an FC.
    /// </summary>
    public class FCConfig
    {
        /// <summary>
        /// The FC this config applies to.
        /// </summary>
        public FC FC;

        /// <summary>
        /// The selected color to apply.
        /// </summary>
        public Vector4 Color;

        /// <summary>
        /// The UI color ID.
        /// </summary>
        public string UiColor;
    }

    public struct FCMember
    {
        public string ID;
        public string Name;
    }
}