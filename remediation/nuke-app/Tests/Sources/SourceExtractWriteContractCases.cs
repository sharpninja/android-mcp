using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Publication;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed record SourceExtractWriteCase(string Id, bool Resume=false, bool Alias=false, int? Limit=null, bool Diagnostic=false, string? Error=null, bool DynamicKind=false);
public sealed record SourceExtractWriteObservation(SourceExtractResult? Result, Exception? Error, IReadOnlyDictionary<string,Artifact> Files,
    IReadOnlyList<string> Events, int RuntimeCreates, int RuntimeDisposals, int Dispatches, int RawReads, int Lookups, int Publications, bool OriginalsRetained, bool TokenForwarded);
public interface ISourceExtractWriteProbe
{ Task<SourceExtractWriteObservation> WriteAsync(SourceExtractWriteFixture fixture,CancellationToken cancellationToken); }

public sealed class SourceExtractWriteFixture
{
    public required SourceExtractWriteCase Case { get; init; }
    public SourceExtractRequest Request { get; private set; } = null!;
    public Dictionary<string,Artifact> RawFiles { get; } = new(StringComparer.Ordinal);
    public Dictionary<string,Artifact> GenerationFiles { get; } = new(StringComparer.Ordinal);
    public SourceCacheLookup PriorLookup { get; private set; } = null!;
    public SourceCachePublication Publication { get; private set; } = null!;
    public Artifact[] PublishedArtifacts { get; private set; } = [];
    public Exception PublisherFault { get; } = new IOException("Literal cache publication failure");
    public int SelectedCount => Math.Max(0,Math.Min(Case.Alias ? 2 : 1,Case.Limit ?? int.MaxValue));
    public int WantedHits => Case.Resume && SelectedCount>0 ? 1 : 0;
    public int WantedExtractions => SelectedCount-WantedHits;
    public int WantedPublications => SelectedCount>0 && !Case.Resume ? 1 : 0;
    public byte[] Records(bool conflicting=false) => Encoding.UTF8.GetBytes(conflicting ? "[{\"canonical\":\"fixture.Other\",\"kind\":\"class\"}]" : "[{\"canonical\":\"fixture.Cache\",\"kind\":\"class\"}]");
    public SourceContext Context => new("fixture/repository","pinned-revision",SourceExtractReadFixture.RecipeSha,null);
    public JsonObject Metadata(RehydrationSourceRow row) => new(){["logical_path"]=row.LogicalPath,["wire_status"]="unknown"};
    public SourceIssue[] Issues => Case.Diagnostic ? [new("retained-diagnostic","Literal parser issue retained",0,1)] : [];
    public StructuralParseStatus Status => Case.Diagnostic ? StructuralParseStatus.Partial : StructuralParseStatus.Clean;
    public IReadOnlyList<SourceExtractRow> ExpectedRows => Request.Projection.Rows.Take(SelectedCount).Select((row,index)=>
    {
        var hit=Case.Resume&&index==0;var receipt=Case.Resume?PriorLookup.Receipt:Publication.Receipt;var seal=Case.Resume?PriorLookup.Seal:Publication.Seal;
        return new SourceExtractRow(row.RowIndex,1,Context,Records(),Status,Issues,Metadata(row),Case.Diagnostic?"eA==":null,receipt,seal,hit);
    }).ToArray();
    public static SourceExtractWriteFixture Create(SourceExtractWriteCase test)
    {
        var data=new SourceExtractWriteFixture{Case=test};var canonical=new CanonicalJson();
        var baseline=SourceExtractReadFixture.Create(new("writer-fixture"));var projection=baseline.Request.Projection;
        if(test.DynamicKind)
        {
            var original=JsonNode.Parse(projection.Rows[0].OriginalRow.GetRawText())!.AsObject();original["kind"]="registered-cache-kind";
            var row=projection.Rows[0] with{Kind="registered-cache-kind",OriginalRow=JsonSerializer.SerializeToElement(original)};
            projection=projection with{Rows=[row]};
        }
        if(test.Alias)
        {
            var original=JsonNode.Parse(projection.Rows[0].OriginalRow.GetRawText())!.AsObject();original["path"]="aliases/API.xml";
            var alias=projection.Rows[0] with{RowIndex=1,LogicalPath="aliases/API.xml",OriginalRow=JsonSerializer.SerializeToElement(original)};
            projection=projection with{Rows=[projection.Rows[0],alias],CacheAliases=[projection.CacheAliases[0] with{SelectionRowIndices=[0,1]}],
                PhysicalSources=[projection.PhysicalSources[0] with{SelectionRowIndices=[0,1]}]};
        }
        data.Request=new(projection,test.Id=="unsafe-root"?"../escape":"new-generation",SourceExtractionProfile.CurrentCandidate,baseline.Request.Producer,
            CacheSourceLimit:test.Limit);
        var raw=new Artifact("raw/API.xml",Encoding.UTF8.GetBytes(test.Id=="raw-pin"?"y":"x"),"application/octet-stream");
        if(!test.Resume || test.Alias)data.RawFiles[raw.RelativePath]=raw;
        Artifact Bytes(string path,string value,string media)=>new(path,Encoding.UTF8.GetBytes(value),media);
        var jsonl=Encoding.UTF8.GetBytes("{\"canonical\":\"fixture.Cache\",\"kind\":\"class\"}\n");
        var gzip=AndroidMcp.BuildTool.Tests.Sources.Acquisition.SealedCacheFixture.GZip(jsonl);
        var fingerprint=canonical.Hash(JsonSerializer.SerializeToElement(new{cache_contract=3,language="csharp",runtime=data.Request.Producer.RuntimeIdentity,
            kind=projection.Rows[0].Kind,producer=data.Request.Producer.ProducerId,profile=data.Request.Profile.Id,code=data.Request.Producer.ProducerFiles}));
        var newData=new Artifact("new-generation/cache/0.json.gz",gzip,"application/gzip");
        SourceCacheReceipt Receipt(Artifact artifact)=>new(artifact.RelativePath,data.Request.Producer.ProducerId,data.Request.Profile.Id,projection.RecipeSha256,
            SourceExtractReadFixture.RawSha,fingerprint,ContentHash.Sha256(artifact.Bytes.Span),null,1,false);
        var newSidecar=new Artifact(newData.RelativePath+".csharp-cache.json",canonical.Serialize(JsonSerializer.SerializeToElement(Receipt(newData))),"application/json");
        data.PublishedArtifacts=[newData,newSidecar];
        data.Publication=new(Receipt(newData),new(Pin(newData),Pin(newSidecar),1));
        var oldData=new Artifact("old-generation/cache/API.gz",gzip,"application/gzip");
        var oldSidecar=new Artifact(oldData.RelativePath+".csharp-cache.json",canonical.Serialize(JsonSerializer.SerializeToElement(Receipt(oldData))),"application/json");
        data.PriorLookup=new(data.Records(),Receipt(oldData),new(Pin(oldData),Pin(oldSidecar),1));
        if(test.Resume)
        {
            data.GenerationFiles[oldData.RelativePath]=oldData;data.GenerationFiles[oldSidecar.RelativePath]=oldSidecar;
            var row=projection.Rows[0];var indexed=new SourceExtractIndexRow(row.RowIndex,row.LogicalPath,row.Kind,row.PhysicalSourceIndex,row.CacheAliasIndex,
                SourceExtractReadFixture.RawSha,1,canonical.Hash(row.OriginalRow),data.Context,row.Context.Scope,data.Status,data.Issues,data.Metadata(row),test.Diagnostic?"eA==":null,data.PriorLookup.Receipt,data.PriorLookup.Seal);
            var index=new SourceExtractIndex("androidmcp-native-source-index-v1",projection.Release,projection.RecipeSha256,data.Request.Profile.Id,data.Request.Producer,[indexed]);
            var indexArtifact=new Artifact("old-generation/source-index.json",canonical.Serialize(JsonSerializer.SerializeToElement(index)),"application/json");
            var manifest=new SourceExtractGenerationManifest("androidmcp-native-source-generation-v1","old-generation",[],[Pin(indexArtifact)],Pin(indexArtifact));
            var committed=new Artifact("old-generation/index.json",canonical.Serialize(JsonSerializer.SerializeToElement(manifest)),"application/json");
            data.GenerationFiles[indexArtifact.RelativePath]=indexArtifact;data.GenerationFiles[committed.RelativePath]=committed;
            data.Request=data.Request with{TrustedPriorIndex=test.Id=="prior-pin"?Pin(committed) with{Sha256=new('9',64)}:Pin(committed)};
        }
        if(test.Id=="occupied")data.GenerationFiles["new-generation/source-index.json"]=Bytes("new-generation/source-index.json","retained original output","application/json");
        return data;
    }
    public static StoredArtifact Pin(Artifact artifact)=>new(artifact.RelativePath,ContentHash.Sha256(artifact.Bytes.Span),artifact.Bytes.Length,artifact.MediaType);
}
public static class SourceExtractWriteContractCases
{
    public static readonly string[] ManifestFields=["Schema","GenerationId","Inputs","Outputs","IndexData"];
    public static readonly string[] IndexFields=["Schema","Release","RecipeSha256","ProfileId","Producer","Rows"];
    public static readonly string[] IndexedRowFields=["RowIndex","LogicalPath","Kind","PhysicalSourceIndex","CacheAliasIndex","SourceSha256","SourceLength","OriginalRowCanonicalSha256","Context","Scope","StructuralStatus","Issues","Metadata","RawSourceBase64","CacheReceipt","CacheSeal"];
    public static IReadOnlyList<SourceExtractWriteCase> All { get; } =
    [
        new("fresh"),new("trusted-hit-missing-raw",Resume:true),new("alias-two-logical",Alias:true),new("limit-one-logical",Alias:true,Limit:1),
        new("limit-zero",Limit:0),new("diagnostic",Diagnostic:true),new("dynamic-registration",DynamicKind:true),new("mixed-alias-hit-miss",Resume:true,Alias:true),
        new("negative-limit",Limit:-1,Error:"ArgumentOutOfRangeException"),new("unsafe-root",Error:"ArgumentException"),
        new("prior-pin",Resume:true,Error:"SOURCE_EXTRACT_INDEX_PIN"),new("raw-pin",Error:"SOURCE_EXTRACT_RAW_PIN"),
        new("alias-conflict",Alias:true,Error:"SOURCE_EXTRACT_ALIAS_CONFLICT"),new("occupied",Error:"OUTPUT_EXISTS"),
        new("publisher-fault",Error:"PROJECTION_FAILED"),new("cancelled",Error:"OperationCanceledException")
    ];
    public static IEnumerable<object[]> Cases=>All.Select(test=>new object[]{test.Id});
    public static SourceExtractWriteCase Get(string id)=>All.Single(test=>test.Id==id);
    public static async Task AssertContract(SourceExtractWriteCase test,ISourceExtractWriteProbe probe)
    {
        var fixture=SourceExtractWriteFixture.Create(test);using var cancellation=new CancellationTokenSource();if(test.Id=="cancelled")cancellation.Cancel();
        var observed=await probe.WriteAsync(fixture,cancellation.Token);Assert.True(observed.OriginalsRetained);Assert.True(observed.TokenForwarded);
        Assert.Equal(observed.RuntimeCreates,observed.RuntimeDisposals);
        if(test.Error is not null)
        {
            Assert.Null(observed.Result);Assert.NotNull(observed.Error);Assert.DoesNotContain("write:new-generation/index.json",observed.Events);
            if(test.Error=="OperationCanceledException")Assert.IsAssignableFrom<OperationCanceledException>(observed.Error);
            else if(test.Error.EndsWith("Exception",StringComparison.Ordinal))Assert.Equal(test.Error,observed.Error!.GetType().Name);
            else if(observed.Error is SourceExtractException source)Assert.Equal(test.Error,source.Code);
            else Assert.Equal(test.Error,Assert.IsType<ArtifactGenerationException>(observed.Error).Code);
            if(test.Id is "negative-limit" or "unsafe-root" or "prior-pin" or "cancelled"){Assert.Equal(0,observed.RuntimeCreates);Assert.Equal(0,observed.Dispatches);Assert.Equal(0,observed.Publications);}
            if(test.Id is "alias-conflict" or "occupied" or "raw-pin")Assert.Equal(0,observed.Publications);
            Assert.Equal(test.Id is "occupied" or "publisher-fault" ? 1 : 0,observed.Events.Count(value=>value.StartsWith("lease:",StringComparison.Ordinal)));
            Assert.Equal(test.Id is "occupied" or "publisher-fault" ? 1 : 0,observed.Events.Count(value=>value=="lease-dispose"));
            if(test.Id=="publisher-fault")Assert.Same(fixture.PublisherFault,observed.Error!.InnerException);
            return;
        }
        Assert.Null(observed.Error);var result=Assert.IsType<SourceExtractResult>(observed.Result);Assert.Equal(1,observed.RuntimeCreates);
        Assert.Equal(1,observed.Events.Count(value=>value.StartsWith("lease:",StringComparison.Ordinal)));Assert.Equal(1,observed.Events.Count(value=>value=="lease-dispose"));
        Assert.True(observed.Events.ToList().FindIndex(value=>value.StartsWith("lease:",StringComparison.Ordinal))<observed.Events.ToList().FindIndex(value=>value.StartsWith("write:",StringComparison.Ordinal)));
        Assert.Equal(fixture.SelectedCount,result.SelectedLogicalCacheRows);Assert.Equal(fixture.SelectedCount>0?1:0,result.UniqueCacheAliases);
        Assert.Equal(fixture.WantedHits,result.CacheHits);Assert.Equal(fixture.WantedExtractions,result.RawExtractions);Assert.Equal(fixture.WantedExtractions,observed.Dispatches);
        Assert.Equal(fixture.WantedHits>0?1:0,observed.Lookups);Assert.Equal(fixture.WantedPublications,observed.Publications);
        if(fixture.WantedExtractions==0)Assert.Equal(0,observed.RawReads);else Assert.True(observed.RawReads>0);
        Assert.Equal(fixture.ExpectedRows.Count,result.Rows.Count);
        for(var index=0;index<result.Rows.Count;index++)AssertRow(fixture.ExpectedRows[index],result.Rows[index]);
        Assert.Equal("new-generation/index.json",result.Index.RelativePath);var manifestArtifact=observed.Files[result.Index.RelativePath];Assert.Equal(SourceExtractWriteFixture.Pin(manifestArtifact),result.Index);
        Assert.Equal("write:new-generation/index.json",observed.Events.Last(value=>value.StartsWith("write:",StringComparison.Ordinal)));
        using var manifestJson=JsonDocument.Parse(manifestArtifact.Bytes);var manifest=manifestJson.RootElement.Deserialize<SourceExtractGenerationManifest>()!;
        Assert.True(new HashSet<string>(ManifestFields,StringComparer.Ordinal).SetEquals(manifestJson.RootElement.EnumerateObject().Select(property=>property.Name)));
        Assert.Equal("androidmcp-native-source-generation-v1",manifest.Schema);Assert.Equal("new-generation",manifest.GenerationId);Assert.Contains(manifest.IndexData,manifest.Outputs);
        var expectedInputs=new List<StoredArtifact>();
        if(fixture.WantedExtractions>0)expectedInputs.Add(SourceExtractWriteFixture.Pin(fixture.RawFiles["raw/API.xml"]) with{RelativePath="source/raw/API.xml"});
        if(fixture.WantedHits>0)expectedInputs.AddRange(fixture.GenerationFiles.Where(pair=>pair.Key.StartsWith("old-generation/",StringComparison.Ordinal)).Select(pair=>SourceExtractWriteFixture.Pin(pair.Value) with{RelativePath="generation/"+pair.Key}));
        Assert.Equal(expectedInputs.OrderBy(pin=>pin.RelativePath,StringComparer.Ordinal),manifest.Inputs.OrderBy(pin=>pin.RelativePath,StringComparer.Ordinal));
        var expectedOutputs=fixture.WantedPublications>0?fixture.PublishedArtifacts.Select(SourceExtractWriteFixture.Pin).Append(manifest.IndexData):[manifest.IndexData];
        Assert.Equal(expectedOutputs.OrderBy(pin=>pin.RelativePath,StringComparer.Ordinal),manifest.Outputs.OrderBy(pin=>pin.RelativePath,StringComparer.Ordinal));
        Assert.Equal(SourceExtractWriteFixture.Pin(observed.Files[manifest.IndexData.RelativePath]),manifest.IndexData);
        using var indexJson=JsonDocument.Parse(observed.Files[manifest.IndexData.RelativePath].Bytes);var data=indexJson.RootElement.Deserialize<SourceExtractIndex>()!;
        Assert.True(new HashSet<string>(IndexFields,StringComparer.Ordinal).SetEquals(indexJson.RootElement.EnumerateObject().Select(property=>property.Name)));
        Assert.Equal("androidmcp-native-source-index-v1",data.Schema);Assert.Equal(fixture.Request.Projection.Release,data.Release);Assert.Equal(fixture.Request.Projection.RecipeSha256,data.RecipeSha256);Assert.Equal(fixture.Request.Profile.Id,data.ProfileId);
        Assert.Equal(fixture.ExpectedRows.Count,data.Rows.Count);Assert.Equal(fixture.Request.Producer.ProducerId,data.Producer.ProducerId);
        var canonical=new CanonicalJson();
        Assert.Equal(canonical.Hash(JsonSerializer.SerializeToElement(fixture.Request.Producer)),canonical.Hash(JsonSerializer.SerializeToElement(data.Producer)));
        foreach(var rowJson in indexJson.RootElement.GetProperty("Rows").EnumerateArray())Assert.True(new HashSet<string>(IndexedRowFields,StringComparer.Ordinal).SetEquals(rowJson.EnumerateObject().Select(property=>property.Name)));
        foreach(var row in data.Rows)
        {
            var original=fixture.Request.Projection.Rows.Single(value=>value.RowIndex==row.RowIndex);var expected=fixture.ExpectedRows.Single(value=>value.RowIndex==row.RowIndex);
            Assert.Equal(fixture.Context,row.Context);Assert.Null(row.Context.PlatformVersion);Assert.Equal(SourceExtractReadFixture.RawSha,row.SourceSha256);Assert.Equal(1,row.SourceLength);
            Assert.Equal(original.LogicalPath,row.LogicalPath);Assert.Equal(original.Kind,row.Kind);Assert.Equal(original.PhysicalSourceIndex,row.PhysicalSourceIndex);Assert.Equal(original.CacheAliasIndex,row.CacheAliasIndex);
            Assert.Equal(canonical.Hash(original.OriginalRow),row.OriginalRowCanonicalSha256);Assert.Equal(original.Context.Scope,row.Scope);Assert.Equal(expected.StructuralStatus,row.StructuralStatus);
            Assert.Equal(expected.Issues,row.Issues);Assert.True(JsonNode.DeepEquals(expected.Metadata,row.Metadata));Assert.Equal(expected.RawSourceBase64,row.RawSourceBase64);
            Assert.Equal(expected.CacheReceipt,row.CacheReceipt);Assert.Equal(expected.CacheSeal,row.CacheSeal);
        }
    }
    private static void AssertRow(SourceExtractRow expected,SourceExtractRow actual)
    {
        Assert.Equal(expected.RowIndex,actual.RowIndex);Assert.Equal(expected.SourceLength,actual.SourceLength);Assert.Equal(expected.Context,actual.Context);Assert.Equal(expected.CacheHit,actual.CacheHit);
        Assert.Equal(expected.RecordsJson.ToArray(),actual.RecordsJson.ToArray());Assert.Equal(expected.StructuralStatus,actual.StructuralStatus);Assert.Equal(expected.Issues,actual.Issues);
        Assert.True(JsonNode.DeepEquals(expected.Metadata,actual.Metadata));Assert.Equal(expected.RawSourceBase64,actual.RawSourceBase64);Assert.Equal(expected.CacheReceipt,actual.CacheReceipt);Assert.Equal(expected.CacheSeal,actual.CacheSeal);
    }
}
