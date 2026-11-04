using System.Globalization;
using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;

namespace OsnetSkinMenu;

public partial class Plugin
{
 private void DrawPortal(CCSPlayerController p,PortalClient c){
  if(!EnsurePortal())return;
  if(c.Sent.Count==0)foreach(var key in PortalVariableNames)PV(p,c,key,"");
  foreach(var kv in PortalLabels)PV(p,c,kv.Key,kv.Value);
  var tabs=AllPortalTabs();if(!tabs.Any(t=>t.Id==c.Tab))c.Tab="intro";
  PV(p,c,"server_name",CounterStrikeSharp.API.Modules.Cvars.ConVar.Find("hostname")?.StringValue??"Sunucu");
  PV(p,c,"footer_brand",string.IsNullOrWhiteSpace(_portalData.public_address)?"OyunServerim.NET":_portalData.public_address);
  PV(p,c,"player_name",PortalFiles.Text(p.PlayerName,64));PV(p,c,"player_rank",PortalRanks[PortalRank(p)]);
  for(int i=0;i<7;i++){
   int at=c.TabStart+i;bool valid=at<tabs.Count;PC(p,c,"op-tab-"+i,"op-hidden",!valid);PV(p,c,"tab_"+i,valid?tabs[at].Name:"");PC(p,c,"op-tab-"+i,"is-active",valid&&tabs[at].Id==c.Tab);
  }
  PC(p,c,"op-tabs-prev","op-hidden",c.TabStart==0);PC(p,c,"op-tabs-next","op-hidden",c.TabStart+7>=tabs.Count);
  foreach(string tab in PortalTabs.Append("custom"))PC(p,c,"op-root","view-"+tab,tab==(c.Tab.StartsWith("custom-")?"custom":c.Tab));
  int pages=1;
  switch(c.Tab){
   case "intro":
    PV(p,c,"intro_title",_portalData.welcome_title);PV(p,c,"intro_body",_portalData.welcome_text);
    for(int i=0;i<3;i++)PaintPortalBlock(p,c,"op-announcement-"+i,"announcement_"+i,i<_portalData.announcements.Count?_portalData.announcements[i]:null);
    break;
   case "rules":pages=PaintPortalBlocks(p,c,_portalData.rules,"op-rule-","rule_");break;
   case "commands":
    var commands=PublicPortalCommands(p);pages=Math.Max(1,(commands.Count+7)/8);c.Page=Math.Min(c.Page,pages-1);
    for(int i=0;i<8;i++){
     var cmd=commands.Skip(c.Page*8+i).FirstOrDefault();PC(p,c,"op-command-"+i,"op-hidden",cmd==null);PV(p,c,$"command_{i}_name",cmd?.name??"");PV(p,c,$"command_{i}_description",cmd?.description??"");PV(p,c,$"command_{i}_example",cmd==null?"":"Örnek: "+cmd.example);
    }
    PV(p,c,"commands_subtitle",commands.Count>0?"Sunucuda herkese açık komutlar. Bir komuta tıklayarak kullanabilirsiniz.":"Bu mod için herkese açık komut bulunamadı.");break;
   case "settings":
    foreach(var item in new[]{("hint",c.Prefs.show_hint),("hold",c.Prefs.hold_e),("join",c.Prefs.show_on_join)}){
     PV(p,c,"setting_"+item.Item1+"_value",item.Item2?"Açık":"Kapalı");PC(p,c,"op-setting-"+item.Item1,"is-on",item.Item2);
    }
    PV(p,c,"setting_delay_value",c.Prefs.hold_seconds.ToString("0.0",CultureInfo.GetCultureInfo("tr-TR"))+" sn");PV(p,c,"settings_saved",c.Loaded?"Tercihleriniz yalnız kendi oyuncu hesabınız içindir.":"Kayıtlı tercihleriniz yükleniyor…");break;
   case "ranking":PaintPortalRanking(p,c);break;
   case "vip":PaintPortalVip(p,c);break;
   default:
    var custom=_portalData.custom_pages.FirstOrDefault(page=>page.id==c.Tab);
    if(custom!=null){PV(p,c,"custom_title",custom.title);pages=PaintPortalBlocks(p,c,custom.blocks,"op-custom_block-","custom_block_");}
    break;
  }
  PC(p,c,"op-pager","op-hidden",pages<=1);PV(p,c,"page_label",$"{c.Page+1} / {pages}");
 }
 private void PaintPortalBlock(CCSPlayerController p,PortalClient c,string id,string prefix,PortalBlock? block){
  PC(p,c,id,"op-hidden",block==null);PV(p,c,prefix+"_title",block?.title??"");PV(p,c,prefix+"_body",block?.body??"");PV(p,c,prefix+"_link",block?.url??"");
 }
 private int PaintPortalBlocks(CCSPlayerController p,PortalClient c,List<PortalBlock> blocks,string id,string prefix){
  int pages=Math.Max(1,(blocks.Count+5)/6);c.Page=Math.Min(c.Page,pages-1);
  for(int i=0;i<6;i++)PaintPortalBlock(p,c,id+i,prefix+i,blocks.Skip(c.Page*6+i).FirstOrDefault());return pages;
 }
 private void PaintPortalRanking(CCSPlayerController p,PortalClient c){
  PC(p,c,"op-self","op-hidden",true);for(int i=0;i<15;i++)PC(p,c,"op-rank-"+i,"op-hidden",true);
  if(!_portalLive.stats_active){PV(p,c,"ranking_empty","Sıralama eklentisi bu modda açık değil.");return;}
  if(!_portalReady||_portalDb==null){PV(p,c,"ranking_empty","Sıralama veritabanı henüz hazır değil.");return;}
  if(c.RankAt==0){PV(p,c,"ranking_empty","Sıralama yükleniyor…");}
  else {
   PV(p,c,"ranking_empty",c.RankError?"Sıralama okunamadı. Biraz sonra yeniden deneyin.":c.Ranking.Count==0?"Henüz sıralama kaydı yok. Oyuncular oynadıkça liste oluşacak.":"");
   for(int i=0;i<Math.Min(15,c.Ranking.Count);i++){
    var r=c.Ranking[i];PC(p,c,"op-rank-"+i,"op-hidden",false);PC(p,c,"op-rank-"+i,"is-self",r.Id==c.Steam.ToString(CultureInfo.InvariantCulture));
    foreach(var value in new Dictionary<string,string>{{"place",(i+1).ToString()},{"name",r.Name},{"points",r.Points.ToString("N0")},{"kills",r.Kills.ToString()},{"deaths",r.Deaths.ToString()},{"kd",Ratio(r)}})PV(p,c,$"ranking_{i}_{value.Key}",value.Value);
   }
   if(c.Standing is {} self){PC(p,c,"op-self","op-hidden",false);PV(p,c,"self_place","Sıran: #"+self.Place);PV(p,c,"self_points","Puan: "+self.Row.Points);PV(p,c,"self_kills","Öldürme: "+self.Row.Kills);PV(p,c,"self_deaths","Ölüm: "+self.Row.Deaths);PV(p,c,"self_kd","K/D: "+Ratio(self.Row));PV(p,c,"self_playtime",$"Süre: {self.Row.Seconds/3600} s {self.Row.Seconds%3600/60} dk");}
  }
  if(c.RankPending||PortalNow-c.RankAt<10000)return;
  c.RankPending=true;var db=_portalDb;
  _=Task.Run(async()=>{List<PortalRank> rows=[];PortalStanding? standing=null;bool failed=false;
   try{rows=await db.Top();standing=await db.Self(c.Steam.ToString(CultureInfo.InvariantCulture));}catch{failed=true;}
   if(!_portalDisposed)Server.NextFrame(()=>{if(!PortalSame(p,c))return;c.RankPending=false;c.RankAt=PortalNow;c.RankError=failed;c.Ranking=rows;c.Standing=standing;if(c.Open&&c.Tab=="ranking")DrawPortal(p,c);});
  });
 }
 private static string Ratio(PortalRank row)=>(row.Kills/(double)Math.Max(1,row.Deaths)).ToString("0.00",CultureInfo.GetCultureInfo("tr-TR"));
 private void PaintPortalVip(CCSPlayerController p,PortalClient c){
  try{
   string folder=Path.GetFullPath(Path.Combine(ModuleDirectory,"../../configs/plugins/OsnetVip"));
   var cfg=PortalFiles.Read(Path.Combine(folder,"OsnetVip.json"),new Dictionary<string,JsonElement>());
   var runtime=PortalFiles.Read(Path.Combine(folder,"runtime.json"),new Dictionary<string,JsonElement>());
   string mode=runtime.GetValueOrDefault("Mode").ValueKind==JsonValueKind.String?runtime["Mode"].GetString()??"":"";
   bool active=_portalLive.vip_active&&cfg.GetValueOrDefault("Enabled").ValueKind==JsonValueKind.True;
   for(int rank=1;rank<=3;rank++){
    string name=new[]{"vip","silver","golden"}[rank-1];List<string> perks=[];
    if(active){
     if(cfg.GetValueOrDefault("ColoredNames").ValueKind==JsonValueKind.True)perks.Add("Rütbeye özel renkli isim");
     if(cfg.GetValueOrDefault("ColoredText").ValueKind==JsonValueKind.True)perks.Add("Renkli sohbet yazısı");
     if(cfg.GetValueOrDefault("ScoreboardTags").ValueKind==JsonValueKind.True)perks.Add("Skor tablosunda rütbe etiketi");
     if(mode!="match")foreach(var perk in new[]{("DefuseKit","CT takımında defuse kiti"),("Zeus","Zeus"),("BodyArmor","Kasksız vücut zırhı")}){
      if(!cfg.TryGetValue(perk.Item1,out var value)||value.GetProperty("Enabled").ValueKind!=JsonValueKind.True||value.GetProperty("MinimumRank").GetInt32()>rank||!value.GetProperty("Modes").EnumerateArray().Any(m=>m.GetString()==mode))continue;
      perks.Add(perk.Item2);
     }
    }
    for(int i=0;i<6;i++)PV(p,c,$"vip_{name}_benefit_{i}",i<perks.Count?"• "+perks[i]:"");
    PV(p,c,$"vip_{name}_status",!active?"VIP eklentisi bu modda kapalı.":mode=="match"?"Maç modunda ekipman ayrıcalıkları kapalı.":"Bu modda açık özellikler gösterilir.");
   }
  }catch{foreach(string name in new[]{"vip","silver","golden"}){for(int i=0;i<6;i++)PV(p,c,$"vip_{name}_benefit_{i}","");PV(p,c,$"vip_{name}_status","VIP ayarları şu anda okunamıyor.");}}
 }
}
