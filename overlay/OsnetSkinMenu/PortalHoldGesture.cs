namespace OsnetSkinMenu;

// Engine-free state machine: an interrupted use/defuse gesture cannot later open a menu.
internal sealed class PortalHoldGesture
{
    private long? _pressed;
    private bool _consumed;
    internal void SuppressUntilRelease() { _pressed = null; _consumed = true; }
    internal (int Segments, bool Open) Update(bool down, bool blocked, bool enabled, long now, double seconds)
    {
        if (!down) { _pressed = null; _consumed = false; return (0, false); }
        if (blocked || !enabled) { SuppressUntilRelease(); return (0, false); }
        if (_consumed) return (0, false);
        _pressed ??= now;
        double progress = Math.Clamp((now - _pressed.Value) / (Math.Clamp(seconds, .3, 2) * 1000), 0, 1);
        if (progress < 1) return ((int)Math.Ceiling(progress * 16), false);
        _consumed = true;
        return (16, true);
    }
}
