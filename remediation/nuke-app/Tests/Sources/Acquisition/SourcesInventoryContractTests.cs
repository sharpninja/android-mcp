using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Publication;
using AndroidMcp.BuildTool.Tests.Publication;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources.Acquisition;

public interface ISourcesInventoryProbe
{
    Task<SourcesInventoryResult> RunAsync(SourcesInventoryRequest request,CancellationToken token);
    IReadOnlyList<RehydrationPlanRequest> PlannerRequests{get;}
    IReadOnlyList<(string Path,CancellationToken Token)> Reads{get;}
    IReadOnlyList<(SourceBatchResumeRequest Request,CancellationToken Token)> ResumeRequests{get;}
    IReadOnlyList<(Artifact Artifact,CancellationToken Token)> Writes{get;}
    bool InputRetained{get;}
    int NetworkCalls{get;}
    int ProcessCalls{get;}
}
public sealed class SourcesInventoryCase:IDisposable
{
    public required string Mode{get;init;}
    public required SourcesInventoryRequest Request{get;init;}
    public required RehydrationPlanResult Planned{get;init;}
    public required SourceBatchResult Acquired{get;init;}
    public required byte[] AdmissionBytes{get;init;}
    public CancellationTokenSource Cancellation{get;}=new();
    public bool Success=>Mode is "partial" or "full" or "empty";
    public int ExpectedPlans=>Mode=="cancelled"?0:1;
    public int ExpectedReads=>Mode is "cancelled" or "invalid-plan" or "missing-projection"||Mode.StartsWith("non-pure-",StringComparison.Ordinal)?0:1;
    public int ExpectedResumes=>ExpectedReads==0||Mode is "manifest-missing" or "manifest-hash" or "manifest-length" or "manifest-malformed"?0:1;
    public int ExpectedWrites=>Success||Mode=="output-failure"?1:0;
    public string ErrorCode=>Mode switch{
        "invalid-plan"=>"INVENTORY_PLAN_INVALID","missing-projection"=>"INVENTORY_PROJECTION_REQUIRED","non-pure-plan" or "non-pure-complete" or "non-pure-historical"=>"INVENTORY_PLAN_NOT_PURE",
        "manifest-hash" or "manifest-length" or "reader-pin"=>"INVENTORY_ACQUISITION_PIN","manifest-malformed"=>"INVENTORY_ACQUISITION_RECEIPT",
        "reader-missing"=>"BATCH_REQUIRED_RECEIPT","reader-replaced"=>"BATCH_RECEIPT_PIN","reader-output-missing"=>"BATCH_SEALED","reader-not-reused"=>"INVENTORY_SEALED",_=>""};
    public SourcesInventoryReceipt ExpectedReceipt
    {
        get
        {
            var projection=Acquired.Receipt.Projection;
            var entries=projection.PhysicalSources.Select(source=>{
                var selected=projection.SelectedPhysicalSourceIndices.Contains(source.PhysicalSourceIndex);
                return new SourceInventoryEntry(source.PhysicalSourceIndex,selected?SourceInventoryDisposition.VerifiedSelected:SourceInventoryDisposition.NotSelected,
                    selected?Acquired.Receipt.Sources.Single(item=>item.PhysicalSourceIndex==source.PhysicalSourceIndex).Source:null);
            }).ToArray();
            return new(Acquired,entries,projection.SelectedPhysicalSourceIndices.Count,
                projection.PhysicalSources.Count-projection.SelectedPhysicalSourceIndices.Count,Acquired.Receipt.Complete,false,false);
        }
    }
    public byte[] ExpectedBytes=>SourceBatchCases.Canonical.Serialize(JsonSerializer.SerializeToElement(ExpectedReceipt));
    public static SourcesInventoryCase Create(string mode)
    {
        var pin0=SourceBatchCases.Pin();
        var pin1=SourceBatchCases.Pin("raw/b") with{SizeBytes=null,RequiredLocalSnapshot="snapshots/b",
            Archive=new(new Uri("https://example.test/platform.zip"),"source.properties",SizeBytes:100)};
        var snapshots=new[]{new SnapshotRequirement("logical/2","raw/b",null,SourceBatchCases.AbcSha,null)};
        var physical=new[]{SourceBatchCases.Physical(0,pin0,[0,1]),SourceBatchCases.Physical(1,pin1,[2],snapshots,
            [SourceBatchCases.Missing(RehydrationPrerequisiteCode.RequiredImmutableSnapshot,"raw/b",2),SourceBatchCases.Missing(RehydrationPrerequisiteCode.CanonicalArchiveETag,"raw/b",2)])};
        var rows=new[]{SourceBatchCases.Row(0,0,0),SourceBatchCases.Row(1,0,0) with{LogicalPath="alias/A",Context=SourceBatchCases.Row(1,0).Context with{Repository=null}},SourceBatchCases.Row(2,1)};
        var selected=mode=="empty"?Array.Empty<int>():mode=="full"?new[]{0,1}:new[]{0};
        var batch=SourceBatchCases.Request(physical,rows,selected,caches:[new(0,"cache/native-unadmitted.gz",null,[0,1],
            [SourceBatchCases.Missing(RehydrationPrerequisiteCode.HistoricalCacheRecordCount,"cache/native-unadmitted.gz")])]);
        var batchCase=new SourceBatchCase("inventory-prior",batch,new Dictionary<string,byte[]>{{"raw/a",SourceBatchCases.Abc}},
            new Dictionary<string,byte[]>(),null,0,0,selected.Length==2,rows.Count(row=>selected.Contains(row.PhysicalSourceIndex)),selected.Length,0);
        var receipt=SourceBatchCases.ExpectedReceipt(batchCase);
        var admission=SourceBatchCases.Canonical.Serialize(JsonSerializer.SerializeToElement(receipt));
        var manifest=new StoredArtifact(batch.ReceiptPath,ContentHash.Sha256(admission),admission.Length,"application/json");
        var recipe=RehydrationProjectionCases.Request(new("inventory-facade"));
        var projection=batch.Projection;
        var plan=new RehydrationPlan(projection.Release,projection.RecipeSha256,rows.Length,physical.Length,3,1,snapshots,[],
            new("rehydration/plan.json",Encoding.UTF8.GetBytes("{}"),"application/json"),false,false,false,[],
            [new("logical/2","raw/b",pin1.Archive!.Url.AbsoluteUri,pin1.Archive.Member,null,pin1.Sha256)],projection);
        var planned=new RehydrationPlanResult(plan,[]);
        if(mode=="invalid-plan")planned=new(null,[new("LOCK_COUNT_MISMATCH","recipe.lock.json")]);
        if(mode=="missing-projection")planned=new(plan with{SourceProjection=null},[]);
        if(mode=="non-pure-plan")planned=new(plan with{Network=true},[]);
        if(mode=="non-pure-complete")planned=new(plan with{Complete=true},[]);
        if(mode=="non-pure-historical")planned=new(plan with{HistoricalByteIdentityClaimed=true},[]);
        if(mode=="manifest-malformed"){admission=Encoding.UTF8.GetBytes("[]");manifest=manifest with{Sha256=ContentHash.Sha256(admission),Length=admission.Length};}
        if(mode=="manifest-hash")admission=[0];
        if(mode=="manifest-length")manifest=manifest with{Length=manifest.Length+1};
        var test=new SourcesInventoryCase{Mode=mode,Request=new(recipe,manifest),Planned=planned,
            Acquired=new(receipt,new(batch.ReceiptPath,ContentHash.Sha256(SourceBatchCases.Canonical.Serialize(JsonSerializer.SerializeToElement(receipt))),
                SourceBatchCases.Canonical.Serialize(JsonSerializer.SerializeToElement(receipt)).Length,"application/json"),true),AdmissionBytes=admission};
        if(mode=="cancelled")test.Cancellation.Cancel();return test;
    }
    public static IEnumerable<object[]> Cases=>new[]{"partial","full","empty","invalid-plan","missing-projection","non-pure-plan","non-pure-complete","non-pure-historical",
        "manifest-missing","manifest-hash","manifest-length","manifest-malformed","reader-missing","reader-replaced","reader-output-missing",
        "reader-not-reused","reader-pin","cancelled","output-failure"}.Select(mode=>new object[]{Create(mode)});
    public async Task AssertCase(ISourcesInventoryProbe probe)
    {
        SourcesInventoryResult? result=null;
        if(Mode=="cancelled")await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>probe.RunAsync(Request,Cancellation.Token));
        else if(Mode=="manifest-missing")await Assert.ThrowsAsync<FileNotFoundException>(()=>probe.RunAsync(Request,Cancellation.Token));
        else if(Mode=="output-failure")await Assert.ThrowsAsync<IOException>(()=>probe.RunAsync(Request,Cancellation.Token));
        else if(!Success)
        {
            if(Mode.StartsWith("reader-",StringComparison.Ordinal)&&Mode is not ("reader-not-reused" or "reader-pin"))
                Assert.Equal(ErrorCode,(await Assert.ThrowsAsync<TransportException>(()=>probe.RunAsync(Request,Cancellation.Token))).Code);
            else{var error=await Assert.ThrowsAsync<SourcesInventoryException>(()=>probe.RunAsync(Request,Cancellation.Token));Assert.Equal(ErrorCode,error.Code);Assert.Equal(Mode=="invalid-plan"?Planned.Issues:Array.Empty<PublicationIssue>(),error.Issues);}
        }
        else result=await probe.RunAsync(Request,Cancellation.Token);
        Assert.Equal(ExpectedPlans,probe.PlannerRequests.Count);if(ExpectedPlans>0)Assert.Same(Request.Recipe,probe.PlannerRequests[0]);
        Assert.Equal(ExpectedReads,probe.Reads.Count);foreach(var read in probe.Reads){Assert.Equal(Request.AcquisitionManifest.RelativePath,read.Path);Assert.Equal(Cancellation.Token,read.Token);}
        Assert.Equal(ExpectedResumes,probe.ResumeRequests.Count);
        foreach(var call in probe.ResumeRequests)
        {
            Assert.Equal(Request.AcquisitionManifest,call.Request.RequiredManifest);Assert.Equal(Cancellation.Token,call.Token);
            Assert.Equal(Acquired.Receipt.GenerationId,call.Request.Acquisition.GenerationId);Assert.Equal(Request.AcquisitionManifest.RelativePath,call.Request.Acquisition.ReceiptPath);
            Assert.Equal(Acquired.Receipt.CachePolicy,call.Request.Acquisition.CachePolicy);Assert.Same(Planned.Plan!.SourceProjection,call.Request.Acquisition.Projection);
        }
        Assert.Equal(ExpectedWrites,probe.Writes.Count);foreach(var write in probe.Writes){Assert.Equal(Request.ManifestPath,write.Artifact.RelativePath);Assert.Equal("application/json",write.Artifact.MediaType);Assert.Equal(ExpectedBytes,write.Artifact.Bytes.ToArray());Assert.Equal(Cancellation.Token,write.Token);}
        Assert.True(probe.InputRetained);Assert.Equal(0,probe.NetworkCalls);Assert.Equal(0,probe.ProcessCalls);
        if(!Success)return;
        Assert.NotNull(result);Assert.Equal(ExpectedBytes,SourceBatchCases.Canonical.Serialize(JsonSerializer.SerializeToElement(result.Receipt)));
        Assert.Equal(new StoredArtifact(Request.ManifestPath,ContentHash.Sha256(ExpectedBytes),ExpectedBytes.Length,"application/json"),result.Manifest);
        Assert.False(result.Receipt.ExtractionComplete);Assert.False(result.Receipt.HistoricalByteIdentityClaimed);
    }
    public void Dispose()=>Cancellation.Dispose();
}

public sealed class ScriptedSourcesInventoryProbe(SourcesInventoryCase fixture,string? mutation=null,SourcesInventoryReceipt? replacement=null):ISourcesInventoryProbe
{
    private readonly List<RehydrationPlanRequest> plans=[];
    private readonly List<(string Path,CancellationToken Token)> reads=[];
    private readonly List<(SourceBatchResumeRequest Request,CancellationToken Token)> resumes=[];
    private readonly List<(Artifact Artifact,CancellationToken Token)> writes=[];
    public IReadOnlyList<RehydrationPlanRequest> PlannerRequests=>plans;
    public IReadOnlyList<(string Path,CancellationToken Token)> Reads=>reads;
    public IReadOnlyList<(SourceBatchResumeRequest Request,CancellationToken Token)> ResumeRequests=>resumes;
    public IReadOnlyList<(Artifact Artifact,CancellationToken Token)> Writes=>writes;
    public bool InputRetained=>mutation!="input";
    public int NetworkCalls=>mutation=="network"?1:0;
    public int ProcessCalls=>mutation=="process"?1:0;
    public Task<SourcesInventoryResult> RunAsync(SourcesInventoryRequest request,CancellationToken token)
    {
        if(fixture.Mode=="cancelled"&&mutation!="cancel")token.ThrowIfCancellationRequested();
        for(var index=0;index<(mutation=="double-plan"?2:fixture.ExpectedPlans);index++)plans.Add(mutation=="recipe"?request.Recipe with{Limit=99}:request.Recipe);
        if(mutation=="omit-plan")plans.Clear();
        if(fixture.ExpectedReads>0)reads.Add((mutation=="read-path"?"wrong":request.AcquisitionManifest.RelativePath,mutation=="read-token"?CancellationToken.None:token));
        if(mutation=="early-read"&&reads.Count==0)reads.Add((request.AcquisitionManifest.RelativePath,token));
        if(fixture.ExpectedResumes>0)resumes.Add((new(new(mutation=="generation"?"wrong":fixture.Acquired.Receipt.GenerationId,
            mutation=="receipt-path"?"wrong":request.AcquisitionManifest.RelativePath,mutation=="projection"?fixture.Acquired.Receipt.Projection with{Release="wrong"}:fixture.Planned.Plan!.SourceProjection!,
            mutation=="policy"?SourceBatchCachePolicy.ImportLockedHistorical:fixture.Acquired.Receipt.CachePolicy),mutation=="required-pin"?request.AcquisitionManifest with{Sha256="wrong"}:request.AcquisitionManifest),mutation=="resume-token"?CancellationToken.None:token));
        if(mutation=="double-resume"&&resumes.Count>0)resumes.Add(resumes[0]);
        if(mutation=="omit-resume")resumes.Clear();
        if(mutation=="early-resume"&&resumes.Count==0)resumes.Add((new(new(fixture.Acquired.Receipt.GenerationId,request.AcquisitionManifest.RelativePath,fixture.Acquired.Receipt.Projection),request.AcquisitionManifest),token));
        if(fixture.ExpectedWrites>0||mutation=="early-write")writes.Add((new(mutation=="write-path"?"wrong":request.ManifestPath,
            mutation=="write-bytes"?new byte[]{0}:fixture.ExpectedBytes,mutation=="write-media"?"wrong":"application/json"),mutation=="write-token"?CancellationToken.None:token));
        if(mutation=="double-write"&&writes.Count>0)writes.Add(writes[0]);
        if(mutation=="omit-write")writes.Clear();
        if(fixture.Mode=="manifest-missing")throw new FileNotFoundException();
        if(fixture.Mode=="output-failure"&&mutation!="ignore-write-error")throw new IOException("Retain old output");
        if(!fixture.Success&&fixture.Mode is not ("cancelled" or "output-failure")&&mutation!="omit-error")
        {
            if(fixture.Mode.StartsWith("reader-",StringComparison.Ordinal)&&fixture.Mode is not ("reader-not-reused" or "reader-pin"))throw new TransportException(mutation=="error-code"?"wrong":fixture.ErrorCode,"Resume-only rejection");
            throw new SourcesInventoryException(mutation=="error-code"?"wrong":fixture.ErrorCode,mutation=="issues"?[new("wrong","wrong")]:fixture.Mode=="invalid-plan"?fixture.Planned.Issues:[]);
        }
        var pin=new StoredArtifact(request.ManifestPath,ContentHash.Sha256(fixture.ExpectedBytes),fixture.ExpectedBytes.Length,"application/json");
        return Task.FromResult(new SourcesInventoryResult(replacement??fixture.ExpectedReceipt,mutation switch{
            "pin-path"=>pin with{RelativePath="wrong"},"pin-hash"=>pin with{Sha256="wrong"},"pin-length"=>pin with{Length=-1},"pin-media"=>pin with{MediaType="wrong"},_=>pin}));
    }
}

public static class SourcesInventoryMetadataMutations
{
    // Deserialize each type-valid mutant before invoking the assertion harness, so fixture/codec errors cannot masquerade as rejected assertions.
    public static IEnumerable<SourcesInventoryReceipt> All(SourcesInventoryReceipt expected)
    {
        var root=JsonSerializer.SerializeToNode(expected)!;
        foreach(var path in Paths(root,[]))
        {
            var copy=root.DeepClone();var node=Get(copy,path);
            if(node is JsonArray array){if(array.Count>0)array.RemoveAt(0);else array.Add(EmptyArrayItem(path[^1],expected));}
            else
            {
                JsonNode replacement;
                if(node is null)replacement=NullReplacement(path[^1]);
                else
                {
                    var value=JsonSerializer.SerializeToElement(node);
                    replacement=value.ValueKind switch{
                        JsonValueKind.String=>JsonValue.Create(path[^1]=="RepositoryXml"?"YQ==":value.GetString()!.StartsWith("https://",StringComparison.Ordinal)?"https://other.test/changed":"changed")!,
                        JsonValueKind.Number=>JsonValue.Create(value.GetInt64()+1)!,
                        JsonValueKind.True or JsonValueKind.False=>JsonValue.Create(!value.GetBoolean())!,
                        _=>throw new InvalidOperationException("Unexpected metadata leaf")};
                }
                Set(copy,path,replacement);
            }
            yield return copy.Deserialize<SourcesInventoryReceipt>()??throw new InvalidOperationException("Mutant was null");
        }
    }
    private static JsonNode NullReplacement(string property)=>property switch{
        "VerifiedSource"=>JsonSerializer.SerializeToNode(new AcquiredSource(new("raw/changed",new string('0',64),9,"application/octet-stream"),"reused",new Uri("https://other.test/changed"),new string('a',40),null))!,
        "Archive"=>JsonSerializer.SerializeToNode(new SourceArchivePin(new Uri("https://other.test/changed"),"source.properties"))!,
        "Pin"=>JsonSerializer.SerializeToNode(SourceBatchCases.CachePin)!,
        "SizeBytes" or "Length" or "CacheAliasIndex"=>JsonValue.Create(9)!,
        _=>JsonValue.Create("changed")!};
    private static JsonNode EmptyArrayItem(string property,SourcesInventoryReceipt expected)
    {
        var pin=expected.Acquisition.Receipt.Projection.PhysicalSources[0].Pin;
        var acquired=new AcquiredSource(new(pin.LocalPath,pin.Sha256,3,"application/octet-stream"),"reused",pin.Url,pin.Revision,pin.GitBlob);
        return property switch{
            "RequiredSnapshots"=>JsonSerializer.SerializeToNode(new SnapshotRequirement("added",pin.LocalPath,"bundle/added",pin.Sha256,3))!,
            "Prerequisites"=>JsonSerializer.SerializeToNode(new RehydrationPrerequisite(RehydrationPrerequisiteCode.SourceRevision,[0],pin.LocalPath,"added"))!,
            "HistoricalCaches"=>JsonSerializer.SerializeToNode(new SourceBatchHistoricalCache(0,SourceBatchCases.CachePin,new("cache/a.gz",SourceBatchCases.CachePin.GZipSha256,1,"application/gzip")))!,
            "Outputs"=>JsonSerializer.SerializeToNode(acquired.Artifact)!,
            "Sources"=>JsonSerializer.SerializeToNode(new SourceBatchSource(0,acquired))!,
            "SelectedPhysicalSourceIndices"=>JsonValue.Create(0)!,
            _=>throw new InvalidOperationException("Unclassified empty metadata array: "+property)};
    }
    private static IEnumerable<string[]> Paths(JsonNode? node,string[] prefix)
    {
        if(node is JsonObject obj){foreach(var property in obj)foreach(var child in Paths(property.Value,[..prefix,property.Key]))yield return child;}
        else if(node is JsonArray array)
        {
            yield return prefix;
            for(var index=0;index<array.Count;index++)foreach(var child in Paths(array[index],[..prefix,index.ToString(System.Globalization.CultureInfo.InvariantCulture)]))yield return child;
        }
        else yield return prefix;
    }
    private static JsonNode? Get(JsonNode root,IReadOnlyList<string> path)
    {
        JsonNode? node=root;
        foreach(var part in path)node=node is JsonArray array?array[int.Parse(part,System.Globalization.CultureInfo.InvariantCulture)]:node![part];
        return node;
    }
    private static void Set(JsonNode root,IReadOnlyList<string> path,JsonNode replacement)
    {
        var parent=Get(root,path.Take(path.Count-1).ToArray());
        if(parent is JsonArray array)array[int.Parse(path[^1],System.Globalization.CultureInfo.InvariantCulture)]=replacement;else parent![path[^1]]=replacement;
    }
}

[Trait("Gate","Mock")]
public sealed class SourcesInventoryMockTests
{
    [Theory,MemberData(nameof(SourcesInventoryCase.Cases),MemberType=typeof(SourcesInventoryCase))]
    public Task CorrectRecipeBoundStatusAndProvenance(SourcesInventoryCase test)=>test.AssertCase(new ScriptedSourcesInventoryProbe(test));
    [Theory,MemberData(nameof(SourcesInventoryCase.Cases),MemberType=typeof(SourcesInventoryCase))]
    public async Task EveryMetadataFieldAndEffectMutationIsRejected(SourcesInventoryCase test)
    {
        var mutations=new List<string>{"input","network","process"};
        if(test.Mode=="cancelled")mutations.Add("cancel");
        else
        {
            mutations.AddRange(["recipe","double-plan","omit-plan"]);
            if(test.ExpectedReads>0)mutations.AddRange(["read-path","read-token"]);else mutations.Add("early-read");
            if(test.ExpectedResumes>0)mutations.AddRange(["generation","receipt-path","projection","policy","required-pin","resume-token","double-resume","omit-resume"]);else mutations.Add("early-resume");
            if(test.ExpectedWrites>0)mutations.AddRange(["write-path","write-bytes","write-media","write-token","double-write","omit-write"]);else mutations.Add("early-write");
            if(test.Success)mutations.AddRange(["pin-path","pin-hash","pin-length","pin-media"]);
            else if(test.Mode=="output-failure")mutations.Add("ignore-write-error");
            else if(test.Mode!="manifest-missing")
            {
                mutations.AddRange(["omit-error","error-code"]);
                if(test.Mode is not ("reader-missing" or "reader-replaced" or "reader-output-missing"))mutations.Add("issues");
            }
        }
        foreach(var mutation in mutations)Assert.NotNull(await Record.ExceptionAsync(()=>test.AssertCase(new ScriptedSourcesInventoryProbe(test,mutation))));
        if(test.Success)foreach(var replacement in SourcesInventoryMetadataMutations.All(test.ExpectedReceipt))
            Assert.NotNull(await Record.ExceptionAsync(()=>test.AssertCase(new ScriptedSourcesInventoryProbe(test,replacement:replacement))));
    }
}
