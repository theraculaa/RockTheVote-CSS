using System.Diagnostics;
using System.Text;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using Microsoft.Extensions.Logging;

namespace cs2_rockthevote
{
    /// <summary>
    /// osnet: timing diagnostics. Every line starts with "[RTV.Perf]" so it can be grepped from the server log.
    /// It only measures and logs; it never changes plugin behaviour.
    /// </summary>
    internal static class Perf
    {
        private static ILogger? _logger;

        // A frame that takes longer than this is logged together with the last command that ran.
        private const double FrameGapLogMs = 100;
        // Commands slower than this are logged (css_rtv is always logged).
        private const double CommandLogMs = 20;

        private static readonly Stopwatch FrameClock = new();
        private static string _lastCommand = "-";
        private static double _lastCommandMs;
        private static bool _tickRegistered;

        internal static void Init(BasePlugin plugin)
        {
            _logger = plugin.Logger;
            if (!_tickRegistered)
            {
                plugin.RegisterListener<Listeners.OnTick>(OnTick);
                _tickRegistered = true;
            }
            FrameClock.Restart();
        }

        internal static void Shutdown(BasePlugin plugin)
        {
            if (_tickRegistered)
            {
                try { plugin.RemoveListener<Listeners.OnTick>(OnTick); } catch { /* unloading */ }
                _tickRegistered = false;
            }
        }

        private static TimeSpan _tickJit;
        private static TimeSpan _tickGc;

        private static void OnTick()
        {
            double ms = FrameClock.Elapsed.TotalMilliseconds;
            FrameClock.Restart();
            TimeSpan jit = System.Runtime.JitInfo.GetCompilationTime();
            TimeSpan gc = GC.GetTotalPauseDuration();
            if (ms >= FrameGapLogMs)
                Log($"frame gap {ms:F0}ms (jit {(jit - _tickJit).TotalMilliseconds:F1} gc {(gc - _tickGc).TotalMilliseconds:F1}; last command: {_lastCommand} {_lastCommandMs:F1}ms)");
            _tickJit = jit;
            _tickGc = gc;
        }

        internal static void Log(string message)
        {
            try
            {
                if (_logger != null) _logger.LogInformation("{Message}", "[RTV.Perf] " + message);
                else Console.WriteLine("[RTV.Perf] " + message);
            }
            catch
            {
                // diagnostics must never break the plugin
            }
        }

        /// <summary>Wraps a command callback with a timer.</summary>
        internal static CommandInfo.CommandCallback Wrap(string name, CommandInfo.CommandCallback callback)
        {
            return (player, info) =>
            {
                long start = Stopwatch.GetTimestamp();
                TimeSpan jit0 = System.Runtime.JitInfo.GetCompilationTime();
                TimeSpan gc0 = GC.GetTotalPauseDuration();
                try
                {
                    callback(player, info);
                }
                finally
                {
                    double ms = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                    string who = player == null ? "console" : $"slot{SafeSlot(player)}";
                    _lastCommand = $"{name}({who})";
                    _lastCommandMs = ms;
                    if (ms >= CommandLogMs || name == "css_rtv")
                        Log($"command {name} by {who}: {ms:F1}ms (jit {(System.Runtime.JitInfo.GetCompilationTime() - jit0).TotalMilliseconds:F1} gc {(GC.GetTotalPauseDuration() - gc0).TotalMilliseconds:F1})");
                }
            };
        }

        private static string SafeSlot(CCSPlayerController player)
        {
            try { return player.Slot.ToString(); }
            catch { return "?"; }
        }

        /// <summary>Collects named stages and writes one summary line.</summary>
        internal sealed class Stages
        {
            private readonly string _name;
            private readonly long _start = Stopwatch.GetTimestamp();
            private long _mark;
            private TimeSpan _jitMark = System.Runtime.JitInfo.GetCompilationTime();
            private TimeSpan _gcMark = GC.GetTotalPauseDuration();
            private int _gen2Mark = GC.CollectionCount(2);
            private readonly List<(string Stage, double Ms, double Jit, double Gc, int Gen2)> _stages = new();
            private readonly Dictionary<string, (double Ms, int Count, double Max)> _sums = new();

            internal Stages(string name)
            {
                _name = name;
                _mark = _start;
            }

            /// <summary>Closes the current stage.</summary>
            internal void Mark(string stage)
            {
                long now = Stopwatch.GetTimestamp();
                TimeSpan jit = System.Runtime.JitInfo.GetCompilationTime();
                TimeSpan gc = GC.GetTotalPauseDuration();
                int gen2 = GC.CollectionCount(2);
                _stages.Add((stage, Stopwatch.GetElapsedTime(_mark, now).TotalMilliseconds,
                    (jit - _jitMark).TotalMilliseconds, (gc - _gcMark).TotalMilliseconds, gen2 - _gen2Mark));
                _mark = now;
                _jitMark = jit;
                _gcMark = gc;
                _gen2Mark = gen2;
            }

            /// <summary>Adds a repeated sub-step (for example one per player).</summary>
            internal void Add(string step, long startTimestamp)
            {
                double ms = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
                _sums.TryGetValue(step, out var cur);
                _sums[step] = (cur.Ms + ms, cur.Count + 1, Math.Max(cur.Max, ms));
            }

            internal void Finish(string extra = "")
            {
                double total = Stopwatch.GetElapsedTime(_start).TotalMilliseconds;
                var sb = new StringBuilder();
                sb.Append(_name).Append(" total=").Append(total.ToString("F1")).Append("ms");
                foreach (var (stage, ms, jit, gc, gen2) in _stages)
                {
                    sb.Append(' ').Append(stage).Append('=').Append(ms.ToString("F1"));
                    // osnet: show where the time went when a stage is slow (JIT compile time / GC pause / full GC count)
                    if (ms >= 20)
                        sb.Append("(jit ").Append(jit.ToString("F1")).Append(" gc ").Append(gc.ToString("F1"))
                          .Append(gen2 > 0 ? " gen2 x" + gen2 : "").Append(')');
                }
                foreach (var kv in _sums)
                    sb.Append(" [").Append(kv.Key).Append(" n=").Append(kv.Value.Count)
                      .Append(" sum=").Append(kv.Value.Ms.ToString("F1"))
                      .Append(" max=").Append(kv.Value.Max.ToString("F1")).Append(']');
                if (extra.Length > 0)
                    sb.Append(' ').Append(extra);
                Log(sb.ToString());
            }
        }
    }
}
