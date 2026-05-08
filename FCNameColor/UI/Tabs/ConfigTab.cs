using System;
using FCNameColor.Config;

namespace FCNameColor.UI.Tabs;

public abstract class ConfigTab
{
    protected readonly Plugin Plugin;
    protected readonly ConfigurationV1 Config;

    protected ConfigTab(Plugin plugin)
    {
        Plugin = plugin;
        Config = plugin.Config;
    }
    
    public abstract void Draw(Func<bool> markDirty);
}