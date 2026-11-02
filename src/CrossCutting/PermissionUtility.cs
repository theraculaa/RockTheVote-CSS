using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;

namespace cs2_rockthevote
{
    public static class PermissionUtility
    {
        private static Dictionary<string,List<string>> _rules = new();
        public static void Configure(Dictionary<string,List<string>> rules) { _rules = rules ?? new(); }
        public static bool Allowed(CCSPlayerController player, string command, IReadOnlyCollection<string>? legacy = null)
        {
            command = command switch { "nom" => "nominate", "ve" => "voteextend", "maplist" => "maps", _ => command };
            if (_rules.TryGetValue(command, out var permissions))
            {
                if (HasAny(player, permissions)) return true;
                // These handlers check their original configured permission themselves.
                if (legacy == null && new[]{"nominate","votemap","voteextend","extend","mapmenu","reloadmaps","reloadrtv"}.Contains(command)) return true;
                if (legacy is { Count: > 0 }) return HasAny(player, legacy);
                // Existing administrators retain formerly public commands.
                return AdminManager.PlayerHasPermissions(player, "@css/generic") || AdminManager.PlayerHasPermissions(player, "@css/root");
            }
            return legacy == null || HasAny(player, legacy);
        }

        public static string[] Parse(string? permissions)
        {
            return string.IsNullOrWhiteSpace(permissions)
                ? Array.Empty<string>()
                : [.. permissions
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(permission => !string.IsNullOrWhiteSpace(permission))
                    .Distinct(StringComparer.Ordinal)];
        }

        public static bool HasAny(CCSPlayerController player, IReadOnlyCollection<string> permissions)
        {
            if (permissions.Count == 0)
                return true;

            foreach (string permission in permissions)
            {
                if (AdminManager.PlayerHasPermissions(player, permission))
                    return true;
            }

            return false;
        }
    }
}
