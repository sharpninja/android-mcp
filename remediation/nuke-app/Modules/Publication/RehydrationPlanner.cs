using System.IO.Compression;
using System.Text.Json;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Publication;

/// <summary>Pure recipe validation and operation references. Never acquisition or extraction.</summary>
public sealed class RehydrationPlanner(ICanonicalJson canonical):IRehydrationPlanner
{
    public RehydrationPlanResult Plan(RehydrationPlanRequest request)
    {
        try
        {
            if(request.Limit<0)Fail("INVALID_LIMIT","");
            using var lockDocument=JsonDocument.Parse(canonical.Serialize(request.Lock.Bytes));
            var pin=lockDocument.RootElement;
            var digest=ContentHash.Sha256(request.Recipe.Bytes.Span);
            if(pin.GetProperty("sha256").GetString()!=digest)Fail("RECIPE_PIN_MISMATCH",request.Recipe.RelativePath);
            if(pin.GetProperty("recipe_file").GetString()!=request.Recipe.RelativePath)Fail("LOCK_PATH_MISMATCH",request.Recipe.RelativePath);
            Path(request.Recipe.RelativePath);Path(request.Lock.RelativePath);
            var recipeBytes=request.Recipe.Bytes.ToArray();
            if(request.Recipe.RelativePath.EndsWith(".gz",StringComparison.Ordinal))
            {
                using var compressed=new MemoryStream(recipeBytes,false);
                using var gzip=new GZipStream(compressed,CompressionMode.Decompress);
                using var expanded=new MemoryStream();gzip.CopyTo(expanded);recipeBytes=expanded.ToArray();
            }
            using var recipeDocument=JsonDocument.Parse(canonical.Serialize(recipeBytes));
            var recipe=recipeDocument.RootElement;
            if(recipe.GetProperty("recipe_version").GetInt32()!=1)Fail("UNSUPPORTED_RECIPE","");
            var release=recipe.GetProperty("release").GetString();
            if(string.IsNullOrWhiteSpace(release))Fail("INVALID_RELEASE","");
            if(request.ExpectedRelease is not null && request.ExpectedRelease!=release)Fail("RELEASE_MISMATCH","");
            var rows=recipe.GetProperty("inventory").GetProperty("sources").EnumerateArray().ToArray();
            if(rows.Length==0)Fail("EMPTY_RECIPE","");
            var producer=recipe.GetProperty("producer");Path(Text(producer,"path"));
            var producerFiles=producer.GetProperty("files").EnumerateObject().ToArray();
            if(producerFiles.Length==0)Fail("EMPTY_PRODUCER","");
            foreach(var file in producerFiles){Path(file.Name);Pin(file.Value.GetString()??"",file.Name);}
            if(pin.GetProperty("source_rows").GetInt32()!=rows.Length || pin.GetProperty("producer_files").GetInt32()!=producerFiles.Length)
                Fail("LOCK_COUNT_MISMATCH","");
            var unique=new Dictionary<string,(string Sha,long? Length)>(StringComparer.Ordinal);
            var caches=new Dictionary<string,(string Sha,string Parser,string Source)>(StringComparer.Ordinal);
            var snapshots=new List<SnapshotRequirement>();
            var archives=new List<ArchiveRequirement>();
            foreach(var row in rows)
            {
                var logical=Text(row,"path");var local=Text(row,"local_path");Path(logical);Path(local);
                var sha=Text(row,"sha256");Pin(sha,local);
                long? length=row.TryGetProperty("size_bytes",out var size) && size.ValueKind!=JsonValueKind.Null ? size.GetInt64() : null;
                if(length<0)Fail("INVALID_LENGTH",local);
                if(unique.TryGetValue(local,out var old) && (old.Sha!=sha || old.Length is not null && length is not null && old.Length!=length))
                    Fail("CONFLICTING_SOURCE_ALIAS",local);
                unique[local]=(sha,length??(unique.TryGetValue(local,out old)?old.Length:null));
                if(row.TryGetProperty("metadata_cache",out var cache) && cache.ValueKind!=JsonValueKind.Null)
                {
                    var cachePath=Text(cache,"path");Path(cachePath);
                    var cacheSha=Text(cache,"sha256");var parserSha=Text(cache,"parser_sha256");Pin(cacheSha,cachePath);Pin(parserSha,cachePath);
                    var cacheSource=Text(cache,"source_sha256");Pin(cacheSource,cachePath);
                    if(cacheSource!=sha)Fail("CACHE_SOURCE_MISMATCH",cachePath);
                    var identity=(cacheSha,parserSha,cacheSource);
                    if(caches.TryGetValue(cachePath,out var previous) && previous!=identity)Fail("CONFLICTING_CACHE_ALIAS",cachePath);
                    caches[cachePath]=identity;
                }
                var transport=row.TryGetProperty("transport",out var t) && t.ValueKind==JsonValueKind.Object ? t : default;
                string? archiveMember=null,archivePin=null;
                var url=Text(row,"url");
                if(transport.ValueKind==JsonValueKind.Object && (transport.TryGetProperty("archive_url",out _) || transport.TryGetProperty("archive_member",out _) || transport.TryGetProperty("archive_sha256",out _)))
                {
                    var archiveUrl=Text(transport,"archive_url");archiveMember=Text(transport,"archive_member");
                    if(string.IsNullOrWhiteSpace(archiveUrl) || string.IsNullOrWhiteSpace(archiveMember))Fail("INVALID_ARCHIVE_TRANSPORT",local);
                    Path(archiveMember);url=archiveUrl;
                    if(transport.TryGetProperty("archive_sha256",out var archiveHash) && archiveHash.ValueKind!=JsonValueKind.Null)
                    {archivePin=archiveHash.GetString()??"";Pin(archivePin,local);}
                }
                if(!Uri.TryCreate(url,UriKind.Absolute,out var uri) || uri.UserInfo.Length>0 ||
                    (uri.Scheme!=Uri.UriSchemeHttps && !(request.AllowFileFixtures && uri.Scheme==Uri.UriSchemeFile)))Fail("UNSUPPORTED_TRANSPORT",local);
                if(archiveMember is not null)archives.Add(new(logical,local,url,archiveMember,archivePin,sha));
                if(transport.ValueKind==JsonValueKind.Object && transport.TryGetProperty("immutable_url",out var immutable) && immutable.ValueKind==JsonValueKind.False)
                {
                    var bundle=transport.TryGetProperty("required_local_snapshot",out var bundlePath) && bundlePath.ValueKind!=JsonValueKind.Null ? bundlePath.GetString() : null;
                    if(bundle is not null)Path(bundle);
                    snapshots.Add(new(logical,local,bundle,sha,length));
                }
            }
            foreach(var collision in unique.Keys.Intersect(caches.Keys,StringComparer.Ordinal).Order(StringComparer.Ordinal))Fail("PATH_ROLE_CONFLICT",collision);
            long known=0;foreach(var item in unique.Values)known=checked(known+(item.Length??0));
            var unknown=unique.Count(x=>x.Value.Length is null);
            var flow=new OperationKey[]{new(Category.Sources,Operation.Fetch),new(Category.Sources,Operation.Extract),new(Category.Catalog,Operation.Build),new(Category.Validate,Operation.Catalog),new(Category.Catalog,Operation.Export)};
            var manifest=canonical.Serialize(JsonSerializer.SerializeToUtf8Bytes(new
            {
                schema_version=1,release,recipe_sha256=digest,recipe_file=request.Recipe.RelativePath,
                source_rows=rows.Length,unique_source_paths=unique.Count,selected_paths=Math.Min(request.Limit??unique.Count,unique.Count),
                known_source_bytes=known,unknown_size_paths=unknown,resume=request.Resume,
                network=false,complete=false,historical_byte_identity_claimed=false,
                required_snapshots=snapshots.Select(s=>new{source_path=s.SourcePath,local_path=s.LocalPath,bundle_path=s.BundlePath,sha256=s.Sha256,length=s.Length}),
                archive_members=archives.Select(a=>new{source_path=a.SourcePath,local_path=a.LocalPath,url=a.Url,member=a.Member,archive_sha256=a.ArchiveSha256,member_sha256=a.MemberSha256,archive_verified=false}),
                operations=flow.Select(x=>new{category=x.Category.ToString(),operation=x.Operation.ToString()}),
                expected=Preserve(recipe,"expected"),environment=Preserve(recipe,"environment"),provenance=Preserve(recipe,"provenance"),limits=Preserve(recipe,"limits"),producer=producer.Clone()
            }));
            var caveats=new List<string>{"Selected corpus only; expected counts are not observed validation.","Owner-supplied mutable snapshots and historical byte identity remain explicit limitations.","Legacy environment and producer pins are evidence; they do not authorize legacy execution.","Operation references are plans, not a claim that root execution adapters are ready."};
            if(archives.Count>0)caveats.Add("Archive bytes are not verified by planning.");
            if(archives.Any(a=>a.ArchiveSha256 is null))caveats.Add("Container SHA256 is unavailable for some members; member identity remains pinned.");
            return new(new(release!,digest,rows.Length,unique.Count,known,unknown,snapshots,flow,
                new("rehydration-plan.json",manifest,"application/json"),false,false,false,caveats,archives),[]);
        }
        catch(PlanningFailure e){return new(null,[new(e.Code,e.Path)]);}
        catch(OverflowException){return new(null,[new("INVALID_LENGTH","")]);}
        catch(Exception e) when(e is JsonException or FormatException or InvalidOperationException or KeyNotFoundException or InvalidDataException or ArgumentException)
        {return new(null,[new("INVALID_RECIPE","")]);}
    }
    private static string Text(JsonElement value,string key)=>value.TryGetProperty(key,out var field) && field.ValueKind==JsonValueKind.String ? field.GetString()??"" : "";
    private static JsonElement? Preserve(JsonElement value,string key)=>value.TryGetProperty(key,out var field)?field.Clone():null;
    private static void Path(string value){if(!PublicationNames.Relative(value))Fail("UNSAFE_PATH",value);}
    private static void Pin(string value,string path){if(!PublicationNames.Pin(value))Fail("INVALID_PIN",path);}
    private static void Fail(string code,string path)=>throw new PlanningFailure(code,path);
    private sealed class PlanningFailure(string code,string path):Exception(code)
    {public string Code { get; }=code;public string Path { get; }=path;}
}
