using System;
using FCNameColor.Config;

namespace FCNameColor.UI.Tabs;

public abstract class ConfigTab(Plugin plugin)
{
    protected readonly Plugin Plugin = plugin;
    protected readonly ConfigurationV1 Config = plugin.Config;

    public abstract void Draw(Func<bool> markDirty);
}