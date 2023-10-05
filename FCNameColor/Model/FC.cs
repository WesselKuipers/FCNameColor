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
        /// The World of the FC.
        /// </summary>
        public string World;
        
        /// <summary>
        /// The list of FC members
        /// </summary>
        public FCMember[] Members;

        /// <summary>
        /// When the FC was last fetched.
        /// </summary>
        public DateTime LastUpdated;
    }

    public struct Group
    {
        /// <summary>
        /// The selected color to apply.
        /// </summary>
        public Vector4 Color;

        /// <summary>
        /// The UI color ID.
        /// </summary>
        public string UiColor;

        public Group(string uiColor, Vector4 color)
        {
            UiColor = uiColor;
            Color = color;
        }
    }
    
    /// <summary>
    /// A configuration specific to an FC.
    /// </summary>
    public class FCConfig
    {
        /// <summary>
        /// The FC this config applies to.
        /// </summary>
        public string ID;

        /// <summary>
        /// The name of the group this FC is assigned to.
        /// </summary>
        public string Group;
    }

    /// <summary>
    /// A representation of an FC’s member.
    /// </summary>
    public struct FCMember
    {
        /// <summary>
        /// The Lodestone ID of the member.
        /// </summary>
        public string ID;
        
        /// <summary>
        /// The name of the member.
        /// </summary>
        public string Name;
    }
}