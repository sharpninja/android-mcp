using System.IO.Compression;
using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;

namespace AndroidMcp.BuildTool.Publication;

/// <summary>Pure recipe validation and operation references. Never acquisition or extraction.</summary>
public sealed class RehydrationPlanner(ICanonicalJson canonical):IRehydrationSeedPlanner,IRehydrationSnapshotPlanner
{
    public RehydrationPlanResult Plan(RehydrationPlanRequest request) => PlanCore(request,out _,out _);
    public RehydrationSeedPlanResult PlanSeed(RehydrationPlanRequest request)
    {
        var baseline=PlanCore(request,out var seedProjection,out _);
        return new(baseline,seedProjection);
    }
    public RehydrationSnapshotPlanResult PlanSnapshot(RehydrationPlanRequest request)
    {
        var baseline=PlanCore(request,out _,out var snapshotProjection,captureSnapshot:true);
        return new(baseline,snapshotProjection);
    }
    private RehydrationPlanResult PlanCore(RehydrationPlanRequest request,out RehydrationSeedProjection? seedProjection,
        out RehydrationSnapshotProjection? snapshotProjection,bool captureSnapshot=false)
    {
        seedProjection=null;snapshotProjection=null;
        try
        {
            if(!Enum.IsDefined(request.Profile))Fail("INVALID_PROFILE","");
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
            var unique=new Dictionary<string,SourceProjectionState>(StringComparer.Ordinal);
            var seedSources=new Dictionary<int,RehydrationSeedSource>();
            var caches=new Dictionary<string,CacheProjectionState>(StringComparer.Ordinal);
            var projectedRows=new List<RehydrationSourceRow>();
            var snapshotRows=new List<RehydrationSnapshotRow>();
            var snapshots=new List<SnapshotRequirement>();
            var archives=new List<ArchiveRequirement>();
            for(var rowIndex=0;rowIndex<rows.Length;rowIndex++)
            {
                var row=rows[rowIndex];
                var logical=Text(row,"path");var local=Text(row,"local_path");Path(logical);Path(local);
                var sha=Text(row,"sha256");Pin(sha,local);
                long? length=row.TryGetProperty("size_bytes",out var size) && size.ValueKind!=JsonValueKind.Null ? size.GetInt64() : null;
                if(length<0)Fail("INVALID_LENGTH",local);
                var hasSource=unique.TryGetValue(local,out var old);
                if(hasSource && (old!.Pin.Sha256!=sha || old.Pin.SizeBytes is not null && length is not null && old.Pin.SizeBytes!=length))
                    Fail("CONFLICTING_SOURCE_ALIAS",local);
                CacheProjectionState? cacheState=null;
                if(row.TryGetProperty("metadata_cache",out var cache) && cache.ValueKind!=JsonValueKind.Null)
                {
                    var cachePath=Text(cache,"path");Path(cachePath);
                    var cacheSha=Text(cache,"sha256");var parserSha=Text(cache,"parser_sha256");Pin(cacheSha,cachePath);Pin(parserSha,cachePath);
                    var cacheSource=Text(cache,"source_sha256");Pin(cacheSource,cachePath);
                    if(cacheSource!=sha)Fail("CACHE_SOURCE_MISMATCH",cachePath);
                    var cacheKind=OptionalText(cache,"kind")??OptionalText(row,"kind");
                    if(cacheKind is not ("java-source" or "sdk-api-index"))cacheKind=null;
                    int? records=cache.TryGetProperty("records",out var count) && count.ValueKind!=JsonValueKind.Null?count.GetInt32():null;
                    if(records<0)Fail("INVALID_RECIPE",cachePath);
                    if(caches.TryGetValue(cachePath,out cacheState))
                    {
                        if(cacheState.Sha!=cacheSha || cacheState.Parser!=parserSha || cacheState.Source!=cacheSource ||
                            cacheState.Kind is not null && cacheKind is not null && cacheState.Kind!=cacheKind ||
                            cacheState.Count is not null && records is not null && cacheState.Count!=records)
                            Fail("CONFLICTING_CACHE_ALIAS",cachePath);
                        cacheState.Kind??=cacheKind;cacheState.Count??=records;
                    }
                    else
                    {
                        cacheState=new(caches.Count,cachePath,cacheSha,parserSha,cacheSource,cacheKind,records);
                        caches.Add(cachePath,cacheState);
                    }
                    cacheState.Rows.Add(rowIndex);
                }
                var transport=row.TryGetProperty("transport",out var t) && t.ValueKind==JsonValueKind.Object ? t : default;
                string? archiveMember=null,archivePin=null;
                var sourceUrl=Text(row,"url");var url=sourceUrl;
                if(transport.ValueKind==JsonValueKind.Object && (transport.TryGetProperty("archive_url",out _) || transport.TryGetProperty("archive_member",out _) || transport.TryGetProperty("archive_sha256",out _)))
                {
                    var archiveUrl=Text(transport,"archive_url");archiveMember=Text(transport,"archive_member");
                    if(string.IsNullOrWhiteSpace(archiveUrl) || string.IsNullOrWhiteSpace(archiveMember))Fail("INVALID_ARCHIVE_TRANSPORT",local);
                    Path(archiveMember);url=archiveUrl;
                    if(transport.TryGetProperty("archive_sha256",out var archiveHash) && archiveHash.ValueKind!=JsonValueKind.Null)
                    {archivePin=archiveHash.GetString()??"";Pin(archivePin,local);}
                }
                var uri=Endpoint(url,request.AllowFileFixtures,local);
                var sourceUri=Endpoint(sourceUrl,request.AllowFileFixtures,local);
                if(archiveMember is not null)archives.Add(new(logical,local,url,archiveMember,archivePin,sha));
                SourceArchivePin? archive=null;
                if(archiveMember is not null)
                {
                    var archiveSha1=OptionalText(transport,"archive_sha1");
                    if(archiveSha1 is not null)GitPin(archiveSha1,local);
                    var archiveLocal=OptionalText(transport,"archive_local_path");if(archiveLocal is not null)Path(archiveLocal);
                    long? archiveLength=transport.TryGetProperty("archive_size_bytes",out var archiveSize) && archiveSize.ValueKind!=JsonValueKind.Null?archiveSize.GetInt64():null;
                    if(archiveLength<0)Fail("INVALID_LENGTH",local);
                    var xml=OptionalText(transport,"repository_xml");
                    archive=new(uri,archiveMember,archivePin,archiveSha1,archiveLength,OptionalText(transport,"etag"),archiveLocal,request.Profile,
                        xml is null?default:System.Text.Encoding.UTF8.GetBytes(xml),OptionalText(transport,"platform_version")??OptionalText(row,"platform_api_version"),OptionalText(transport,"package_path"));
                }
                var gitBlob=OptionalText(row,"git_blob");if(gitBlob is not null)GitPin(gitBlob,local);
                var encoding=OptionalText(transport,"kind") is "gitiles-text" or "gitiles-base64"?SourceWireEncoding.GitilesText:SourceWireEncoding.Raw;
                SnapshotRequirement? snapshot=null;
                if(transport.ValueKind==JsonValueKind.Object && transport.TryGetProperty("immutable_url",out var immutable) && immutable.ValueKind==JsonValueKind.False)
                {
                    var bundle=transport.TryGetProperty("required_local_snapshot",out var bundlePath) && bundlePath.ValueKind!=JsonValueKind.Null ? bundlePath.GetString() : null;
                    if(bundle is not null)Path(bundle);
                    snapshot=new(logical,local,bundle,sha,length);snapshots.Add(snapshot);
                }
                var sourcePin=new SourcePin(local,sourceUri,OptionalText(row,"revision")??"",sha,length,gitBlob,encoding,snapshot?.BundlePath,archive,request.Profile);
                if(captureSnapshot)
                {
                    // Exact logical-row facts precede first/last physical pin
                    // selection. Nonstring status is ineligible, not invalid.
                    var statusPresent=row.TryGetProperty("status",out var statusValue);
                    var statusKind=statusValue.ValueKind;
                    var status=statusKind==JsonValueKind.String?statusValue.GetString():null;
                    SnapshotRequirement[] currentSnapshots=snapshot is {} rowSnapshot?[rowSnapshot]:[];
                    var rowSnapshots=Array.AsReadOnly(currentSnapshots);
                    var rowIndices=Array.AsReadOnly(new[]{rowIndex});
                    RehydrationCanonicalMetadataReference? metadataReference=null;
                    if(OptionalText(transport,"kind")=="bounded-sdk-zip-range-member")
                    {
                        var receiptPresent=transport.TryGetProperty("metadata_receipt",out var receiptValue);
                        var packagePresent=transport.TryGetProperty("package_path",out var packageValue);
                        var rowPlatformPresent=row.TryGetProperty("platform_api_version",out var rowPlatformValue);
                        var transportPlatformPresent=transport.TryGetProperty("platform_api_version",out var legacyPlatformValue);
                        var platform=rowPlatformValue.ValueKind==JsonValueKind.String?rowPlatformValue.GetString():null;
                        // Only the admitted nullable-string legacy domain is
                        // represented. Unsupported nonstrings stay unresolved;
                        // neither stringify nor substitute native platform_version.
                        if(string.IsNullOrEmpty(platform) && (rowPlatformValue.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null or JsonValueKind.String) &&
                            transportPlatformPresent && legacyPlatformValue.ValueKind==JsonValueKind.String)
                            platform=legacyPlatformValue.GetString();
                        // Preserve exact detached facts only for the type domain
                        // not represented by the existing nullable-string view.
                        // Acquisition alone interprets truth, precedence and text.
                        var nonstringRow=rowPlatformValue.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False or JsonValueKind.Array or JsonValueKind.Object;
                        var nullableOrEmptyRow=(rowPlatformValue.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null) ||
                            (rowPlatformValue.ValueKind==JsonValueKind.String && string.IsNullOrEmpty(rowPlatformValue.GetString()));
                        RehydrationLegacyPlatformFacts? legacyFacts=null;
                        if(nonstringRow || (nullableOrEmptyRow && transportPlatformPresent && legacyPlatformValue.ValueKind!=JsonValueKind.String))
                            legacyFacts=new(
                                new(rowPlatformPresent,rowPlatformValue.ValueKind,rowPlatformPresent?(JsonElement?)rowPlatformValue.Clone():null),
                                new(transportPlatformPresent,legacyPlatformValue.ValueKind,transportPlatformPresent?(JsonElement?)legacyPlatformValue.Clone():null));
                        metadataReference=new(receiptPresent,receiptValue.ValueKind,
                            receiptValue.ValueKind==JsonValueKind.String?receiptValue.GetString():null,platform,
                            packagePresent,packageValue.ValueKind,packageValue.ValueKind==JsonValueKind.String?packageValue.GetString():null,legacyFacts);
                    }
                    snapshotRows.Add(new(rowIndex,status,sourcePin,rowSnapshots,
                        SourcePrerequisites(sourcePin,rowSnapshots,rowIndices,request.Profile),statusPresent,statusKind,metadataReference));
                }
                SourceProjectionState state;
                if(hasSource)
                {
                    state=old!;
                    if(state.Pin.GitBlob is not null && gitBlob is not null && state.Pin.GitBlob!=gitBlob)Fail("CONFLICTING_SOURCE_ALIAS",local);
                    state.Pin=state.Pin with{SizeBytes=state.Pin.SizeBytes??length,GitBlob=state.Pin.GitBlob??gitBlob};
                }
                else{state=new(unique.Count,sourcePin);unique.Add(local,state);}
                // Keep all-row integrity evidence in Pin for conflict admission.
                // Generic rehydrate separately selects the exact last row.
                state.LastPin=sourcePin;state.LastSnapshot=snapshot;
                state.Rows.Add(rowIndex);if(snapshot is not null)state.Snapshots.Add(snapshot);
                // Capture the exact last logical row for Seed's typed projection.
                // Seed consumes typed values; OriginalRow is audit evidence only.
                var candidates=new List<RehydrationSeedCandidate>{new(0,RehydrationSeedCandidateKind.LocalPath,local)};
                if(snapshot?.BundlePath is {} seedBundle)candidates.Add(new(1,RehydrationSeedCandidateKind.RequiredLocalSnapshot,seedBundle));
                seedSources[state.Index]=new(state.Index,rowIndex,sourcePin,snapshot,Array.AsReadOnly(candidates.ToArray()));
                var context=new RehydrationRowContext(OptionalText(row,"repository"),OptionalText(row,"revision"),OptionalText(row,"source_surface"),OptionalText(row,"scope"),OptionalText(row,"license"),Preserve(row,"source_release_context"),
                    Array.AsReadOnly(Elements(row,"selection_origins")),Array.AsReadOnly(Strings(row,"selection_reasons")));
                projectedRows.Add(new(rowIndex,logical,OptionalText(row,"kind"),state.Index,cacheState?.Index,context,row.Clone()));
            }
            foreach(var collision in unique.Keys.Intersect(caches.Keys,StringComparer.Ordinal).Order(StringComparer.Ordinal))Fail("PATH_ROLE_CONFLICT",collision);
            var physicalSources=unique.Values.Select(s=>s.Freeze(request.Profile)).ToArray();
            long known=0;foreach(var item in physicalSources)known=checked(known+(item.Pin.SizeBytes??0));
            var unknown=physicalSources.Count(x=>x.Pin.SizeBytes is null);
            var projection=new RehydrationSourceProjection(release!,digest,request.Profile,Array.AsReadOnly(projectedRows.ToArray()),
                Array.AsReadOnly(physicalSources),
                Array.AsReadOnly(caches.Values.Select(c=>c.Freeze(digest)).ToArray()),
                Array.AsReadOnly(Enumerable.Range(0,Math.Min(request.Limit??unique.Count,unique.Count)).ToArray()),request.Resume);
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
            var baseline=new RehydrationPlanResult(new(release!,digest,rows.Length,unique.Count,known,unknown,snapshots,flow,
                new("rehydration-plan.json",manifest,"application/json"),false,false,false,caveats,archives,projection),[]);
            seedProjection=new(release!,digest,Array.AsReadOnly(seedSources.OrderBy(s=>s.Key).Select(s=>s.Value).ToArray()));
            if(captureSnapshot)snapshotProjection=new(release!,digest,request.Profile,Array.AsReadOnly(snapshotRows.ToArray()),request.Resume);
            return baseline;
        }
        catch(PlanningFailure e){return new(null,[new(e.Code,e.Path)]);}
        catch(OverflowException){return new(null,[new("INVALID_LENGTH","")]);}
        catch(Exception e) when(e is JsonException or FormatException or InvalidOperationException or KeyNotFoundException or InvalidDataException or ArgumentException)
        {return new(null,[new("INVALID_RECIPE","")]);}
    }
    private static string Text(JsonElement value,string key)=>value.TryGetProperty(key,out var field) && field.ValueKind==JsonValueKind.String ? field.GetString()??"" : "";
    private static string? OptionalText(JsonElement value,string key)
    {
        if(value.ValueKind!=JsonValueKind.Object || !value.TryGetProperty(key,out var field) || field.ValueKind==JsonValueKind.Null)return null;
        if(field.ValueKind!=JsonValueKind.String)Fail("INVALID_RECIPE",key);
        return field.GetString();
    }
    private static JsonElement[] Elements(JsonElement row,string key)
    {
        if(!row.TryGetProperty(key,out var value) || value.ValueKind==JsonValueKind.Null)return [];
        if(value.ValueKind!=JsonValueKind.Array)Fail("INVALID_RECIPE",key);
        return value.EnumerateArray().Select(x=>x.Clone()).ToArray();
    }
    private static string[] Strings(JsonElement row,string key)
    {
        if(!row.TryGetProperty(key,out var value) || value.ValueKind==JsonValueKind.Null)return [];
        if(value.ValueKind!=JsonValueKind.Array)Fail("INVALID_RECIPE",key);
        return value.EnumerateArray().Select(x=>x.GetString()??throw new FormatException()).ToArray();
    }
    private static Uri Endpoint(string value,bool allowFile,string path)
    {
        if(!Uri.TryCreate(value,UriKind.Absolute,out var uri) || uri.UserInfo.Length>0 ||
            (uri.Scheme!=Uri.UriSchemeHttps && !(allowFile && uri.Scheme==Uri.UriSchemeFile)))Fail("UNSUPPORTED_TRANSPORT",path);
        return uri!;
    }
    private static void GitPin(string value,string path)
    {if(value.Length!=40 || !value.All(c=>c is >= '0' and <= '9' or >= 'a' and <= 'f'))Fail("INVALID_GIT_BLOB",path);}
    private static IReadOnlyList<RehydrationPrerequisite> SourcePrerequisites(SourcePin pin,
        IReadOnlyList<SnapshotRequirement> snapshots,IReadOnlyList<int> indexes,AcquisitionCompatibilityProfile profile)
    {
        var prerequisites=new List<RehydrationPrerequisite>();
        void Missing(RehydrationPrerequisiteCode code)=>prerequisites.Add(new(code,indexes,pin.LocalPath,"Missing pinned prerequisite; exact existing target may be reused."));
        if(snapshots.Any(s=>s.BundlePath is null))Missing(RehydrationPrerequisiteCode.RequiredImmutableSnapshot);
        if(profile==AcquisitionCompatibilityProfile.CanonicalRestore && pin.Archive is {ArchiveSha256:null} archive)
        {
            if(string.IsNullOrWhiteSpace(archive.ETag))Missing(RehydrationPrerequisiteCode.CanonicalArchiveETag);
            if(string.IsNullOrWhiteSpace(archive.PlatformVersion))Missing(RehydrationPrerequisiteCode.CanonicalArchivePlatformVersion);
            if(archive.RepositoryXml.IsEmpty)Missing(RehydrationPrerequisiteCode.CanonicalArchiveRepositoryXml);
            if(archive.SizeBytes is null)Missing(RehydrationPrerequisiteCode.CanonicalArchiveSize);
        }
        return Array.AsReadOnly(prerequisites.ToArray());
    }
    private sealed class SourceProjectionState(int index,SourcePin pin)
    {
        public int Index { get; }=index;
        public SourcePin Pin { get; set; }=pin;
        public SourcePin LastPin { get; set; }=pin;
        public SnapshotRequirement? LastSnapshot { get; set; }
        public List<int> Rows { get; }=[];
        public List<SnapshotRequirement> Snapshots { get; }=[];
        public RehydrationPhysicalSource Freeze(AcquisitionCompatibilityProfile profile)
        {
            var generic=profile==AcquisitionCompatibilityProfile.GenericRehydrate;
            var selectedPin=generic?LastPin:Pin;
            SnapshotRequirement[] selectedSnapshots = generic
                ? (LastSnapshot is {} snapshot ? [snapshot] : []) : Snapshots.ToArray();
            var indexes=Array.AsReadOnly(Rows.ToArray());
            var prerequisites=SourcePrerequisites(selectedPin,selectedSnapshots,indexes,profile);
            var executable=!generic&&selectedSnapshots.Length>0?selectedPin with{RequiredLocalSnapshot=selectedSnapshots.Select(s=>s.BundlePath).FirstOrDefault(p=>p is not null)}:selectedPin;
            return new(Index,executable,indexes,Array.AsReadOnly(selectedSnapshots),prerequisites);
        }
    }
    private sealed class CacheProjectionState(int index,string path,string sha,string parser,string source,string? kind,int? count)
    {
        public int Index { get; }=index;
        public string Path { get; }=path;
        public string Sha { get; }=sha;
        public string Parser { get; }=parser;
        public string Source { get; }=source;
        public string? Kind { get; set; }=kind;
        public int? Count { get; set; }=count;
        public List<int> Rows { get; }=[];
        public RehydrationCacheAlias Freeze(string recipe)
        {
            var indexes=Array.AsReadOnly(Rows.ToArray());var prerequisites=new List<RehydrationPrerequisite>();
            void Missing(RehydrationPrerequisiteCode code)=>prerequisites.Add(new(code,indexes,Path,"Missing pinned prerequisite; exact existing target may be reused."));
            if(Kind is null)Missing(RehydrationPrerequisiteCode.HistoricalCacheKind);
            if(Count is null)Missing(RehydrationPrerequisiteCode.HistoricalCacheRecordCount);
            HistoricalCachePin? pin=Kind is not null && Count is not null?new(Path,Kind,Sha,Parser,Count.Value,Source,recipe):null;
            return new(Index,Path,pin,indexes,Array.AsReadOnly(prerequisites.ToArray()));
        }
    }
    private static JsonElement? Preserve(JsonElement value,string key)=>value.TryGetProperty(key,out var field)?field.Clone():null;
    private static void Path(string value){if(!PublicationNames.Relative(value))Fail("UNSAFE_PATH",value);}
    private static void Pin(string value,string path){if(!PublicationNames.Pin(value))Fail("INVALID_PIN",path);}
    private static void Fail(string code,string path)=>throw new PlanningFailure(code,path);
    private sealed class PlanningFailure(string code,string path):Exception(code)
    {public string Code { get; }=code;public string Path { get; }=path;}
}
