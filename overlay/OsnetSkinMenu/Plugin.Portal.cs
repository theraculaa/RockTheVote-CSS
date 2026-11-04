using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Extensions;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Timers;
using Microsoft.Extensions.Logging;
using static CounterStrikeSharp.API.Core.Listeners;

namespace OsnetSkinMenu;

public partial class Plugin
{
 private sealed class PortalClient {
  public required ulong Steam;
  public required int Slot;
  public PortalPrefs Prefs=new();
  public bool Loaded,AutoDone,Open,Held,Dirty;
  public long Due,Pressed,NextClick;
  public int Revision,Page,TabStart,Request;
  public string Tab="intro";
  public long RankAt; public bool RankPending,RankError; public List<PortalRank> Ranking=[]; public PortalStanding? Standing;
  public readonly Dictionary<string,string> Sent=new();
  public readonly Dictionary<string,bool> Classes=new();
 }
 private CCSCustomHudLayout? _portalLayout;
 private PortalData _portalData=new();
 private PortalLive _portalLive=new();
 private PortalDatabase? _portalDb;
 private readonly Dictionary<int,PortalClient> _portalClients=[];
 private volatile bool _portalReady;
 private bool _portalDisposed,_portalLoading;
 private long _portalRetry;
 private string _portalRevision="";
 private string PortalFolder=>Path.GetFullPath(Path.Combine(ModuleDirectory,"../../configs/plugins/OsnetSkinMenu"));
 private static readonly string[] PortalTabs=["intro","skins","ranking","vip","rules","commands","settings"];
 private static readonly string[] PortalTabNames=["Tanıtım","Skinler","Sıralama","VIP","Kurallar","Komutlar","Ayarlar"];
 private static readonly string[] PortalRanks=["Oyuncu","VIP","Silver","Golden","Admin","Süper Admin","Server Sahibi"];
 private static long PortalNow=>Environment.TickCount64;
 private bool PortalPermission(CCSPlayerController p,string alias) =>
  !AdminManager.CommandIsOverriden(alias)||AdminManager.GetPlayerCommandOverrideState(p,alias);
 private bool PortalAccess(CCSPlayerController p)=>p.IsValid&&!p.IsBot&&p.SteamID>0&&p.Slot is >=0 and <64&&PortalPermission(p,"css_ws")&&PortalPermission(p,"css_menu");
 private static int PortalRank(CCSPlayerController p){
  bool Has(string f)=>AdminManager.PlayerHasPermissions(p,f);
  if(Has("@css/root")||Has("@osnet/owner"))return 6;
  if(Has("@osnet/superadmin")||Has("@css/permban"))return 5;
  if(Has("@osnet/admin")||Has("@css/generic"))return 4;
  if(Has("@osnet/gold"))return 3;
  if(Has("@osnet/silver"))return 2;
  return Has("@osnet/vip")||Has("@css/vip")?1:0;
 }
 private void StartPortal(bool hotReload){
  _portalDisposed=false;ReloadPortal();
  AddCommand("css_menu","Sunucu ana menüsünü açar",(p,_)=>{if(p!=null)OpenPortal(p);});
  // Exact aliases only. No global command gate; all unrelated commands continue untouched.
  foreach(string alias in new[]{"css_ws","css_menu"})AddCommandListener(alias,(p,_)=>{
   if(p==null||!p.IsValid||p.IsBot)return HookResult.Continue;
   if(!PortalAccess(p)){p.PrintToChat("[OyunServerim.NET] Bu menü için yetkiniz yok.");return HookResult.Handled;}
   OpenPortal(p);return HookResult.Handled;
  },HookMode.Pre);
  foreach(string alias in _portalLive.menu_close_aliases.Where(a=>Regex.IsMatch(a,@"\Acss_[a-z][a-z0-9_]{1,47}\z")).Distinct())AddCommandListener(alias,(p,_)=>{
   if(p!=null&&p.IsValid&&_portalClients.TryGetValue(p.Slot,out var c)&&c.Open)ClosePortal(p,c);
   return HookResult.Continue;
  },HookMode.Pre);
  AddCommand("osnet_portal_status","Server-only portal readiness",(p,i)=>{if(p==null)i.ReplyToCommand("OSNET_PORTAL_STATUS:"+JsonSerializer.Serialize(new{version="0.2.0",ready=_portalReady,workshop="3833701930",schema=1}));});
  RegisterListener<OnCustomHudClicked>(PortalClick);
  RegisterListener<OnClientConnected>(slot=>_portalClients.Remove(slot));
  RegisterListener<OnClientDisconnect>(slot=>_portalClients.Remove(slot));
  RegisterListener<OnTick>(PortalTick);
  AddTimer(1,PortalPoll,TimerFlags.REPEAT);
  foreach(var p in Utilities.GetPlayers().Where(p=>p.IsValid&&!p.IsBot&&p.SteamID>0))PortalClientFor(p,hotReload);
  InitPortalDb();
 }
 private void InitPortalDb(){
  if(_portalLoading||_portalDisposed||_portalReady||PortalNow<_portalRetry)return;
  _portalLoading=true;_portalRetry=PortalNow+30000;
  try{_portalDb??=new PortalDatabase(ModuleDirectory);}
  catch{_portalLoading=false;Logger.LogWarning("Portal SQL identity/configuration is not ready. No database credentials were logged.");return;}
  var db=_portalDb;
  _=Task.Run(async()=>{bool ok=false;try{await db.Init();ok=true;}catch{ }
   if(!_portalDisposed)Server.NextFrame(()=>{if(_portalDisposed)return;_portalLoading=false;_portalReady=ok;if(!ok)Logger.LogWarning("Portal SQL initialization failed; retrying later.");});
  });
 }
 private PortalClient PortalClientFor(CCSPlayerController p,bool existing=false){
  if(_portalClients.TryGetValue(p.Slot,out var c)&&c.Steam==p.SteamID)return c;
  c=new(){Slot=p.Slot,Steam=p.SteamID,Prefs=_portalData.player_defaults.Copy(),AutoDone=existing,Due=PortalNow+_portalData.welcome_delay_seconds*1000};_portalClients[p.Slot]=c;return c;
 }
 private bool PortalSame(CCSPlayerController p,PortalClient c)=>!_portalDisposed&&p.IsValid&&!p.IsBot&&p.Slot==c.Slot&&p.SteamID==c.Steam&&_portalClients.GetValueOrDefault(c.Slot)==c;
 private void LoadPortalPrefs(CCSPlayerController p,PortalClient c){
  if(c.Loaded||c.Request!=0||!_portalReady||_portalDb==null)return;
  c.Request=1;int revision=c.Revision;var db=_portalDb;
  _=Task.Run(async()=>{PortalPrefs? prefs=null;bool failed=false;try{prefs=await db.Load(c.Steam.ToString(CultureInfo.InvariantCulture));}catch{failed=true;}
   if(!_portalDisposed)Server.NextFrame(()=>{if(!PortalSame(p,c))return;c.Request=0;c.Loaded=true;
    if(!failed&&prefs!=null&&revision==c.Revision)c.Prefs=prefs;
    if(failed){c.AutoDone=true;p.PrintToChat("[OyunServerim.NET] Menü tercihleriniz okunamadı; otomatik açılış atlandı. !menu ile açabilirsiniz.");}
    if(c.Open)DrawPortal(p,c);
   });
  });
 }
 private void SavePortalPrefs(CCSPlayerController p,PortalClient c){
  c.Revision++;var prefs=c.Prefs.Copy();var db=_portalDb;
  if(!_portalReady||db==null){p.PrintToChat("[OyunServerim.NET] Tercih bu bağlantıda uygulandı; veritabanı hazır olmadığı için kalıcı kaydedilemedi.");return;}
  int revision=c.Revision;long stamp=DateTime.UtcNow.Ticks;
  _=Task.Run(async()=>{bool ok=true;try{await db.Save(c.Steam.ToString(CultureInfo.InvariantCulture),prefs,stamp);}catch{ok=false;}
   if(!_portalDisposed)Server.NextFrame(()=>{if(!PortalSame(p,c)||revision!=c.Revision)return;if(c.Open)PV(p,c,"settings_saved",ok?"Tercihiniz kaydedildi.":"Kalıcı kayıt başarısız. Tercih bu bağlantıda geçerli.");});
  });
 }
 private bool EnsurePortal(){
  if(_portalLayout is {IsValid:true})return true;
  var layout=Utilities.CreateEntityByName<CCSCustomHudLayout>("custom_hud_layout");if(layout==null||layout.Handle==IntPtr.Zero)return false;
  layout.StrLayout="panorama/layout/custom_game/osnet_portal.xml";layout.DispatchSpawn();if(!layout.IsValid)return false;
  _portalLayout=layout;foreach(var c in _portalClients.Values){c.Sent.Clear();c.Classes.Clear();}return true;
 }
 private void PV(CCSPlayerController p,PortalClient c,string key,string value){
  value=PortalFiles.Text(value);if(c.Sent.GetValueOrDefault(key)==value||_portalLayout is not {IsValid:true})return;
  _portalLayout.SetDialogVariableStringForPlayer(p,key.StartsWith("hint_")?"op-hint":"op-root",key,value);c.Sent[key]=value;
 }
 private void PC(CCSPlayerController p,PortalClient c,string id,string name,bool value){
  string key=id+"|"+name;if(c.Classes.TryGetValue(key,out bool old)&&old==value||_portalLayout is not {IsValid:true})return;
  _portalLayout.SetHasClassForPlayer(p,id,name,value);c.Classes[key]=value;
 }
 private void OpenPortal(CCSPlayerController p){
  if(!PortalAccess(p)||!EnsurePortal())return;
  var c=PortalClientFor(p);c.AutoDone=true;
  try{_menus.Get()?.CloseMenu(p);}catch{ }
  Hide(p);MenuManager.CloseActiveMenu(p);c.Open=true;c.Page=0;c.TabStart=0;c.Tab="intro";DrawPortal(p,c);
  PC(p,c,"op-root","op-hidden",false);PC(p,c,"op-hint","op-hidden",true);
  Later(.2f,()=>{if(PortalSame(p,c)&&c.Open&&_portalLayout is {IsValid:true})_portalLayout.SetInputCaptureEnabled(p,true);});
 }
 private void ClosePortal(CCSPlayerController p,PortalClient c){
  c.Open=false;c.NextClick=PortalNow+200;
  if(_portalLayout is {IsValid:true}){PC(p,c,"op-root","op-hidden",true);_portalLayout.SetInputCaptureEnabled(p,false);PortalHint(p,c);}
 }
 private void PortalHint(CCSPlayerController p,PortalClient c){
  if(!EnsurePortal())return;
  bool show=c.Loaded&&c.Prefs.show_hint&&!c.Open&&PortalAccess(p)&&!_slots[p.Slot].Shown;
  PC(p,c,"op-hint","op-hidden",!show);PV(p,c,"hint_title","Sunucu menüsü");PV(p,c,"hint_subtitle",c.Prefs.hold_e?c.Prefs.hold_seconds.ToString("0.0",CultureInfo.GetCultureInfo("tr-TR"))+" sn basılı tut":"!menu ile aç");
 }
 private void PortalTick(){
  if(Server.TickCount%4!=0||_portalDisposed)return;
  foreach(var c in _portalClients.Values.ToArray()){
   var p=Utilities.GetPlayerFromSlot(c.Slot);if(p==null||!PortalSame(p,c))continue;
   bool down=(p.Buttons&PlayerButtons.Use)!=0;
   if(!down){c.Held=false;c.Pressed=0;continue;}
   if(c.Held)continue;
   if(c.Pressed==0)c.Pressed=PortalNow;
   if(c.Open||!c.Loaded||!c.Prefs.hold_e||PortalNow-c.Pressed<c.Prefs.hold_seconds*1000)continue;
   c.Held=true;OpenPortal(p);
  }
 }
 private void PortalPoll(){
  if(_portalDisposed)return;InitPortalDb();ReloadPortal();
  foreach(var p in Utilities.GetPlayers().Where(p=>p.IsValid&&!p.IsBot&&p.SteamID>0&&p.Slot is >=0 and <64)){
   var c=PortalClientFor(p);LoadPortalPrefs(p,c);
   if(c.Loaded&&!c.AutoDone&&PortalNow>=c.Due){
    bool busy=false;try{busy=MenuManager.GetActiveMenu(p)!=null||(_menus.Get()?.HasOpenedMenu(p)??false);}catch{busy=true;}
    // Wait for team selection and existing menus; do not reopen on spawn/round/map.
    if(p.TeamNum>=2&&!busy){c.AutoDone=true;if(c.Prefs.show_on_join)OpenPortal(p);}
   }
   if(c.Open&&(!PortalAccess(p)||MenuManager.GetActiveMenu(p)!=null))ClosePortal(p,c);else if(c.Open&&c.Dirty){c.Dirty=false;DrawPortal(p,c);}
   PortalHint(p,c);
  }
 }
 private void ReloadPortal(){
  string dataPath=Path.Combine(PortalFolder,"OsnetPortal.json"),livePath=Path.Combine(PortalFolder,"PortalLive.json");
  try{
   string revision=(File.Exists(dataPath)?File.GetLastWriteTimeUtc(dataPath).Ticks:0)+":"+(File.Exists(livePath)?File.GetLastWriteTimeUtc(livePath).Ticks:0);
   if(revision==_portalRevision)return;
   var data=PortalFiles.Read(dataPath,new PortalData());PortalFiles.Check(data);var live=PortalFiles.Read(livePath,new PortalLive());
   _portalData=data;_portalLive=live;_portalRevision=revision;foreach(var c in _portalClients.Values)c.Dirty=true;
  }catch{Logger.LogWarning("Portal content could not be reloaded; retaining the last valid content.");}
 }
 private List<(string Id,string Name)> AllPortalTabs()=>PortalTabs.Select((id,i)=>(id,PortalTabNames[i])).Concat(_portalData.custom_pages.Select(p=>(p.id,p.title))).ToList();
 private List<PortalCommand> PublicPortalCommands(CCSPlayerController p)=>_portalLive.commands.Where(c=>Regex.IsMatch(c.alias,@"\Acss_[a-z][a-z0-9_]{1,47}\z")&&PortalPermission(p,c.alias)&&PortalPublicPolicy(c)).Take(1000).ToList();
 private bool PortalPublicPolicy(PortalCommand command){
  try {
   string root=Path.GetFullPath(Path.Combine(ModuleDirectory,"../../configs/plugins"));
   var policy=PortalFiles.Read(Path.Combine(root,"OsnetVip/command-policy.json"),new Dictionary<string,JsonElement>());
   if(command.id.Length>0&&policy.TryGetValue("Assignments",out var assignments)&&assignments.TryGetProperty(command.id,out var level)&&level.ValueKind==JsonValueKind.Number&&level.GetInt32()>0)return false;
   if(command.kind=="rtv"){
    var rtv=PortalFiles.Read(Path.Combine(root,"RockTheVote/RockTheVote.json"),new Dictionary<string,JsonElement>());
    if(rtv.TryGetValue("CommandPermissions",out var permissions)&&permissions.TryGetProperty(command.key,out var flags)&&flags.ValueKind==JsonValueKind.Array&&flags.GetArrayLength()>0)return false;
    var fields=new Dictionary<string,(string Section,string Field)>{{"nominate",("Nominate","Permission")},{"votemap",("Votemap","Permission")},{"voteextend",("VoteExtend","Permission")},{"extend",("General","ExtendPermission")},{"mapmenu",("MapChooser","Permission")}};
    if(fields.TryGetValue(command.key,out var f)&&rtv.TryGetValue(f.Section,out var section)&&section.TryGetProperty(f.Field,out var value)&&value.ValueKind==JsonValueKind.String&&!string.IsNullOrWhiteSpace(value.GetString()))return false;
   }
   return true;
  }catch{return command.alias is "css_menu" or "css_ws";}
 }
 private void PortalClick(CCSPlayerController p,CCSCustomHudLayout layout,string id){
  if(_portalLayout is not {IsValid:true}||layout.Handle!=_portalLayout.Handle||!_portalClients.TryGetValue(p.Slot,out var c)||!PortalSame(p,c)||!c.Open||!PortalAccess(p)||PortalNow<c.NextClick)return;
  c.NextClick=PortalNow+150;
  if(id=="op-close"){ClosePortal(p,c);return;}
  var tabs=AllPortalTabs();
  if(id is "op-tabs-prev" or "op-tabs-next"){c.TabStart=Math.Clamp(c.TabStart+(id.EndsWith("next")?7:-7),0,Math.Max(0,((tabs.Count-1)/7)*7));DrawPortal(p,c);return;}
  if(id.StartsWith("op-tab-")&&int.TryParse(id[7..],out int tab)&&tab is >=0 and <7&&tab+c.TabStart<tabs.Count){c.Tab=tabs[tab+c.TabStart].Id;c.Page=0;DrawPortal(p,c);return;}
  if(id is "op-open-skins" or "op-open-ranking" or "op-open-rules"){c.Tab=id[8..];c.TabStart=0;c.Page=0;DrawPortal(p,c);return;}
  if(id is "op-next" or "op-prev"){c.Page=Math.Max(0,c.Page+(id=="op-next"?1:-1));DrawPortal(p,c);return;}
  if(id.StartsWith("op-skin-")){
   string key=id[8..];if(!new[]{"weapons","knife","gloves","agents","music","pins","stickers"}.Contains(key))return;
   string command=_bridge.Command(key=="weapons"?"skins":key);
   if(!PortalPermission(p,"css_"+command)){p.PrintToChat("[OyunServerim.NET] Bu özellik için yetkiniz yok.");return;}
   ClosePortal(p,c);try{_bridge.OpenPortalSection(p,key);}catch{p.PrintToChat("[OyunServerim.NET] Skin seçicisi şu anda açılamadı.");}return;
  }
  if(id.StartsWith("op-command-")&&int.TryParse(id[11..],out int ix)&&ix is >=0 and <8){
   var commands=PublicPortalCommands(p);int position=c.Page*8+ix;if(position>=commands.Count)return;
   string alias=commands[position].alias;ClosePortal(p,c);Server.NextFrame(()=>{if(PortalSame(p,c)&&PortalPermission(p,alias))p.ExecuteClientCommandFromServer(alias);});return;
  }
  bool changed=true;
  switch(id){case "op-setting-hint":c.Prefs.show_hint=!c.Prefs.show_hint;break;case "op-setting-hold":c.Prefs.hold_e=!c.Prefs.hold_e;break;case "op-setting-join":c.Prefs.show_on_join=!c.Prefs.show_on_join;break;case "op-delay-plus":c.Prefs.hold_seconds=Math.Min(2,Math.Round(c.Prefs.hold_seconds+.1,1));break;case "op-delay-minus":c.Prefs.hold_seconds=Math.Max(.3,Math.Round(c.Prefs.hold_seconds-.1,1));break;default:changed=false;break;}
  if(changed){SavePortalPrefs(p,c);DrawPortal(p,c);}
 }
 private void PortalMapStart(){
  _portalLayout=null;foreach(var c in _portalClients.Values){c.Open=false;c.Held=true;c.Pressed=0;c.Sent.Clear();c.Classes.Clear();}
 }
 private void StopPortal(){
  _portalDisposed=true;
  foreach(var c in _portalClients.Values){var p=Utilities.GetPlayerFromSlot(c.Slot);if(p is {IsValid:true}&&p.SteamID==c.Steam&&_portalLayout is {IsValid:true})_portalLayout.SetInputCaptureEnabled(p,false);}
  if(_portalLayout is {IsValid:true})_portalLayout.Remove();_portalLayout=null;_portalClients.Clear();
 }
}
