using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;

namespace cs2_rockthevote
{
    public partial class Plugin
    {
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
        public void OnReloadMapsCommand(CCSPlayerController? player, CommandInfo command)
        {
            _reloadMapsCommand.CommandHandler(player, command);
        }
    }

    public class ReloadMapsCommand : IPluginDependency<Plugin, Config>
    {
        private readonly MapLister _mapLister;
        private readonly ILogger<ReloadMapsCommand> _logger;
        private readonly StringLocalizer _localizer;
        private GeneralConfig _generalConfig = new();

        public ReloadMapsCommand(MapLister mapLister, StringLocalizer localizer, ILogger<ReloadMapsCommand> logger)
        {
            _mapLister = mapLister;
            _localizer = localizer;
            _logger = logger;
        }

        public void OnConfigParsed(Config config)
        {
            _generalConfig = config.General;
        }

        public void CommandHandler(CCSPlayerController? player, CommandInfo command)
        {
            if (player != null && !PermissionUtility.HasAny(player, _generalConfig.AdminPermissions))
            {
                command.ReplyToCommand(_localizer.LocalizeWithPrefix("general.incorrect.permission"));
                return;
            }

            try
            {
                _mapLister.LoadMaps();
                if (!_mapLister.MapsLoaded)
                {
                    command.ReplyToCommand(_localizer.LocalizeWithPrefix("admin.maplist-reload-failed"));
                    return;
                }

                command.ReplyToCommand(_localizer.LocalizeWithPrefix("admin.maplist-reloaded"));
            }
            catch (FileNotFoundException ex)
            {
                _logger.LogError(ex, "[RTV.ReloadMaps] maplist.txt not found while running css_reloadmaps.");
                command.ReplyToCommand(_localizer.LocalizeWithPrefix("admin.maplist-not-found"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[RTV.ReloadMaps] Failed to reload map list via css_reloadmaps.");
                command.ReplyToCommand(_localizer.LocalizeWithPrefix("admin.maplist-reload-error"));
            }
        }
    }
}
