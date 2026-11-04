"""Reproducible adaptation of pinned GPL upstreams; no host changes."""
from pathlib import Path
import re, shutil, json
ROOT=Path(__file__).resolve().parent
OUT=ROOT/'adapted'
if OUT.exists():shutil.rmtree(OUT)
OUT.mkdir()
mapping={'MenuManagerCore':'OsnetSkinMenuCore','PMM_WeaponSkins':'OsnetSkinMenu','WeaponSkins':'OsnetSkins','MenuManagerApi':'OsnetSkinMenuApi'}
for upstream,folder in [('core','MenuManagerApi'),('core','MenuManagerCore'),('skin','WeaponSkins'),('pmm','PMM_WeaponSkins')]:
    source=ROOT/'src'/upstream/folder
    for p in source.rglob('*'):
        if not p.is_file()or any(x in p.parts for x in ('bin','obj','3rd_party')):continue
        rel=str(p.relative_to(source));target=OUT/mapping[folder]/rel
        for old,new in mapping.items():target=Path(str(target).replace(old,new))
        target.parent.mkdir(parents=True,exist_ok=True)
        if p.suffix in ('.cs','.csproj','.json','.xml','.css','.txt'):
            s=p.read_text(encoding='utf-8-sig')
            for old,new in mapping.items():s=s.replace(old,new)
            s=re.sub(r'\bMenuManager\b','OsnetSkinMenus',s)
            s=s.replace('CounterStrikeSharp.API.Modules.Menu.OsnetSkinMenus','CounterStrikeSharp.API.Modules.Menu.MenuManager')
            s=s.replace('menu:nfcore','osnet:skins:menu').replace('pmm:paint','osnet:skins:paint')
            s=s.replace('"ws_', '"osnet_skins_') if p.suffix=='.cs'and('/DB/'in str(p)or'/DiscordBot/Database/'in str(p)) else s
            if p.suffix=='.cs':s=s.replace('player_menus','osnet_skin_menus')
            target.write_text(s)
        else:shutil.copyfile(p,target)

def patch(rel,old,new):
    p=OUT/rel;s=p.read_text()
    if old not in s:raise RuntimeError('Patch anchor missing: '+rel+' '+old[:70])
    p.write_text(s.replace(old,new))

# Private API, capabilities, command names and SQL table avoid replacing any existing menu plugin.
for p in (OUT/'OsnetSkinMenuCore').rglob('*.cs'):
    s=p.read_text().replace('css_','css_oskin_').replace('"menuselect"','"oskin_menuselect"')
    p.write_text(s)
patch('OsnetSkinMenuCore/Main.cs','if (Config.CS2MenuManagerCompat)','if (false)')
patch('OsnetSkinMenuCore/Main.cs','AddTimer(3.0f, PisexLayout.Sync, TimerFlags.REPEAT);','// Isolated skin menus never bridge an existing server menu.')
patch('OsnetSkinMenuCore/Main.cs','Cs2MenuManagerDeploy.Uninstall();','// No global compatibility assembly was installed.')
patch('OsnetSkinMenuCore/Main.cs','["menu", "menus"]','[]')
patch('OsnetSkinMenuCore/Main.cs','ModuleName => "[FORK] OsnetSkinMenus"','ModuleName => "OyunServerim Skin Menu Core"')
patch('OsnetSkinMenuCore/Main.cs','ModuleVersion => "v1.3.01"','ModuleVersion => "0.1.0"')
patch('OsnetSkinMenuCore/DataBaseService.cs','_useSqlite = true;', 'throw new InvalidOperationException("This build requires the server osnet_sN database; SQLite fallback is disabled.");\n            _useSqlite = true;')
for name,refs in [('OsnetSkinMenuApi',[]),('OsnetSkinMenuCore',['OsnetSkinMenuApi']),('OsnetSkinMenu',['OsnetSkinMenuApi']),('OsnetSkins',[])]:
    p=OUT/name/(name+'.csproj')
    packages='<PackageReference Include="CounterStrikeSharp.API" Version="1.0.378" />'
    if name=='OsnetSkinMenuCore':packages+='\n<PackageReference Include="Dapper" Version="2.1.89"/><PackageReference Include="MySqlConnector" Version="2.6.2"/><PackageReference Include="Microsoft.Data.Sqlite" Version="10.0.12"/>'
    if name=='OsnetSkins':packages+='\n<PackageReference Include="Discord.Net" Version="3.20.1"/><PackageReference Include="MySqlConnector" Version="2.6.2"/>'
    refsXml=''.join('<ProjectReference Include="../'+r+'/'+r+'.csproj"><Private>false</Private></ProjectReference>'for r in refs)
    extra='<Compile Remove="Compat/**"/>'if name=='OsnetSkinMenuCore'else''
    content=''
    if name=='OsnetSkins':content='<None Update="Data/**" CopyToOutputDirectory="PreserveNewest"/><None Update="Lang/**" CopyToOutputDirectory="PreserveNewest" TargetPath="lang/%(Filename)%(Extension)"/>'
    if name=='OsnetSkinMenuCore':content='<None Update="lang/**" CopyToOutputDirectory="PreserveNewest"/>'
    if name=='OsnetSkinMenu':content='<None Update="ws_items.json" CopyToOutputDirectory="PreserveNewest"/>'
    p.write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><AllowUnsafeBlocks>true</AllowUnsafeBlocks><CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies><AssemblyName>'+name+'</AssemblyName><Version>0.1.0</Version></PropertyGroup><ItemGroup>'+packages+refsXml+extra+content+'</ItemGroup></Project>')

# Core does not ship or write a compatibility replacement assembly.
p=OUT/'OsnetSkinMenuCore/Main.cs';s=p.read_text();s=re.sub(r'        if \(false\)\s*\{\s*Cs2MenuManagerDeploy.Install\(this\);\s*\}', '', s);p.write_text(s)

# Use the public ready-made Workshop layout; the dynamic heading carries our brand.
patch('OsnetSkinMenu/Plugin.cs','public string Language { get; set; } = "en";', 'public string Language { get; set; } = "tr";\n [JsonPropertyName("Brand")] public string Brand { get; set; } = "OyunServerim.NET";')
patch('OsnetSkinMenu/Plugin.View.cs','send.Var(Root, "ws_title", snapshot.Title);','send.Var(Root, "ws_title", Config.Brand + " · " + snapshot.Title);')
patch('OsnetSkinMenu/Plugin.cs','"1.1.8", "1.1.9"','"0.1.0"')
patch('OsnetSkinMenu/Plugin.cs','_bridge = new WsBridge(Logger);','_bridge = new WsBridge(Logger);\n        AddCommand("osnet_skin_menu_status", "Hosting menu probe", (caller, info) => { if(caller is null)info.ReplyToCommand("OSNET_SKIN_MENU_READY:"+(_painterAdded?"1":"0")); });')
patch('OsnetSkinMenu/Lang.cs','"ru" => "ru",','"tr" => "tr",\n            "ru" => "ru",')
patch('OsnetSkinMenu/Lang.cs','"ru" => Ru,','"tr" => Tr,\n            "ru" => Ru,')
tr={'back':'‹   GERİ','equipped':'Kuşanıldı','default':'Varsayılan','none':'Yok','cat.weapons':'SİLAHLAR','cat.knife':'BIÇAK','cat.gloves':'ELDİVEN','cat.agents':'AJAN','cat.music':'MÜZİK','cat.pins':'ROZET','cat.stickers':'ÇIKARTMA','sub.weapons':'Silah görünümleri','sub.stickers':'Elindeki silah için','crumb.root':'Envanterin','crumb.sep':'  ›  ','prompt.title':'Sohbete yaz','prompt.hint':'Y tuşuna bas, yanıtı yaz ve Enter ile gönder. Geri dönmek için - yaz.','hint.items':'{0} eşya · seçmek için karta tıkla','hint.item':'1 eşya · seçmek için karta tıkla','hint.empty':'Gösterilecek eşya yok','hint.board':'Değiştirmek için satıra tıkla','equipped.count':'{0} kuşanıldı','on':'Açık','off':'Kapalı','line.wear':'Aşınma  {0}','line.seed':'Desen  {0}','line.stattrak':'StatTrak  {0}','line.nametag':'İsim etiketi  {0}','wear.fn':'Fabrikadan Yeni Çıkmış','wear.mw':'Az Aşınmış','wear.ft':'Görevde Kullanılmış','wear.ww':'Eskimiş','wear.bs':'Savaş Görmüş','rarity.1':'Tüketici Sınıfı','rarity.2':'Endüstri Sınıfı','rarity.3':'Askerî Sınıf','rarity.4':'Sınırlı','rarity.5':'Gizli','rarity.6':'Çok Gizli','rarity.7':'Kaçak','rarity.glove':'Olağanüstü','rarity.item.1':'Temel Sınıf','rarity.item.3':'Yüksek Sınıf','rarity.item.4':'Kayda Değer','rarity.item.5':'Egzotik','rarity.item.6':'Olağanüstü','rarity.item.7':'Kaçak','rarity.agent.3':'Seçkin','rarity.agent.4':'İstisnai','rarity.agent.5':'Üstün','rarity.agent.6':'Usta','slots.used':'{0} / 4 çıkartma','row.all':'Tümü','row.search':'Ara','row.remove':'Kaldır','row.remove_slot':'{0}. yuvayı temizle','row.remove_all':'Tümünü kaldır'}
trcode='private static readonly Dictionary<string,string> Tr = new(StringComparer.Ordinal) {\n'+',\n'.join('['+json.dumps(k,ensure_ascii=False)+'] = '+json.dumps(v,ensure_ascii=False)for k,v in tr.items())+'\n};\n'
patch('OsnetSkinMenu/Lang.cs','private static readonly Dictionary<string, string> En',trcode+'\nprivate static readonly Dictionary<string, string> En')
patch('OsnetSkins/OsnetSkins.cs','ModuleVersion => "1.1.9"','ModuleVersion => "0.1.0"')
patch('OsnetSkins/OsnetSkins.cs','public sealed class OsnetSkins :','public sealed partial class OsnetSkins :')
patch('OsnetSkins/Config.cs','public class SkinsConfig :','public partial class SkinsConfig :')
patch('OsnetSkins/Config.cs','[JsonPropertyName("database")]','[JsonIgnore]')
patch('OsnetSkins/OsnetSkins.cs','Config = config;','config.Database = OsnetSharedDatabase.Load(ModuleDirectory);\n        Config = config;')
dbhelper='''using System.Text.Json;
using System.Text.RegularExpressions;
namespace NAMESPACE;
internal static class OsnetSharedDatabase {
 internal static RESULT Load(string moduleDirectory) {
  var match=Regex.Match(moduleDirectory,@"/srv/cs2/instances/([1-9][0-9]{0,3})/game/");
  if(!match.Success)throw new InvalidOperationException("Server identity could not be verified.");
  string path=Path.GetFullPath(Path.Combine(moduleDirectory,"../../configs/plugins/CS2-SimpleAdmin/CS2-SimpleAdmin.json"));
  if(new FileInfo(path).Length>65536)throw new InvalidOperationException("Database configuration size invalid.");
  using var doc=JsonDocument.Parse(File.ReadAllBytes(path),new JsonDocumentOptions{CommentHandling=JsonCommentHandling.Skip,AllowTrailingCommas=true});
  var db=doc.RootElement.GetProperty("DatabaseConfig");
  string name=db.GetProperty("DatabaseName").GetString()??"";
  if(name!="osnet_s"+match.Groups[1].Value)throw new InvalidOperationException("Database belongs to another server. Clone credentials must be regenerated first.");
  return FACTORY;
 }
}
'''
(OUT/'OsnetSkins/OsnetSharedDatabase.cs').write_text(dbhelper.replace('NAMESPACE','OsnetSkins').replace('RESULT','DatabaseConfig').replace('FACTORY','new DatabaseConfig {Host="127.0.0.1",Port=3306,Name=name,User=db.GetProperty("DatabaseUser").GetString()??"",Password=db.GetProperty("DatabasePassword").GetString()??"",SslMode="Preferred"}'))
(OUT/'OsnetSkinMenuCore/OsnetSharedDatabase.cs').write_text(dbhelper.replace('NAMESPACE','OsnetSkinMenuCore').replace('RESULT','(string Name,string User,string Password)').replace('FACTORY','(name,db.GetProperty("DatabaseUser").GetString()??"",db.GetProperty("DatabasePassword").GetString()??"")'))
for prop in ['DatabaseHost','DatabasePort','DatabaseUser','DatabasePassword','DatabaseName']:
    patch('OsnetSkinMenuCore/Main.cs','[JsonPropertyName("'+prop+'")]','[JsonIgnore]')
patch('OsnetSkinMenuCore/Main.cs','Config = config;','var db=OsnetSharedDatabase.Load(ModuleDirectory);\n        config.DatabaseHost="127.0.0.1";config.DatabasePort=3306;config.DatabaseName=db.Name;config.DatabaseUser=db.User;config.DatabasePassword=db.Password;\n        Config = config;')
patch('OsnetSkins/OsnetSkins.cs','return !Links.Required || Links.IsLinked(player.SteamID);','return Config.Osnet.Enabled && (!Links.Required || Links.IsLinked(player.SteamID));')

# Module/rank gate is enforced by both menu entry and cosmetic application.
root=OUT/'OsnetSkins'
(root/'OsnetPolicy.cs').write_text('''using System.Text.Json;
using System.Text.Json.Serialization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
namespace OsnetSkins;
public partial class SkinsConfig {
 [JsonPropertyName("Osnet")] public OsnetOptions Osnet { get; set; } = new();
}
public sealed class OsnetOptions {
 public bool Enabled {get;set;} = false;
 public Dictionary<string,OsnetFeature> Features {get;set;} = new();
}
public sealed class OsnetFeature {
 public bool Enabled {get;set;} = true;
 public int MinimumRank {get;set;} = 0;
}
public sealed partial class OsnetSkins {
 public WeaponEntry FilterEntry(CCSPlayerController player,WeaponEntry value) => new() {
  Paint=value.Paint, Wear=Allowed(player,"wear")?value.Wear:0.000001f,
  Seed=Allowed(player,"seed")?value.Seed:0, NameTag=Allowed(player,"nametag")?value.NameTag:null,
  StatTrak=Allowed(player,"stattrak")?value.StatTrak:-1,
  Stickers=Allowed(player,"stickers")?value.Stickers:[], Charm=Allowed(player,"charms")?value.Charm:null
 };
 public bool Allowed(CCSPlayerController player,string feature) {
  if(!Config.Osnet.Enabled || !player.IsValid || player.IsBot)return false;
  if(!Config.Osnet.Features.TryGetValue(feature,out var rule))return false;
  if(!rule.Enabled)return false;
  int rank=0;
  bool Has(string flag)=>AdminManager.PlayerHasPermissions(player,flag);
  if(Has("@css/root")||Has("@osnet/owner"))rank=6;
  else if(Has("@osnet/superadmin")||Has("@css/permban"))rank=5;
  else if(Has("@osnet/admin")||Has("@css/generic"))rank=4;
  else if(Has("@osnet/gold"))rank=3;
  else if(Has("@osnet/silver"))rank=2;
  else if(Has("@osnet/vip")||Has("@css/vip"))rank=1;
  return rank>=rule.MinimumRank;
 }
 private void RegisterOsnetStatus() {
  AddCommand("osnet_skins_status","Hosting readiness probe",(caller,info)=>{
   if(caller is not null)return;
   info.ReplyToCommand("OSNET_SKINS_STATUS:"+JsonSerializer.Serialize(new{version="0.1.0",ready=Initialized&&!Stopping&&Config.Osnet.Enabled,catalog=Catalog.Loaded,native=EconAttributes.Available,menu_ready=NativeMenuProvider.Find(3,Logger) is not null,enabled=Config.Osnet.Enabled}));
  });
 }
}
''')
patch('OsnetSkins/OsnetSkins.cs','public override void Load(bool hotReload)\n\t{','public override void Load(bool hotReload)\n\t{\n        RegisterOsnetStatus();')
for key,method in [('skins','OpenSkins'),('knife','OpenKnife'),('gloves','OpenGloves'),('agents','OpenAgents'),('music','OpenMusic'),('pins','OpenPins')]:
    p=root/'Menus/SkinMenus.cs';s=p.read_text();pattern=r'public void '+method+r'\(CCSPlayerController player\) => ([^;]+);'
    s,n=re.subn(pattern,lambda m:'public void '+method+'(CCSPlayerController player) { if(plugin.Allowed(player,"'+key+'")) '+m.group(1)+'; }',s)
    assert n==1,(method,n);p.write_text(s)
patch('OsnetSkins/Game/WeaponApplier.cs','var isKnife = KnifeService.IsKnifeClass(weapon.DesignerName);','var isKnife = KnifeService.IsKnifeClass(weapon.DesignerName);\n        if(!plugin.Allowed(player,isKnife ? "knife" : "skins"))return;')
patch('OsnetSkins/Game/WeaponApplier.cs','side.Weapons.TryGetValue(defIndex, out var entry);','side.Weapons.TryGetValue(defIndex, out var entry);\n        if(entry is not null)entry=plugin.FilterEntry(player,entry);')
patch('OsnetSkins/Game/GloveService.cs','public void Apply(CCSPlayerController player, bool draw, bool refresh = true)\n\t{','public void Apply(CCSPlayerController player, bool draw, bool refresh = true)\n\t{\n        if(!plugin.Allowed(player,"gloves"))return;')
for signature,feature in [('public void ApplyAgent(CCSPlayerController player, bool applyGloves = false)','agents'),('public void ApplyMusic(CCSPlayerController player)','music'),('public void ApplyPin(CCSPlayerController player)','pins')]:
    patch('OsnetSkins/Game/ProfileService.cs',signature+'\n\t{',signature+'\n\t{\n        if(!plugin.Allowed(player,"'+feature+'"))return;')
patch('OsnetSkins/Commands.cs','public void RegisterLink()\n\t{','public void RegisterLink()\n\t{\n        // Discord integration is not part of the hosting skin menu.\n        return;\n')
# Keep upstream command rules and only add our readiness flag, never a global command listener.
for key in ['stickers','nametag','stattrak','wear','seed','gen','copy']:
    # Command gate is per command; skin/knife/glove application still checks its feature.
    method={'stickers':'OnStickers','nametag':'OnNameTag','stattrak':'OnStatTrak','wear':'OnWear','seed':'OnSeed','gen':'OnGen','copy':'OnCopy'}[key]
    p=root/'Commands.cs';s=p.read_text();pattern=r'(private void '+method+r'\(CCSPlayerController\? caller, CommandInfo info\)\s*\{)'
    s,n=re.subn(pattern,lambda m:m.group(1)+'\n        if(caller is not null && !plugin.Allowed(caller,"'+key+'"))return;',s);assert n==1,method;p.write_text(s)

# Prefix private SQL tables, including literals in raw SQL and interpolated query strings.
for p in root.rglob('*.cs'):
    if '/DB/'in str(p)or'/DiscordBot/Database/'in str(p):
        s=p.read_text();s=re.sub(r'(?<![a-zA-Z0-9_])ws_', 'osnet_skins_',s);p.write_text(s)

# No client files, tokens or database credentials are downloaded from an unpinned build.
provenance={'version':'0.1.0','upstreams':{'skin':'Staaar0/WeaponSkins@1425290ae3d29493171c70e9ed55f74e688c366e','core':'RRimmer/PanoramaMenuManagerCS2@279d2bbfac084ed3f2b8e286932a5d9b707b1fb0','menu':'Staaar0/PMM_WeaponSkins@8db43e369814ba48f2bb653ccf8ae9920f06fa92'}}
(OUT/'PROVENANCE.json').write_text(json.dumps(provenance,indent=2))
for key in ['core','skin','pmm']:shutil.copyfile(ROOT/'src'/key/'LICENSE',OUT/('LICENSE-'+key+'.txt'))
print('Adapted: 4 isolated projects; no original menu API, SQLite fallback or global ws listener.')
