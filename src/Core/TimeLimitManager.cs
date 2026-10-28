using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using Microsoft.Extensions.Logging;

namespace cs2_rockthevote.Core
{
    /// <summary>
    /// Shared map clock used by !timeleft, the end-of-map vote, vote extend / !extend and the map change.
    ///
    /// Normal modes: time played = Server.CurrentTime - GameStartTime, limit = mp_timelimit (unchanged behaviour).
    ///
    /// Valve Deathmatch (game_type 1 / game_mode 2): the match is one long round and GameStartTime stays invalid,
    /// so the old code always reported 0 seconds played and the full mp_timelimit as remaining. Only in that case
    /// the clock follows the round the HUD shows: remaining = RoundTime - (Server.CurrentTime - RoundStartTime).
    /// Both values live on the game rules entity, so reloading the plugin does not restart the clock.
    /// </summary>
    public class TimeLimitManager(GameRules gameRules, ILogger<TimeLimitManager> logger) : IPluginDependency<Plugin, Config>
    {
        private readonly GameRules _gameRules = gameRules;
        private readonly ILogger<TimeLimitManager> _logger = logger;
        private ConVar? _timeLimit;
        private ConVar? _gameType;
        private ConVar? _gameMode;

        private decimal TimeLimitValue => (decimal)(_timeLimit?.GetPrimitiveValue<float>() ?? 0F) * 60M;

        /// <summary>Valve Deathmatch (1/2).</summary>
        public bool IsDeathmatchMode =>
            (_gameType?.GetPrimitiveValue<int>() ?? -1) == 1 && (_gameMode?.GetPrimitiveValue<int>() ?? -1) == 2;

        private static bool ValidStart(float start) => start > 0 && start <= Server.CurrentTime;

        /// <summary>
        /// False while the Deathmatch round clock cannot be read yet. Callers that act on "time is over"
        /// (vote trigger, map change) can use it to wait instead of acting on a placeholder value.
        /// </summary>
        public bool ClockReady => !UsesRoundClock || TryRoundClock(out decimal roundSeconds, out _) && roundSeconds > 0;

        /// <summary>True when the Deathmatch round clock is used instead of GameStartTime.</summary>
        public bool UsesRoundClock => IsDeathmatchMode && !ValidStart(_gameRules.GameStartTime);

        private bool TryRoundClock(out decimal roundSeconds, out decimal played)
        {
            roundSeconds = 0;
            played = 0;
            // Cached game rules (no entity search): this is read every second by the vote timers.
            float roundStart = _gameRules.RoundStartTime;
            int roundTime = _gameRules.RoundTime;
            if (!ValidStart(roundStart))
                return false;
            roundSeconds = roundTime;
            played = (decimal)(Server.CurrentTime - roundStart);
            return true;
        }

        public bool UnlimitedTime
        {
            get
            {
                if (UsesRoundClock && TryRoundClock(out decimal roundSeconds, out _))
                    return roundSeconds <= 0;
                return TimeLimitValue <= 0;
            }
        }

        public decimal TimePlayed
        {
            get
            {
                if (_gameRules.WarmupRunning)
                    return 0;

                if (UsesRoundClock)
                    return TryRoundClock(out _, out decimal played) ? Math.Max(0, played) : 0;

                float gameStartTime = _gameRules.GameStartTime;
                if (gameStartTime <= 0)
                    return 0;

                return (decimal)(Server.CurrentTime - gameStartTime);
            }
        }

        public decimal TimeRemaining
        {
            get
            {
                if (UsesRoundClock)
                {
                    // Not readable yet (map just started, game rules not resolved, round not begun) or warmup:
                    // report the full length instead of 0, otherwise the end-of-map vote and the map change
                    // would think the time is already over and fire right after map start.
                    if (!TryRoundClock(out decimal roundSeconds, out decimal played) || roundSeconds <= 0)
                        return TimeLimitValue;
                    if (_gameRules.WarmupRunning)
                        return roundSeconds;
                    return Math.Max(0, roundSeconds - played);
                }

                if (UnlimitedTime || TimePlayed > TimeLimitValue)
                    return 0;

                return TimeLimitValue - TimePlayed;
            }

            // Value is the new total map length in minutes (see ExtendRoundTimeManager). In Deathmatch the caller
            // also writes gameRules.RoundTime, so the round clock and mp_timelimit stay in step.
            set => _timeLimit!.SetValue((float)value);
        }

        void LoadCvar()
        {
            _timeLimit = ConVar.Find("mp_timelimit");
            _gameType = ConVar.Find("game_type");
            _gameMode = ConVar.Find("game_mode");
        }

        public void OnMapStart(string map)
        {
            LoadCvar();
        }

        public void OnLoad(Plugin plugin)
        {
            LoadCvar();
            plugin.AddCommand("css_rtv_timedebug", "Prints the RockTheVote map clock values (server console or @css/root).", OnTimeDebug);
        }

        private void OnTimeDebug(CCSPlayerController? player, CommandInfo command)
        {
            if (player != null && !AdminManager.PlayerHasPermissions(player, "@css/root"))
                return;

            bool hasRound = _gameRules.TryGetRoundTiming(out float roundTime, out float roundStart);
            string line =
                $"[RTV.Time] game_type={_gameType?.GetPrimitiveValue<int>()} game_mode={_gameMode?.GetPrimitiveValue<int>()} " +
                $"warmup={_gameRules.WarmupRunning} now={Server.CurrentTime:F1} gameStart={_gameRules.GameStartTime:F1} " +
                $"roundStart={roundStart:F1} roundTime={roundTime} roundValid={hasRound} mp_timelimit={_timeLimit?.GetPrimitiveValue<float>()} " +
                $"clock={(UsesRoundClock ? "round" : "game")} played={TimePlayed:F0}s remaining={TimeRemaining:F0}s unlimited={UnlimitedTime}";

            _logger.LogInformation("{Line}", line);
            command.ReplyToCommand(line);
        }
    }
}
