using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Extensions;

namespace OsnetSkinMenu;

public partial class Plugin
{
 private object PortalDiagnostics() {
  var players=Utilities.GetPlayers().Where(p=>p.IsValid&&!p.IsBot&&p.SteamID>0&&p.Slot is >=0 and <64).Select(p=>{
   _portalClients.TryGetValue(p.Slot,out var c);
   bool? capture=null;
   try{if(_portalLayout is {IsValid:true})capture=_portalLayout.IsInputCaptureEnabled(p);}catch{}
   return new {slot=p.Slot,team=p.TeamNum,allowed_menu=PortalPermission(p,"css_menu"),allowed_ws=PortalPermission(p,"css_ws"),defusing=PortalDefusing(p),preferences_loaded=c?.Loaded??false,open=c?.Open??false,tab=c?.Tab,open_attempts=c?.OpenAttempts??0,last_open=c?.OpenResult??"not_seen",root_hidden=c?.Classes.GetValueOrDefault("op-root|op-hidden"),input_capture=capture};
  }).ToArray();
  return new {version="0.3.1",ready=_portalReady,workshop="3833701930",schema=3,hud_valid=_portalLayout is {IsValid:true},layout=_portalLayout is {IsValid:true}?_portalLayout.StrLayout:null,menu_close_aliases=_portalLive.menu_close_aliases,players};
 }
 private void PortalTest(CCSPlayerController? caller,CommandInfo info) {
  if(caller!=null)return;
  var players=Utilities.GetPlayers().Where(p=>p.IsValid&&!p.IsBot&&p.SteamID>0&&p.Slot is >=0 and <64).ToArray();
  if(players.Length!=1){info.ReplyToCommand("OSNET_PORTAL_TEST: exactly one human player must be connected; no menu was opened.");return;}
  OpenPortal(players[0]);
  info.ReplyToCommand("OSNET_PORTAL_STATUS:"+JsonSerializer.Serialize(PortalDiagnostics()));
 }
}
