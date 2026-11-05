using System.Text.Json;
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
   info.ReplyToCommand("OSNET_SKINS_STATUS:"+JsonSerializer.Serialize(new{version="0.3.1",ready=Initialized&&!Stopping&&Config.Osnet.Enabled,catalog=Catalog.Loaded,native=EconAttributes.Available,menu_ready=NativeMenuProvider.Find(3,Logger) is not null,enabled=Config.Osnet.Enabled}));
  });
 }
}
