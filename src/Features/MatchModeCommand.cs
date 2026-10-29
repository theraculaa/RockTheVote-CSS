using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using Microsoft.Extensions.Logging;

namespace cs2_rockthevote
{
    // OyunServerim.NET: match mode for MatchZy (or any match plugin).
    //   css_rtv_matchmode 1  -> RockTheVote stays loaded but starts no vote and changes no map; running votes are dropped.
    //   css_rtv_matchmode 0  -> normal RockTheVote again.
    //   css_rtv_matchmode    -> prints one status line (readable over RCON):
    //     [RTV] matchmode=1 since=2026-10-29T16:24:00Z vote=0 scheduled=0 version=2.4.0-osnet.5
    // The command is cheap (no plugin unload, no file I/O) and idempotent; send "1" again at every map start of a match.
    public partial class Plugin
    {
        public void OnMatchModeCommand(CCSPlayerController? player, CommandInfo info)
        {
            if (player != null && !PermissionUtility.HasAny(player, new List<string> { "@css/root" }))
                return;

            if (info.ArgCount > 1)
            {
                string arg = info.GetArg(1).Trim();
                bool? on = arg switch { "1" or "on" or "true" => true, "0" or "off" or "false" => false, _ => null };
                if (on is null)
                {
                    info.ReplyToCommand("[RTV] usage: css_rtv_matchmode [0|1]");
                    return;
                }
                SetMatchMode(on.Value, player == null ? "console" : player.PlayerName);
            }

            info.ReplyToCommand(MatchModeStatus());
        }

        public string MatchModeStatus()
        {
            string since = _pluginState.MatchModeSinceUtc?.ToString("yyyy-MM-ddTHH:mm:ssZ") ?? "-";
            int vote = (_pluginState.EofVoteHappening || _pluginState.RtvVoteHappening || _pluginState.ExtendTimeVoteHappening) ? 1 : 0;
            int scheduled = _pluginState.MapChangeScheduled ? 1 : 0;
            return $"[RTV] matchmode={(_pluginState.MatchMode ? 1 : 0)} since={since} vote={vote} scheduled={scheduled} version={ModuleVersion}";
        }

        private void SetMatchMode(bool on, string by)
        {
            if (on == _pluginState.MatchMode)
                return;

            _pluginState.MatchMode = on;
            _pluginState.MatchModeSinceUtc = on ? DateTime.UtcNow : null;
            if (on)
            {
                bool vote = _endMapVoteManager.CancelForMatch();
                _rtvManager.CancelForMatch();
                bool scheduled = _changeMapManager.CancelForMatch();
                _pluginState.RtvVoteHappening = false;
                _logger.LogInformation("[RTV.MatchMode] ON by {By} (vote dropped: {Vote}, scheduled change dropped: {Scheduled})", by, vote, scheduled);
            }
            else
            {
                _logger.LogInformation("[RTV.MatchMode] OFF by {By}", by);
            }
        }
    }
}
