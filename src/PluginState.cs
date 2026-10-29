namespace cs2_rockthevote
{
    public class PluginState : IPluginDependency<Plugin, Config>
    {
        public bool Unloaded { get; set; }
        public bool MapChangeScheduled { get; set; }
        public bool EofVoteHappening { get; set; }
        public bool ExtendTimeVoteHappening { get; set; }
        public bool RtvVoteHappening { get; set; }
        public int MapExtensionCount { get; set; } = 0;

        // OyunServerim: match mode (css_rtv_matchmode). While on, RockTheVote never starts a vote and never changes the map.
        // Survives map changes on purpose (a match may change maps); a plugin reload or server restart turns it off.
        public bool MatchMode { get; set; }
        public DateTime? MatchModeSinceUtc { get; set; }

        public bool DisableCommands => MatchMode || MapChangeScheduled || EofVoteHappening || ExtendTimeVoteHappening || RtvVoteHappening;

        public void OnMapStart(string map)
        {
            MapChangeScheduled = false;
            EofVoteHappening = false;
            ExtendTimeVoteHappening = false;
            RtvVoteHappening = false;
            MapExtensionCount = 0;
        }
    }
}
