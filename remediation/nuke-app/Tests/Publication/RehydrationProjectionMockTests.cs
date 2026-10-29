using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Publication;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Publication;

public sealed record ProjectionCase(string Id, bool GZip=false, int? Limit=null,
    AcquisitionCompatibilityProfile Profile=AcquisitionCompatibilityProfile.CanonicalRestore,
    bool Gitiles=false, bool Bundle=false, bool Duplicate=false, bool MissingCacheCount=false,
    string? Error=null)
{ public override string ToString()=>Id; }

public static class RehydrationProjectionCases
{
    public static readonly string A=new('a',64), B=new('b',64), C=new('c',64), D=new('d',64);
    public static IEnumerable<object[]> Cases => new ProjectionCase[]
    {
        new("ROW-ORDER-ALIASES-AUDIT"), new("GZIP",GZip:true), new("LIMIT-ONE",Limit:1), new("LIMIT-ZERO",Limit:0),
        new("EXPLICIT-GENERIC",Profile:AcquisitionCompatibilityProfile.GenericRehydrate),
        new("GITILES",Gitiles:true), new("BUNDLED-SNAPSHOT",Bundle:true), new("EXACT-DUPLICATE",Duplicate:true),
        new("MISSING-CACHE-COUNT",MissingCacheCount:true),
        new("INVALID-PROFILE",Profile:(AcquisitionCompatibilityProfile)(-1),Error:"INVALID_PROFILE"),
        new("BAD-GIT-PIN",Error:"INVALID_GIT_BLOB"), new("CONFLICTING-CACHE-COUNT",Error:"CONFLICTING_CACHE_ALIAS")
    }.Select(c=>new object[]{c});

    public static RehydrationPlanRequest Request(ProjectionCase c)=>RehydrationContractCases.Request(root=>
    {
        JsonObject Cache(string path,string source,string kind,int count)=>new(){["path"]=path,["sha256"]=C,["parser_sha256"]=D,["source_sha256"]=source,["records"]=count,["kind"]=kind};
        JsonObject Row(string path,string local,string sha,string kind,string revision)=>new()
        { ["path"]=path,["local_path"]=local,["sha256"]=sha,["size_bytes"]=3,["url"]="https://example.invalid/"+local,
          ["kind"]=kind,["repository"]="fixture/repository",["revision"]=revision,["source_surface"]="public",["scope"]="selected",
          ["license"]="Apache-2.0",["selection_origins"]=new JsonArray("declared",new JsonObject{["row"]=17}),
          ["selection_reasons"]=new JsonArray("fixture","alias-preserved"),["source_release_context"]="acquisition context only",
          ["unknown_evidence"]=new JsonObject{["nullable"]=null,["nested"]=new JsonArray(1,2,3)} };
        var a=Row("api/A.java","blobs/a",A,"java-source","r1");a["git_blob"]=new string('a',40);a["metadata_cache"]=Cache("cache/a.gz",A,"java-source",2);
        if(c.Gitiles)a["transport"]=new JsonObject{["kind"]="gitiles-text"};
        var metadata=Row("sdk/api.xml","blobs/sdk",B,"sdk-api-index","r2");
        metadata["metadata_cache"]=Cache("cache/sdk.gz",B,"sdk-api-index",1);
        metadata["transport"]=new JsonObject{["kind"]="bounded-sdk-zip-range-member",["archive_url"]="https://example.invalid/platform.zip",["archive_member"]="android-37.2/data/api-versions.xml",["whole_archive_hash_verified"]=false};
        var alias=a.DeepClone().AsObject();alias["path"]="aliases/A.java";alias["source_surface"]="system";
        alias["source_release_context"]=new JsonObject{["release"]="fixture",["meaning"]="acquisition only"};
        var snapshot=Row("snapshot/api.txt","blobs/snapshot",C,"signature","r3");snapshot["transport"]=new JsonObject{["immutable_url"]=false};
        if(c.Bundle)snapshot["transport"]!["required_local_snapshot"]="snapshots/api.txt";
        var pinned=Row("sdk/pinned.xml","blobs/pinned",D,"sdk-api-index","r4");
        pinned["transport"]=new JsonObject{["archive_url"]="https://example.invalid/pinned.zip",["archive_member"]="android/data/api-versions.xml",["archive_sha256"]=A};
        if(c.MissingCacheCount){a["metadata_cache"]!.AsObject().Remove("records");alias["metadata_cache"]!.AsObject().Remove("records");}
        if(c.Error=="INVALID_GIT_BLOB")a["git_blob"]="bad";
        if(c.Error=="CONFLICTING_CACHE_ALIAS")alias["metadata_cache"]!["records"]=99;
        var rows=new JsonArray(a,metadata,alias,snapshot,pinned);if(c.Duplicate)rows.Add(a.DeepClone());
        root["inventory"]!["sources"]=rows;
    },gzip:c.GZip,limit:c.Limit) with {Profile=c.Profile};

    private static JsonElement Element(JsonNode value)
    {using var document=JsonDocument.Parse(value.ToJsonString());return document.RootElement.Clone();}

    public static RehydrationPlanResult Correct(ProjectionCase c)
    {
        if(c.Error is not null)return new(null,[new(c.Error,"")]);
        var request=Request(c);var basePlan=RehydrationContractCases.Correct(new(c.Id,request,null)).Plan!;
        // Independent fixture declarations, not the production recipe parser.
        var snapshot=new SnapshotRequirement("snapshot/api.txt","blobs/snapshot",c.Bundle?"snapshots/api.txt":null,C,3);
        var rowIndices=c.Duplicate?new[]{0,2,5}:new[]{0,2};
        SourceArchivePin Archive(string url,string member,string? hash=null)=>new(new(url),member,hash,Compatibility:c.Profile);
        SourcePin Pin(string path,string sha,string revision,SourceArchivePin? archive=null,string? git=null,string? bundle=null)=>new(path,new("https://example.invalid/"+path),revision,sha,3,git,c.Gitiles&&path=="blobs/a"?SourceWireEncoding.GitilesText:SourceWireEncoding.Raw,bundle,archive,c.Profile);
        RehydrationPrerequisite Missing(RehydrationPrerequisiteCode code,int row,string path)=>new(code,Array.AsReadOnly(new[]{row}),path,"Missing pinned prerequisite; exact existing target may be reused.");
        RehydrationPrerequisite[] metadataMissing=c.Profile==AcquisitionCompatibilityProfile.CanonicalRestore?
            new[]{RehydrationPrerequisiteCode.CanonicalArchiveETag,RehydrationPrerequisiteCode.CanonicalArchivePlatformVersion,RehydrationPrerequisiteCode.CanonicalArchiveRepositoryXml,RehydrationPrerequisiteCode.CanonicalArchiveSize}.Select(code=>Missing(code,1,"blobs/sdk")).ToArray():[];
        var physical=new RehydrationPhysicalSource[]
        {
            new(0,Pin("blobs/a",A,"r1",git:new string('a',40)),Array.AsReadOnly(rowIndices),[],[]),
            new(1,Pin("blobs/sdk",B,"r2",Archive("https://example.invalid/platform.zip","android-37.2/data/api-versions.xml")),Array.AsReadOnly(new[]{1}),[],Array.AsReadOnly(metadataMissing)),
            new(2,Pin("blobs/snapshot",C,"r3",bundle:snapshot.BundlePath),Array.AsReadOnly(new[]{3}),Array.AsReadOnly(new[]{snapshot}),c.Bundle?[]:Array.AsReadOnly(new[]{Missing(RehydrationPrerequisiteCode.RequiredImmutableSnapshot,3,"blobs/snapshot")})),
            new(3,Pin("blobs/pinned",D,"r4",Archive("https://example.invalid/pinned.zip","android/data/api-versions.xml",A)),Array.AsReadOnly(new[]{4}),[],[])
        };
        var caches=new RehydrationCacheAlias[]
        {
            new(0,"cache/a.gz",c.MissingCacheCount?null:new("cache/a.gz","java-source",C,D,2,A,basePlan.RecipeSha256),Array.AsReadOnly(rowIndices),c.MissingCacheCount?Array.AsReadOnly(new[]{new RehydrationPrerequisite(RehydrationPrerequisiteCode.HistoricalCacheRecordCount,Array.AsReadOnly(rowIndices),"cache/a.gz","Missing pinned prerequisite; exact existing target may be reused.")}):[]),
            new(1,"cache/sdk.gz",new("cache/sdk.gz","sdk-api-index",C,D,1,B,basePlan.RecipeSha256),Array.AsReadOnly(new[]{1}),[])
        };
        // Audit expected values are copied from the independently constructed
        // fixture JSON, including unknown fields and null/absent distinctions.
        var rawRequest=Request(c);byte[] raw=rawRequest.Recipe.Bytes.ToArray();
        if(c.GZip){using var input=new MemoryStream(raw);using var gzip=new System.IO.Compression.GZipStream(input,System.IO.Compression.CompressionMode.Decompress);using var output=new MemoryStream();gzip.CopyTo(output);raw=output.ToArray();}
        using var doc=JsonDocument.Parse(raw);
        var original=doc.RootElement.GetProperty("inventory").GetProperty("sources").EnumerateArray().Select(x=>x.Clone()).ToArray();
        var logical=new[]{"api/A.java","sdk/api.xml","aliases/A.java","snapshot/api.txt","sdk/pinned.xml"};
        var rows=original.Select((row,index)=>new RehydrationSourceRow(index,index==5?logical[0]:logical[index],index is 0 or 2 or 5?"java-source":index==3?"signature":"sdk-api-index",index is 0 or 2 or 5?0:index==1?1:index==3?2:3,index is 0 or 2 or 5?0:index==1?1:null,
            new("fixture/repository",index is 0 or 2 or 5?"r1":index==1?"r2":index==3?"r3":"r4",index==2?"system":"public","selected","Apache-2.0",row.GetProperty("source_release_context").Clone(),Array.AsReadOnly(row.GetProperty("selection_origins").EnumerateArray().Select(x=>x.Clone()).ToArray()),Array.AsReadOnly(new[]{"fixture","alias-preserved"})),row)).ToArray();
        var projection=new RehydrationSourceProjection("fixture-release",basePlan.RecipeSha256,c.Profile,Array.AsReadOnly(rows),Array.AsReadOnly(physical),Array.AsReadOnly(caches),Array.AsReadOnly(Enumerable.Range(0,Math.Min(c.Limit??4,4)).ToArray()),true);
        return new(basePlan with{SourceRows=rows.Length,UniqueSourcePaths=4,KnownSourceBytes=12,UnknownSizePaths=0,RequiredSnapshots=[snapshot],SourceProjection=projection},[]);
    }

    public static void AssertCase(ProjectionCase c,IRehydrationPlanner provider)
    {
        var request=Request(c);var before=request.Recipe.Bytes.ToArray();var result=provider.Plan(request);
        Assert.Equal(before,request.Recipe.Bytes.ToArray());Assert.Equal(c.Error is null,result.IsValid);
        Assert.Equal(c.Error is null?Array.Empty<string>():new[]{c.Error},result.Issues.Select(x=>x.Code));
        if(c.Error is not null){Assert.Null(result.Plan);return;}
        var plan=Assert.IsType<RehydrationPlan>(result.Plan);var expected=Correct(c).Plan!;var wanted=expected.SourceProjection!;
        var actual=Assert.IsType<RehydrationSourceProjection>(plan.SourceProjection);
        Assert.Equal(expected.SourceRows,plan.SourceRows);Assert.Equal(expected.UniqueSourcePaths,plan.UniqueSourcePaths);
        Assert.False(plan.Network);Assert.False(plan.Complete);Assert.False(plan.HistoricalByteIdentityClaimed);
        Assert.Equal(wanted.Release,actual.Release);Assert.Equal(wanted.RecipeSha256,actual.RecipeSha256);Assert.Equal(wanted.Profile,actual.Profile);Assert.True(actual.Resume);
        Assert.Equal(wanted.SelectedPhysicalSourceIndices,actual.SelectedPhysicalSourceIndices);Assert.Equal(wanted.Rows.Count,actual.Rows.Count);
        for(var i=0;i<wanted.Rows.Count;i++)
        {
            var e=wanted.Rows[i];var a=actual.Rows[i];Assert.Equal(e.RowIndex,a.RowIndex);Assert.Equal(e.LogicalPath,a.LogicalPath);Assert.Equal(e.Kind,a.Kind);Assert.Equal(e.PhysicalSourceIndex,a.PhysicalSourceIndex);Assert.Equal(e.CacheAliasIndex,a.CacheAliasIndex);
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(e.OriginalRow.GetRawText()),JsonNode.Parse(a.OriginalRow.GetRawText())));
            Assert.Equal(e.Context.Repository,a.Context.Repository);Assert.Equal(e.Context.Revision,a.Context.Revision);Assert.Equal(e.Context.SourceSurface,a.Context.SourceSurface);Assert.Equal(e.Context.Scope,a.Context.Scope);Assert.Equal(e.Context.License,a.Context.License);
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(e.Context.SourceReleaseContext!.Value.GetRawText()),JsonNode.Parse(a.Context.SourceReleaseContext!.Value.GetRawText())));
            Assert.Equal(e.Context.SelectionOrigins.Select(x=>x.GetRawText()),a.Context.SelectionOrigins.Select(x=>x.GetRawText()));Assert.Equal(e.Context.SelectionReasons,a.Context.SelectionReasons);
        }
        Assert.Equal(wanted.PhysicalSources.Count,actual.PhysicalSources.Count);
        for(var i=0;i<wanted.PhysicalSources.Count;i++)
        {var e=wanted.PhysicalSources[i];var a=actual.PhysicalSources[i];Assert.Equal(e.PhysicalSourceIndex,a.PhysicalSourceIndex);Assert.Equal(e.Pin,a.Pin);Assert.Equal(e.SelectionRowIndices,a.SelectionRowIndices);Assert.Equal(e.RequiredSnapshots,a.RequiredSnapshots);AssertPrerequisites(e.Prerequisites,a.Prerequisites);}
        Assert.Equal(wanted.CacheAliases.Count,actual.CacheAliases.Count);
        for(var i=0;i<wanted.CacheAliases.Count;i++)
        {var e=wanted.CacheAliases[i];var a=actual.CacheAliases[i];Assert.Equal(e.CacheAliasIndex,a.CacheAliasIndex);Assert.Equal(e.CachePath,a.CachePath);Assert.Equal(e.Pin,a.Pin);Assert.Equal(e.SelectionRowIndices,a.SelectionRowIndices);AssertPrerequisites(e.Prerequisites,a.Prerequisites);}
    }

    private static void AssertPrerequisites(IReadOnlyList<RehydrationPrerequisite> expected,IReadOnlyList<RehydrationPrerequisite> actual)
    {Assert.Equal(expected.Select(x=>(x.Code,x.Path)),actual.Select(x=>(x.Code,x.Path)));for(var i=0;i<expected.Count;i++){Assert.Equal(expected[i].SelectionRowIndices,actual[i].SelectionRowIndices);Assert.NotEmpty(actual[i].Reason);}}

    public static IEnumerable<RehydrationPlanResult> WrongResponses(ProjectionCase c)
    {
        var good=Correct(c);yield return good with{Issues=[new("WRONG","")]};
        if(good.Plan is not {} plan){yield return good with{Issues=[]};yield break;}
        var p=plan.SourceProjection!;
        RehydrationPlanResult Wrong(RehydrationSourceProjection changed)=>good with{Plan=plan with{SourceProjection=changed}};
        yield return good with{Plan=plan with{SourceProjection=null}};
        yield return Wrong(p with{Release="wrong"});yield return Wrong(p with{RecipeSha256="wrong"});yield return Wrong(p with{Profile=(AcquisitionCompatibilityProfile)99});yield return Wrong(p with{Resume=false});
        yield return Wrong(p with{Rows=p.Rows.Skip(1).ToArray()});yield return Wrong(p with{SelectedPhysicalSourceIndices=[99]});yield return Wrong(p with{PhysicalSources=p.PhysicalSources.Skip(1).ToArray()});yield return Wrong(p with{CacheAliases=[]});
        var row=p.Rows[0];
        foreach(var changed in new[]{row with{RowIndex=99},row with{LogicalPath="wrong"},row with{Kind="wrong"},row with{PhysicalSourceIndex=99},row with{CacheAliasIndex=99},row with{OriginalRow=Element(new JsonObject())},row with{Context=row.Context with{Repository="wrong"}},row with{Context=row.Context with{Revision="wrong"}},row with{Context=row.Context with{SourceSurface="wrong"}},row with{Context=row.Context with{Scope="wrong"}},row with{Context=row.Context with{License="wrong"}},row with{Context=row.Context with{SourceReleaseContext=Element(new JsonObject())}},row with{Context=row.Context with{SelectionOrigins=[]}},row with{Context=row.Context with{SelectionReasons=[]}}})
            yield return Wrong(p with{Rows=p.Rows.Select((x,i)=>i==0?changed:x).ToArray()});
        for(var index=0;index<p.PhysicalSources.Count;index++)
        {
            var source=p.PhysicalSources[index];
            var variants=new List<RehydrationPhysicalSource>{source with{PhysicalSourceIndex=99},source with{Pin=source.Pin with{Sha256="wrong"}},source with{Pin=source.Pin with{Url=new("https://example.invalid/wrong")}},source with{Pin=source.Pin with{Revision="wrong"}},source with{Pin=source.Pin with{SizeBytes=99}},source with{Pin=source.Pin with{GitBlob="wrong"}},source with{Pin=source.Pin with{Compatibility=(AcquisitionCompatibilityProfile)99}},source with{Pin=source.Pin with{Encoding=(SourceWireEncoding)99}},source with{SelectionRowIndices=[]}};
            if(source.Pin.Archive is {} archive){variants.Add(source with{Pin=source.Pin with{Archive=null}});foreach(var badArchive in new[]{archive with{Compatibility=(AcquisitionCompatibilityProfile)99},archive with{Url=new("https://example.invalid/wrong")},archive with{Member="wrong"},archive with{ArchiveSha256="wrong"},archive with{ArchiveSha1="wrong"},archive with{SizeBytes=99},archive with{ETag="wrong"},archive with{ArchiveLocalPath="wrong"},archive with{RepositoryXml=new byte[]{1}},archive with{PlatformVersion="wrong"},archive with{PackagePath="wrong"}})variants.Add(source with{Pin=source.Pin with{Archive=badArchive}});}
            if(source.RequiredSnapshots.Count>0){variants.Add(source with{RequiredSnapshots=[]});variants.Add(source with{Pin=source.Pin with{RequiredLocalSnapshot="wrong"}});}
            if(source.Prerequisites.Count>0){variants.Add(source with{Prerequisites=[]});variants.Add(source with{Prerequisites=source.Prerequisites.Select((x,i)=>i==0?x with{SelectionRowIndices=[99]}:x).ToArray()});variants.Add(source with{Prerequisites=source.Prerequisites.Select((x,i)=>i==0?x with{Reason=""}:x).ToArray()});}
            foreach(var changed in variants)yield return Wrong(p with{PhysicalSources=p.PhysicalSources.Select((x,i)=>i==index?changed:x).ToArray()});
        }
        for(var index=0;index<p.CacheAliases.Count;index++)
        {
            var cache=p.CacheAliases[index];var variants=new List<RehydrationCacheAlias>{cache with{CacheAliasIndex=99},cache with{CachePath="wrong"},cache with{SelectionRowIndices=[]}};
            if(cache.Pin is {} pin){variants.Add(cache with{Pin=null});foreach(var wrongPin in new[]{pin with{Path="wrong"},pin with{Kind="wrong"},pin with{GZipSha256="wrong"},pin with{RecordCount=99},pin with{ProducerFingerprint="wrong"},pin with{SourceSha256="wrong"},pin with{RecipeSha256="wrong"}})variants.Add(cache with{Pin=wrongPin});}
            if(cache.Prerequisites.Count>0){variants.Add(cache with{Prerequisites=[]});variants.Add(cache with{Prerequisites=cache.Prerequisites.Select((x,i)=>i==0?x with{SelectionRowIndices=[99]}:x).ToArray()});variants.Add(cache with{Prerequisites=cache.Prerequisites.Select((x,i)=>i==0?x with{Reason=""}:x).ToArray()});}
            foreach(var changed in variants)yield return Wrong(p with{CacheAliases=p.CacheAliases.Select((x,i)=>i==index?changed:x).ToArray()});
        }
    }
}

[Trait("Gate","Mock")]
[Trait("Requirement","TEST-BUILD-007")]
public sealed class RehydrationProjectionMockTests
{
    [Theory,MemberData(nameof(RehydrationProjectionCases.Cases),MemberType=typeof(RehydrationProjectionCases))]
    public void CorrectTypedProjectionMocksPass(ProjectionCase c)=>RehydrationProjectionCases.AssertCase(c,new ScriptedRehydrationPlanner(RehydrationProjectionCases.Correct(c)));

    [Theory,MemberData(nameof(RehydrationProjectionCases.Cases),MemberType=typeof(RehydrationProjectionCases))]
    public void EveryIndividualWrongFieldOrMissingProofIsRejected(ProjectionCase c)
    {foreach(var wrong in RehydrationProjectionCases.WrongResponses(c))Assert.NotNull(Record.Exception(()=>RehydrationProjectionCases.AssertCase(c,new ScriptedRehydrationPlanner(wrong))));}
}
