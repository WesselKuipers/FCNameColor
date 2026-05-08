using System;
using System.Collections.Generic;

namespace FCNameColor.Model;

/// <summary>
/// Internal representation of FCs as used by this plugin.
/// </summary>
public struct Linkshell
{
    /// <summary>
    /// The Lodestone ID if the Linkshell
    /// </summary>
    public string? ID;

    /// <summary>
    /// The name of the Linkshell
    /// </summary>
    public string? Name;

    // /// <summary>
    // /// The World of the Linkshell.
    // /// </summary>
    // public string? World;

    public bool isCrossworld;

    /// <summary>
    /// The list of Linkshell members
    /// </summary>
    public string[] Members;

    /// <summary>
    /// When the Linkshell was last fetched.
    /// </summary>
    public DateTime LastUpdated;
}