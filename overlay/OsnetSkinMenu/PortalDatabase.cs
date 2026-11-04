using System.Text.Json;
using System.Text.RegularExpressions;
using MySqlConnector;

namespace OsnetSkinMenu;

internal sealed record PortalRank(string Id,string Name,int Points,int Kills,int Deaths,int Seconds);
internal sealed record PortalStanding(PortalRank Row,int Place);
internal sealed class PortalDatabase
{
 private readonly string connection;
 private readonly SemaphoreSlim writes=new(1,1);
 public PortalDatabase(string module) {
  var match=Regex.Match(module,@"/srv/cs2/instances/([1-9][0-9]{0,3})/game/");
  if(!match.Success)throw new InvalidDataException("Server identity could not be verified");
  string path=Path.GetFullPath(Path.Combine(module,"../../configs/plugins/CS2-SimpleAdmin/CS2-SimpleAdmin.json"));
  if(new FileInfo(path).Length>65536)throw new InvalidDataException("SQL settings size invalid");
  using var doc=JsonDocument.Parse(File.ReadAllBytes(path),new JsonDocumentOptions{AllowTrailingCommas=true,CommentHandling=JsonCommentHandling.Skip});
  var db=doc.RootElement.GetProperty("DatabaseConfig");
  string name=db.GetProperty("DatabaseName").GetString()??"";
  if(name!="osnet_s"+match.Groups[1].Value)throw new InvalidDataException("Target SQL database belongs to another server");
  var b=new MySqlConnectionStringBuilder {Server="127.0.0.1",Port=3306,Database=name,UserID=db.GetProperty("DatabaseUser").GetString(),Password=db.GetProperty("DatabasePassword").GetString(),Pooling=true,MinimumPoolSize=0,MaximumPoolSize=4,ConnectionTimeout=3,DefaultCommandTimeout=5};
  connection=b.ConnectionString;
 }
 private async Task<MySqlConnection> Open(){var c=new MySqlConnection(connection);try{await c.OpenAsync();return c;}catch{await c.DisposeAsync();throw;}}
 public async Task Init(){await using var c=await Open();await using var q=c.CreateCommand();q.CommandText="CREATE TABLE IF NOT EXISTS osnet_menu_preferences (steam_id VARCHAR(20) NOT NULL PRIMARY KEY, show_hint BOOLEAN NOT NULL DEFAULT 1, hold_e BOOLEAN NOT NULL DEFAULT 1, show_on_join BOOLEAN NOT NULL DEFAULT 1, hold_seconds DECIMAL(3,1) NOT NULL DEFAULT 0.7, save_seq BIGINT NOT NULL DEFAULT 0) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4";await q.ExecuteNonQueryAsync();}
 public async Task<PortalPrefs?> Load(string steam) {
  await using var c=await Open();await using var q=c.CreateCommand();q.CommandText="SELECT show_hint,hold_e,show_on_join,hold_seconds FROM osnet_menu_preferences WHERE steam_id=@id";q.Parameters.AddWithValue("@id",steam);
  await using var r=await q.ExecuteReaderAsync();return await r.ReadAsync()?new(){show_hint=r.GetBoolean(0),hold_e=r.GetBoolean(1),show_on_join=r.GetBoolean(2),hold_seconds=Math.Clamp(r.GetDouble(3),.3,2)}:null;
 }
 public async Task Save(string steam,PortalPrefs p,long stamp) {
  // Versioned writes keep an older async request from replacing a newer preference.
  await writes.WaitAsync();try{
   await using var c=await Open();await using var q=c.CreateCommand();q.CommandText="INSERT INTO osnet_menu_preferences (steam_id,show_hint,hold_e,show_on_join,hold_seconds,save_seq) VALUES (@id,@hint,@hold,@join,@delay,@seq) ON DUPLICATE KEY UPDATE show_hint=IF(@seq>=save_seq,@hint,show_hint),hold_e=IF(@seq>=save_seq,@hold,hold_e),show_on_join=IF(@seq>=save_seq,@join,show_on_join),hold_seconds=IF(@seq>=save_seq,@delay,hold_seconds),save_seq=GREATEST(save_seq,@seq)";
   q.Parameters.AddWithValue("@seq",stamp);q.Parameters.AddWithValue("@id",steam);q.Parameters.AddWithValue("@hint",p.show_hint);q.Parameters.AddWithValue("@hold",p.hold_e);q.Parameters.AddWithValue("@join",p.show_on_join);q.Parameters.AddWithValue("@delay",p.hold_seconds);await q.ExecuteNonQueryAsync();
  }finally{writes.Release();}
 }
 private const string From=" FROM osnet_stats_k4ranks r LEFT JOIN osnet_stats_k4stats s ON s.steam_id=r.steam_id LEFT JOIN osnet_stats_k4times t ON t.steam_id=r.steam_id ";
 private const string Columns="r.steam_id,r.name,r.points,COALESCE(s.kills,0),COALESCE(s.deaths,0),COALESCE(t.`all`,0)";
 private static PortalRank Row(MySqlDataReader r)=>new(r.GetString(0),r.GetString(1),r.GetInt32(2),r.GetInt32(3),r.GetInt32(4),r.GetInt32(5));
 public async Task<List<PortalRank>> Top(){await using var c=await Open();await using var q=c.CreateCommand();q.CommandText="SELECT "+Columns+From+"ORDER BY r.points DESC,r.steam_id ASC LIMIT 15";await using var r=await q.ExecuteReaderAsync();List<PortalRank> rows=[];while(await r.ReadAsync())rows.Add(Row(r));return rows;}
 public async Task<PortalStanding?> Self(string steam){
  await using var c=await Open();PortalRank? row=null;
  await using(var q=c.CreateCommand()){q.CommandText="SELECT "+Columns+From+"WHERE r.steam_id=@id";q.Parameters.AddWithValue("@id",steam);await using var r=await q.ExecuteReaderAsync();if(await r.ReadAsync())row=Row(r);}
  if(row==null)return null;
  await using var rank=c.CreateCommand();rank.CommandText="SELECT COUNT(*)+1 FROM osnet_stats_k4ranks WHERE points>@points OR (points=@points AND steam_id<@id)";rank.Parameters.AddWithValue("@points",row.Points);rank.Parameters.AddWithValue("@id",steam);return new(row,Convert.ToInt32(await rank.ExecuteScalarAsync()));
 }
}
