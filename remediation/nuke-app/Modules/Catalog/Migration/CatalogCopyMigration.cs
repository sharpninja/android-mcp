using System.Text;
using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using Microsoft.Data.Sqlite;

namespace AndroidMcp.BuildTool.Catalog.Migration;

/// <summary>Checked offline v2 copy. Shared SQLite/schema and create-new artifact services own storage.</summary>
public sealed class CatalogCopyMigration(string catalogRoot,Func<string,IStreamingArtifactReader> readers,Func<string,bool,CatalogDatabase>? sourceDatabases=null,
    Func<string,ICreateNewStreamingArtifactStore>? outputStores=null):ICatalogCopyMigration
{
    private static string Quote(string value)=>"\""+value.Replace("\"","\"\"",StringComparison.Ordinal)+"\"";
    private static FileArtifactStore Store(string root)=>new(root,bytes=>ContentHash.Sha256(bytes.Span));
    private static StoredArtifact Describe(IArtifactReadSession session,string name)
    {
        var stream=session.Content;
        Check(stream.CanRead&&stream.CanSeek,"Admitted migration input requires a readable seekable stream");
        Check(session.Opened.RelativePath==name&&session.Opened.Native.Kind==NativeArtifactKind.RegularFile&&session.Opened.Native.Length>=0&&stream.Length==session.Opened.Native.Length,"Admitted migration input length/identity differs");
        stream.Position=0;var hash=ContentHash.Sha256(stream);
        Check(stream.Position==session.Opened.Native.Length&&stream.Length==session.Opened.Native.Length,"Admitted migration input length changed during hashing");
        return new(name,hash,session.Opened.Native.Length,"application/vnd.sqlite3");
    }
    private static async Task Recheck(IArtifactReadSession session,StoredArtifact pin,string message,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();Check(await session.ObserveCurrentPathAsync(token)==session.Opened,message);
        var current=Describe(session,pin.RelativePath);Check(current==pin,message);
        Check(await session.ObserveCurrentPathAsync(token)==session.Opened,message);token.ThrowIfCancellationRequested();
    }
    private static object? Scalar(CatalogDatabase database,string sql,SqliteTransaction? transaction=null)
    {using var command=database.Command(sql,transaction);return command.ExecuteScalar();}
    private static void Execute(CatalogDatabase database,string sql,SqliteTransaction? transaction=null,params (string Name,object? Value)[] parameters)
    {using var command=database.Command(sql,transaction,parameters);command.ExecuteNonQuery();}
    private static List<Dictionary<string,object?>> Rows(CatalogDatabase database,string sql,SqliteTransaction? transaction=null)
    {
        using var command=database.Command(sql,transaction);using var reader=command.ExecuteReader();var rows=new List<Dictionary<string,object?>>();
        while(reader.Read())
        {
            var row=new Dictionary<string,object?>(StringComparer.Ordinal);
            for(var index=0;index<reader.FieldCount;index++)row.Add(reader.GetName(index),reader.IsDBNull(index)?null:reader.GetValue(index));
            rows.Add(row);
        }
        return rows;
    }
    private static long Count(CatalogDatabase database,string table,SqliteTransaction? transaction=null)=>(long)Scalar(database,"SELECT count(*) FROM "+Quote(table),transaction)!;
    private static void Check(bool value,string message){if(!value)throw new InvalidDataException(message);}
    private static bool Valid(CatalogDatabase database,SqliteTransaction? transaction=null)=>
        (string?)Scalar(database,"PRAGMA integrity_check",transaction)=="ok"&&Rows(database,"PRAGMA foreign_key_check",transaction).Count==0;
    private static void Insert(CatalogDatabase database,string table,Dictionary<string,object?> row,SqliteTransaction transaction)
    {
        var parameters=row.Values.Select((value,index)=>("$p"+index,value)).ToArray();
        Execute(database,"INSERT INTO "+Quote(table)+"("+string.Join(',',row.Keys.Select(Quote))+") VALUES("+string.Join(',',parameters.Select(value=>value.Item1))+")",transaction,parameters);
    }
    public async Task<CatalogMigrationResult> MigrateAsync(string source,string destination,CancellationToken cancellationToken=default)
    {
        var sourcePath=Path.GetFullPath(source);var targetPath=Path.GetFullPath(destination);var stagePath=targetPath+".staging";
        var outputRoot=Path.GetDirectoryName(targetPath)!;var sourceRoot=Path.GetDirectoryName(sourcePath)!;
        var sourceName=Path.GetFileName(sourcePath);var targetName=Path.GetFileName(targetPath);var stageName=Path.GetFileName(stagePath);
        sourcePath=ArtifactPaths.Resolve(sourceRoot,sourceName);targetPath=ArtifactPaths.Resolve(outputRoot,targetName);stagePath=ArtifactPaths.Resolve(outputRoot,stageName);
        Check(!Path.Exists(targetPath)&&!Path.Exists(stagePath),"Destination must be new");cancellationToken.ThrowIfCancellationRequested();
        var sourceReader=readers(sourceRoot);var outputReader=readers(outputRoot);
        await using var sourceSession=await sourceReader.OpenReadAsync(sourceName,cancellationToken);
        var sourcePin=Describe(sourceSession,sourceName);
        Check(await sourceSession.ObserveCurrentPathAsync(cancellationToken)==sourceSession.Opened,"Original source changed during admission");
        var targetPins=new List<StoredArtifact>();
        using(var old=(sourceDatabases??CatalogDatabase.OpenExisting)(sourcePath,true))
        {
            Check((long)Scalar(old,"PRAGMA user_version")! ==2,"Only storage user_version=2 can migrate to v3");
            Check(Valid(old),"Source integrity/FK failure");
            // Original profile owns the sorted script list. Never substitute schema.sql or a different profile's DDL.
            var scripts=new List<string>();var contracts=Store(catalogRoot);var directory=ArtifactPaths.Resolve(catalogRoot,"migrations");
            foreach(var file in Directory.EnumerateFiles(directory,"*.sql").Order(StringComparer.Ordinal))
            {
                var relative="migrations/"+Path.GetFileName(file);var bytes=await contracts.ReadAsync(relative,cancellationToken);
                targetPins.Add(new(relative,ContentHash.Sha256(bytes.Span),bytes.Length,"text/plain"));
                using var stream=new MemoryStream(bytes.ToArray(),false);using var reader=new StreamReader(stream,new UTF8Encoding(false,true),true);scripts.Add(reader.ReadToEnd());
            }
            using var next=CatalogDatabase.CreatePrivateFile(stagePath,scripts);
            Execute(next,"PRAGMA defer_foreign_keys=ON");using var transaction=next.Connection.BeginTransaction(deferred:false);
            try
            {
                var states=new Dictionary<string,string>(StringComparer.Ordinal);var active=new List<Dictionary<string,object?>>();var overlays=new List<Dictionary<string,object?>>();
                var tables=Rows(old,"SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'").Select(row=>(string)row["name"]!).ToArray();
                foreach(var table in tables)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if(table=="catalog_view")continue;
                    if(table=="active_registry"){active=Rows(old,"SELECT * FROM active_registry");continue;}
                    if(table=="overlays"){overlays=Rows(old,"SELECT * FROM overlays");continue;}
                    if(table=="effective_entries")
                    {
                        var selected=Scalar(old,"SELECT registry_id FROM active_registry WHERE singleton=1");var entries=Rows(old,"SELECT entry_id,document_json,record_sha256 FROM effective_entries");
                        Check(selected is not null||entries.Count==0,"No active registry for legacy effective entries");
                        foreach(var row in entries)Insert(next,"registry_entries",new(){{"registry_id",selected},{"entry_id",row["entry_id"]},{"document_json",row["document_json"]},{"record_sha256",row["record_sha256"]}},transaction);
                        Check(Count(next,"registry_entries",transaction)==entries.Count,"Entry count mismatch");continue;
                    }
                    foreach(var row in Rows(old,"SELECT * FROM "+Quote(table)))
                    {
                        if(table is "registries" or "import_jobs"&&row.Remove("status",out var state))row.Add("state",state);
                        if(table=="registries"){states.Add((string)row["registry_id"]!,(string)row["state"]!);row["state"]="staged";}
                        if(table=="permission_relations")
                        {using var expression=JsonDocument.Parse((string)row["expression_json"]!);row["logic"]=expression.RootElement.GetProperty("op").GetString();}
                        if(table=="catalog_meta"&&(string)row["key"]! =="schema_version")row["value"]="1.2.0";
                        Insert(next,table=="import_jobs"?"legacy_import_jobs":table,row,transaction);
                    }
                    Check(Count(next,table=="import_jobs"?"legacy_import_jobs":table,transaction)==Count(old,table),"Count mismatch "+table);
                }
                foreach(var (key,state) in states.OrderBy(pair=>pair.Value=="published"))
                {
                    string[] transitions=state switch{"staged"=>[],"rejected"=>["rejected"],"validated"=>["validated"],"published"=>["validated","published"],"retired"=>["validated","published","retired"],_=>throw new InvalidDataException("Unknown original registry state: "+state)};
                    foreach(var transition in transitions)Execute(next,"UPDATE registries SET state=$state WHERE registry_id=$id",transaction,("$state",transition),("$id",key));
                }
                foreach(var row in active)Insert(next,"active_registry",new(){{"singleton",row["singleton"]},{"registry_id",row["registry_id"]}},transaction);
                Check(Count(next,"active_registry",transaction)==active.Count,"Active pointer count mismatch");
                foreach(var row in overlays)Insert(next,"overlays",row,transaction);
                Check(Count(next,"overlays",transaction)==overlays.Count,"Overlay count mismatch");
                Execute(next,"INSERT OR REPLACE INTO catalog_meta VALUES('rebuilt_with_corrected_extractor','false')",transaction);
                Execute(next,"UPDATE catalog_view SET registry_generation=(SELECT registry_version FROM registries WHERE registry_id=(SELECT registry_id FROM active_registry WHERE singleton=1)) WHERE EXISTS(SELECT 1 FROM active_registry)",transaction);
                Check(Valid(next,transaction),"Migrated integrity/FK failure");transaction.Commit();
            }
            catch{transaction.Rollback();throw;}
        }
        // Both SQLite handles are closed before immutable publication. Retain original and stage, including failure evidence.
        await Recheck(sourceSession,sourcePin,"Original source changed before publication",cancellationToken);
        Check(!Path.Exists(targetPath)&&await outputReader.InspectAsync(targetName,cancellationToken) is null,"Destination must be new");
        await using var stageSession=await outputReader.OpenReadAsync(stageName,cancellationToken);
        var stagePin=Describe(stageSession,stageName);
        Check(await stageSession.ObserveCurrentPathAsync(cancellationToken)==stageSession.Opened,"Retained stage changed during admission");
        await Recheck(stageSession,stagePin,"Retained stage changed before publication",cancellationToken);
        var output=outputStores?.Invoke(outputRoot)??Store(outputRoot);
        var written=await output.WriteNewStreamAsync(targetName,"application/vnd.sqlite3",async(stream,token)=>
        {
            stageSession.Content.Position=0;
            await stageSession.Content.CopyToAsync(stream,81920,token);
            Check(stageSession.Content.Position==stagePin.Length,"Retained stage length changed during copy");
            await Recheck(stageSession,stagePin,"Retained stage changed during publication",token);
            await Recheck(sourceSession,sourcePin,"Original source changed during publication",token);
        },cancellationToken);
        Check(written.Sha256==stagePin.Sha256&&written.Length==stagePin.Length,"Copied migration output differs from verified stage");
        return new(sourcePin,written,stagePin,targetPins);
    }
}
