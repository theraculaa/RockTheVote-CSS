namespace K4System;
using CounterStrikeSharp.API.Modules.Cvars;
public sealed partial class Plugin
{
    public bool DatabaseReady { get; private set; }
    public bool IsFfaMode => Config.GeneralSettings.FFAMode ||
        (ConVar.Find("game_type")?.GetPrimitiveValue<int>() == 1 && ConVar.Find("game_mode")?.GetPrimitiveValue<int>() == 2) ||
        ConVar.Find("mp_teammates_are_enemies")?.GetPrimitiveValue<bool>() == true;
}
