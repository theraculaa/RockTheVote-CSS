using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace OsnetSkinMenu;

internal sealed class PortalBlock
{
 public string title {get;set;}="";
 public string body {get;set;}="";
 public string url {get;set;}="";
}
internal sealed class PortalPage
{
 public string id {get;set;}="";
 public string title {get;set;}="";
 public List<PortalBlock> blocks {get;set;}=[];
}
internal sealed class PortalPrefs
{
 public bool show_hint {get;set;}=true;
 public bool hold_e {get;set;}=true;
 public bool show_on_join {get;set;}=true;
 public double hold_seconds {get;set;}=.7;
 public PortalPrefs Copy()=>new(){show_hint=show_hint,hold_e=hold_e,show_on_join=show_on_join,hold_seconds=hold_seconds};
}
internal sealed class PortalData
{
 public int schema_version {get;set;}=1;
 public string welcome_title {get;set;}="Sunucuna hoş geldin.";
 public string welcome_text {get;set;}="Sunucunu tanı, envanterini düzenle ve oyununa hazırlan.";
 public string public_address {get;set;}="oyunserverim.net";
 public List<PortalBlock> announcements {get;set;}=[];
 public List<PortalBlock> rules {get;set;}=[];
 public List<PortalPage> custom_pages {get;set;}=[];
 public PortalPrefs player_defaults {get;set;}=new();
 public int welcome_delay_seconds {get;set;}=5;
}
internal sealed class PortalCommand
{
 public string name {get;set;}="";
 public string description {get;set;}="";
 public string example {get;set;}="";
 public string alias {get;set;}="";
 public string id {get;set;}="";
 public string kind {get;set;}="";
 public string key {get;set;}="";
}
internal sealed class PortalLive
{
 public List<PortalCommand> commands {get;set;}=[];
 public List<string> menu_close_aliases {get;set;}=["css_admin"];
 public bool vip_active {get;set;}
 public bool stats_active {get;set;}
 public string mode {get;set;}="";
}
internal static class PortalFiles
{
 public static T Read<T>(string path,T fallback) {
  if(!File.Exists(path))return fallback;
  var f=new FileInfo(path);
  if(f.Length>131072||f.LinkTarget!=null)throw new InvalidDataException("Portal configuration size/type invalid");
  return JsonSerializer.Deserialize<T>(File.ReadAllBytes(path),new JsonSerializerOptions{AllowTrailingCommas=true,ReadCommentHandling=JsonCommentHandling.Skip,MaxDepth=16})??fallback;
 }
 public static string Text(string? text,int max=1200) {
  // Dialog variables must remain literal text even in layouts with markup support.
  string value=new((text??"").Where(c=>!char.IsControl(c)||c=='\n'||c=='\t').ToArray());
  value=value.Replace('<','‹').Replace('>','›');
  return value.Length>max?value[..max]:value;
 }
 public static void Check(PortalData d) {
  if(d.schema_version!=1||d.announcements.Count>3||d.rules.Count>20||d.custom_pages.Count>20||d.custom_pages.Select(p=>p.id).Distinct().Count()!=d.custom_pages.Count||d.welcome_delay_seconds<3||d.welcome_delay_seconds>30)throw new InvalidDataException("Portal schema invalid");
  if(d.player_defaults.hold_seconds<.3||d.player_defaults.hold_seconds>2)throw new InvalidDataException("Portal preference invalid");
  foreach(var p in d.custom_pages)if(!Regex.IsMatch(p.id,@"\Acustom-[a-z0-9_-]{1,40}\z")||p.title.Length is <1 or >64||p.blocks.Count>20)throw new InvalidDataException("Portal page invalid");
  foreach(var b in d.rules.Concat(d.announcements).Concat(d.custom_pages.SelectMany(p=>p.blocks)))if(b.title.Length>64||b.body.Length>1200||b.url.Length>240)throw new InvalidDataException("Portal content invalid");
 }
}
