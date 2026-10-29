using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Publication;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources.Acquisition;

public interface ISourcesInventoryReaderProbe
{
    Task<SourcesInventoryResult> ReadAsync(SourcesInventoryReadRequest request,CancellationToken token);
    IReadOnlyList<(StoredArtifact Pin,CancellationToken Token)> PinReads{get;}
    IReadOnlyList<(SourceBatchResumeRequest Request,CancellationToken Token)> ResumeReads{get;}
    int Writes{get;}
    int Http{get;}
    bool InputRetained{get;}
}
public sealed class SourcesInventoryReaderCase:IDisposable
{
    public required string Mode{get;init;}
    public required SourcesInventoryReadRequest Request{get;init;}
    public required SourcesInventoryReceipt Published{get;init;}
    public required SourcesInventoryReceipt Expected{get;init;}
    public required byte[] Bytes{get;init;}
    public CancellationTokenSource Cancellation{get;}=new();
    public bool Success=>Mode is "partial" or "full" or "empty";
    public int ExpectedPinReads=>Mode is "cancel-before" or "media"?0:1;
    public int ExpectedResumeReads=>Mode is "partial" or "full" or "empty" or "count" or "entry" or "extraction" or "historical" or "embedded-receipt" or "batch-missing" or "batch-pin" or "batch-output" or "not-reused" or "result-pin"?1:0;
    public string ErrorCode=>Mode switch{
        "pin-hash"=>"ARTIFACT_PIN_HASH","pin-length"=>"ARTIFACT_PIN_LENGTH",
        "media" or "json" or "duplicate-json" or "missing-shape"=>"INVENTORY_READ_SCHEMA",
        "recipe" or "profile" or "context"=>"INVENTORY_READ_PROJECTION",
        "batch-missing"=>"BATCH_REQUIRED_RECEIPT","batch-pin"=>"BATCH_RECEIPT_PIN","batch-output"=>"BATCH_SEALED",_=>"INVENTORY_READ_METADATA"};
    public static SourcesInventoryReaderCase Create(string mode)
    {
        using var source=SourcesInventoryCase.Create(mode is "full" or "empty"?mode:"partial");
        var expected=source.ExpectedReceipt;var published=expected;var projection=expected.Acquisition.Receipt.Projection;
        if(mode=="count")published=published with{VerifiedPhysicalSources=99};
        if(mode=="entry")published=published with{Entries=published.Entries.Select((entry,index)=>index==0?entry with{Disposition=SourceInventoryDisposition.NotSelected}:entry).ToArray()};
        if(mode=="extraction")published=published with{ExtractionComplete=true};
        if(mode=="historical")published=published with{HistoricalByteIdentityClaimed=true};
        if(mode=="embedded-receipt")
        {
            var receipt=published.Acquisition.Receipt;var original=receipt.Sources[0];
            published=published with{Acquisition=published.Acquisition with{Receipt=receipt with{Sources=[original with{Source=original.Source with{Status="wrong"}}]}}};
        }
        if(mode=="recipe")projection=projection with{RecipeSha256=new string('c',64)};
        if(mode=="profile")projection=projection with{Profile=AcquisitionCompatibilityProfile.GenericRehydrate};
        if(mode=="context")projection=projection with{Rows=projection.Rows.Select((row,index)=>index==0?row with{Context=row.Context with{Repository="different"}}:row).ToArray()};
        var bytes=SourceBatchCases.Canonical.Serialize(JsonSerializer.SerializeToElement(published));
        if(mode=="json")bytes=[123];
        if(mode=="missing-shape")bytes=JsonSerializer.SerializeToUtf8Bytes(new{});
        if(mode=="duplicate-json")
        {
            using var output=new MemoryStream();using(var writer=new Utf8JsonWriter(output)){writer.WriteStartObject();writer.WriteBoolean("RawComplete",true);writer.WriteBoolean("RawComplete",false);writer.WriteEndObject();}bytes=output.ToArray();
        }
        var pin=new StoredArtifact("sources/inventory.json",ContentHash.Sha256(bytes),bytes.Length,mode=="media"?"application/gzip":"application/json");
        var test=new SourcesInventoryReaderCase{Mode=mode,Request=new(projection,pin),Published=published,Expected=expected,Bytes=bytes};
        if(mode=="cancel-before")test.Cancellation.Cancel();return test;
    }
    public static IEnumerable<object[]> All=>new[]{"partial","full","empty","media","pin-hash","pin-length","missing","json","duplicate-json","missing-shape","recipe","profile","context",
        "count","entry","extraction","historical","embedded-receipt","batch-missing","batch-pin","batch-output","not-reused","result-pin","cancel-before","cancel-during-pin"}.Select(mode=>new object[]{Create(mode)});
    public async Task AssertCase(ISourcesInventoryReaderProbe probe)
    {
        SourcesInventoryResult? result=null;
        if(Mode is "cancel-before" or "cancel-during-pin")await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>probe.ReadAsync(Request,Cancellation.Token));
        else if(Mode=="missing")await Assert.ThrowsAsync<FileNotFoundException>(()=>probe.ReadAsync(Request,Cancellation.Token));
        else if(Mode is "pin-hash" or "pin-length")Assert.Equal(ErrorCode,(await Assert.ThrowsAsync<PinnedArtifactException>(()=>probe.ReadAsync(Request,Cancellation.Token))).Code);
        else if(Mode.StartsWith("batch-",StringComparison.Ordinal))Assert.Equal(ErrorCode,(await Assert.ThrowsAsync<TransportException>(()=>probe.ReadAsync(Request,Cancellation.Token))).Code);
        else if(!Success){var error=await Assert.ThrowsAsync<SourcesInventoryException>(()=>probe.ReadAsync(Request,Cancellation.Token));Assert.Equal(ErrorCode,error.Code);Assert.Empty(error.Issues);}
        else result=await probe.ReadAsync(Request,Cancellation.Token);
        Assert.Equal(ExpectedPinReads,probe.PinReads.Count);foreach(var read in probe.PinReads){Assert.Same(Request.InventoryManifest,read.Pin);Assert.Equal(Cancellation.Token,read.Token);}
        Assert.Equal(ExpectedResumeReads,probe.ResumeReads.Count);foreach(var read in probe.ResumeReads)
        {
            Assert.Equal(Cancellation.Token,read.Token);Assert.Equal(Published.Acquisition.Manifest,read.Request.RequiredManifest);
            Assert.Same(Request.Projection,read.Request.Acquisition.Projection);Assert.Equal(Published.Acquisition.Receipt.GenerationId,read.Request.Acquisition.GenerationId);
            Assert.Equal(Published.Acquisition.Manifest.RelativePath,read.Request.Acquisition.ReceiptPath);Assert.Equal(Published.Acquisition.Receipt.CachePolicy,read.Request.Acquisition.CachePolicy);
        }
        Assert.Equal(0,probe.Writes);Assert.Equal(0,probe.Http);Assert.True(probe.InputRetained);
        if(!Success)return;
        Assert.NotNull(result);Assert.Same(Request.InventoryManifest,result.Manifest);
        Assert.Equal(SourceBatchCases.Canonical.Serialize(JsonSerializer.SerializeToElement(Expected)),SourceBatchCases.Canonical.Serialize(JsonSerializer.SerializeToElement(result.Receipt)));
    }
    public void Dispose()=>Cancellation.Dispose();
}
public sealed class ScriptedSourcesInventoryReaderProbe(SourcesInventoryReaderCase fixture,string? mutation=null):ISourcesInventoryReaderProbe
{
    private readonly List<(StoredArtifact Pin,CancellationToken Token)> pins=[];
    private readonly List<(SourceBatchResumeRequest Request,CancellationToken Token)> resumes=[];
    public IReadOnlyList<(StoredArtifact Pin,CancellationToken Token)> PinReads=>pins;
    public IReadOnlyList<(SourceBatchResumeRequest Request,CancellationToken Token)> ResumeReads=>resumes;
    public int Writes=>mutation=="write"?1:0;
    public int Http=>mutation=="http"?1:0;
    public bool InputRetained=>mutation!="input";
    public Task<SourcesInventoryResult> ReadAsync(SourcesInventoryReadRequest request,CancellationToken token)
    {
        if(fixture.Mode=="cancel-before"&&mutation!="cancel")token.ThrowIfCancellationRequested();
        if(fixture.ExpectedPinReads>0)pins.Add((mutation=="pin"?request.InventoryManifest with{Sha256="wrong"}:request.InventoryManifest,mutation=="pin-token"?CancellationToken.None:token));
        if(mutation=="double-pin"&&pins.Count>0)pins.Add(pins[0]);
        if(mutation=="early-pin"&&pins.Count==0)pins.Add((request.InventoryManifest,token));
        if(fixture.ExpectedResumeReads>0)
        {
            var published=fixture.Published.Acquisition;
            resumes.Add((new(new(mutation=="generation"?"wrong":published.Receipt.GenerationId,mutation=="path"?"wrong":published.Manifest.RelativePath,
                mutation=="projection"?request.Projection with{Release="wrong"}:request.Projection,mutation=="policy"?SourceBatchCachePolicy.ImportLockedHistorical:published.Receipt.CachePolicy),
                mutation=="resume-pin"?published.Manifest with{Sha256="wrong"}:published.Manifest),mutation=="resume-token"?CancellationToken.None:token));
        }
        if(mutation=="double-resume"&&resumes.Count>0)resumes.Add(resumes[0]);
        if(mutation=="early-resume"&&resumes.Count==0)resumes.Add((new(new(fixture.Expected.Acquisition.Receipt.GenerationId,fixture.Expected.Acquisition.Manifest.RelativePath,request.Projection),fixture.Expected.Acquisition.Manifest),token));
        if(mutation=="omit-pin")pins.Clear();if(mutation=="omit-resume")resumes.Clear();
        if(fixture.Mode=="cancel-during-pin"&&mutation!="cancel")throw new OperationCanceledException(token);
        if(fixture.Mode=="missing")throw new FileNotFoundException();
        if(!fixture.Success&&fixture.Mode is not ("cancel-before" or "cancel-during-pin")&&mutation!="omit-error")
        {
            var code=mutation=="code"?"wrong":fixture.ErrorCode;
            if(fixture.Mode is "pin-hash" or "pin-length")throw new PinnedArtifactException(code);
            if(fixture.Mode.StartsWith("batch-",StringComparison.Ordinal))throw new TransportException(code,"Resume-only rejection");
            throw new SourcesInventoryException(code,mutation=="issues"?[new("wrong","wrong")]:[]);
        }
        var receipt=fixture.Expected;
        if(mutation=="receipt")receipt=receipt with{RawComplete=!receipt.RawComplete};
        return Task.FromResult(new SourcesInventoryResult(receipt,mutation=="manifest"?request.InventoryManifest with{Sha256="wrong"}:request.InventoryManifest));
    }
}
[Trait("Gate","Mock")]
public sealed class SourcesInventoryReaderMockTests
{
    [Theory,MemberData(nameof(SourcesInventoryReaderCase.All),MemberType=typeof(SourcesInventoryReaderCase))]
    public Task CorrectPinnedScopeResumeAndMetadataAdmission(SourcesInventoryReaderCase test)=>test.AssertCase(new ScriptedSourcesInventoryReaderProbe(test));
    [Theory,MemberData(nameof(SourcesInventoryReaderCase.All),MemberType=typeof(SourcesInventoryReaderCase))]
    public async Task WrongTrustArgumentClaimsEffectsAndDiagnosticsRejected(SourcesInventoryReaderCase test)
    {
        var mutations=new List<string>{"write","http","input"};
        if(test.Mode!="cancel-before")
        {
            if(test.ExpectedPinReads>0)mutations.AddRange(["pin","pin-token","double-pin","omit-pin"]);else mutations.Add("early-pin");
            if(test.ExpectedResumeReads>0)mutations.AddRange(["generation","path","projection","policy","resume-pin","resume-token","double-resume","omit-resume"]);else mutations.Add("early-resume");
        }
        if(test.Success)mutations.AddRange(["receipt","manifest"]);
        else if(test.Mode is "cancel-before" or "cancel-during-pin")mutations.Add("cancel");
        else if(test.Mode!="missing")
        {
            mutations.AddRange(["omit-error","code"]);
            if(test.Mode is not ("pin-hash" or "pin-length")&&!test.Mode.StartsWith("batch-",StringComparison.Ordinal))mutations.Add("issues");
        }
        foreach(var mutation in mutations)Assert.NotNull(await Record.ExceptionAsync(()=>test.AssertCase(new ScriptedSourcesInventoryReaderProbe(test,mutation))));
    }
}
