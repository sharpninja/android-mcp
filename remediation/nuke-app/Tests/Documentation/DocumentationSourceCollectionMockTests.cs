using System.Text;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public sealed record DocumentationCaptureCase(string Id,int AbsentCanonicalMask=0,string? MissingId=null,string? ChangedPinId=null,bool FailRead=false,bool Cancel=false)
{public override string ToString()=>Id;}
public sealed record DocumentationVerifyCase(string Mutation,string? ArtifactId=null,int AbsentCanonicalMask=0)
{public override string ToString()=>Mutation+":"+ArtifactId;}
public sealed class SourceCollectionEnvironment
{
    public Artifact Pins {get;}=DocumentationModelSourceCases.Original();
    public DocumentationModelSourcePlan Plan {get;}=DocumentationModelSourceCases.Expected();
    public List<string> Reads {get;}=[];
    public List<CancellationToken> Tokens {get;}=[];
    public CancellationTokenSource Cancellation {get;}=new();
    public Dictionary<string,byte[]> ProjectBytes {get;}=new(StringComparer.Ordinal);
    public Dictionary<string,byte[]> CanonicalBytes {get;}=new(StringComparer.Ordinal);
    public RecordingCollectionStore Project {get;}
    public RecordingCollectionStore Catalog {get;}
    public RecordingCollectionPlanner Planner {get;}
    public IArtifactReceiptVerifier Verifier {get;}=new ArtifactReceiptVerifier(new NativeContentHash());
    public DocumentationCaptureCase Case {get;}
    public string? ReadFailureId {get;set;}
    public int Writes {get;set;}
    public string? VerifyError {get;set;}
    public string? VerifyErrorId {get;set;}
    public DocumentationModelSourceSnapshot Expected {get;}
    public SourceCollectionEnvironment(DocumentationCaptureCase test)
    {
        Case=test;Project=new(this,false);Catalog=new(this,true);Planner=new(this);
        foreach(var source in Plan.Sources)
        {
            byte[] bytes;
            if(source.ContractId is {} id)
            {
                var contract=DocumentationModelSourceCases.Contracts.Single(contract=>contract.Id==id);
                bytes=ModelContractFixture.Current.Copies.Single(copy=>copy.File==contract.File).Snapshot.Bytes.ToArray();
                Assert.Equal(source.ExpectedSha256,DocumentationFixture.Hash(bytes));
            }
            else bytes=source.RelativePath==Pins.RelativePath?Pins.Bytes.ToArray():Encoding.UTF8.GetBytes("independent fixture:"+source.ArtifactId);
            Store(source)[source.RelativePath]=bytes;
        }
        for(var index=0;index<3;index++)if((test.AbsentCanonicalMask&(1<<index))!=0)
        {var spec=Plan.Sources.Single(source=>source.ArtifactId=="canonical:"+DocumentationModelSourceCases.Contracts[index].Id);Store(spec).Remove(spec.RelativePath);}
        // Expected facts precede failure mutations and never come from the collector under test.
        Expected=ExpectedSnapshot();
        if(test.MissingId is {} missing){var source=Spec(missing);Store(source).Remove(source.RelativePath);}
        if(test.ChangedPinId is {} changed){var source=Spec(changed);Store(source)[source.RelativePath]=Encoding.UTF8.GetBytes("pin mismatch");}
        if(test.FailRead)ReadFailureId=Plan.Sources[0].ArtifactId;
        if(test.Cancel)Cancellation.Cancel();
    }
    public DocumentationModelSourceSpec Spec(string id)=>Plan.Sources.Single(source=>source.ArtifactId==id);
    public Dictionary<string,byte[]> Store(DocumentationModelSourceSpec source)=>source.Role==DocumentationInputRole.CanonicalContractOptional?CanonicalBytes:ProjectBytes;
    public DocumentationModelSourceSnapshot ExpectedSnapshot()
    {
        var sources=Plan.Sources.Select(source=>
        {
            var present=Store(source).TryGetValue(source.RelativePath,out var bytes);
            Artifact? content=present?new Artifact(source.RelativePath,bytes!.ToArray(),source.MediaType):null;
            return new DocumentationCapturedSource(new(source,content),content is null?null:Verifier.Describe(content));
        }).ToArray();
        return new(sources,ExpectedReceipt(sources));
    }
    public static Artifact ExpectedReceipt(IReadOnlyList<DocumentationCapturedSource> sources)
    {
        var root=new JsonObject{["schema_version"]="1.0.0",["kind"]="android-mcp-model-sources",["sources"]=new JsonArray(sources.Select(row=>
        {
            var source=row.Observation.Source;return (JsonNode)new JsonObject{["artifact_id"]=source.ArtifactId,["role"]=source.Role.ToString(),["contract_id"]=source.ContractId?.ToString(),["relative_path"]=source.RelativePath,["media_type"]=source.MediaType,["present"]=row.Observation.Content is not null,["sha256"]=row.Identity?.Sha256,["size_bytes"]=row.Identity is {} identity?JsonValue.Create(identity.Length):null};
        }).ToArray())};
        return new("validation/model-sources.json",new CanonicalJson().Serialize(Encoding.UTF8.GetBytes(root.ToJsonString())),"application/json");
    }
}
public sealed class RecordingCollectionPlanner(SourceCollectionEnvironment environment):IDocumentationModelSourcePlanner
{
    public int Calls {get;private set;}
    public DocumentationModelSourcePlan Plan(Artifact pins){Calls++;Assert.Same(environment.Pins,pins);return environment.Plan;}
}
public sealed class RecordingCollectionStore(SourceCollectionEnvironment environment,bool canonical):IArtifactStore
{
    public Task<ReadOnlyMemory<byte>> ReadAsync(string path,CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var source=environment.Plan.Sources.Single(source=>source.RelativePath==path&&(source.Role==DocumentationInputRole.CanonicalContractOptional)==canonical);
        environment.Reads.Add(source.ArtifactId);environment.Tokens.Add(cancellationToken);
        if(environment.ReadFailureId==source.ArtifactId)throw new IOException("Scripted source read failure");
        return environment.Store(source).TryGetValue(path,out var bytes)?Task.FromResult<ReadOnlyMemory<byte>>(bytes):Task.FromException<ReadOnlyMemory<byte>>(new FileNotFoundException(path));
    }
    public Task<StoredArtifact> WriteAsync(Artifact artifact,CancellationToken cancellationToken=default)
    {environment.Writes++;throw new InvalidOperationException("Read-only source collector attempted a write");}
}
public static class DocumentationSourceCollectionCases
{
    public static IEnumerable<object[]> CaptureCases
    {
        get
        {
            for(var mask=0;mask<8;mask++)yield return [new DocumentationCaptureCase("CAPTURE-OPTIONAL-MASK-"+mask,mask)];
            foreach(var source in DocumentationModelSourceCases.Expected().Sources.Where(source=>source.Role!=DocumentationInputRole.CanonicalContractOptional))yield return [new DocumentationCaptureCase("CAPTURE-MISSING-"+source.ArtifactId,MissingId:source.ArtifactId)];
            foreach(var source in DocumentationModelSourceCases.Expected().Sources.Where(source=>source.ExpectedSha256 is not null))yield return [new DocumentationCaptureCase("CAPTURE-PIN-"+source.ArtifactId,ChangedPinId:source.ArtifactId)];
            yield return [new DocumentationCaptureCase("CAPTURE-PINS-ADMISSION-CHANGED",ChangedPinId:"project:contracts/source-pins.json")];
            yield return [new DocumentationCaptureCase("CAPTURE-READ-FAILURE",FailRead:true)];yield return [new DocumentationCaptureCase("CAPTURE-CANCEL",Cancel:true)];
        }
    }
    public static IEnumerable<object[]> VerifyCases
    {
        get
        {
            yield return [new DocumentationVerifyCase("unchanged")];yield return [new DocumentationVerifyCase("unchanged",AbsentCanonicalMask:7)];
            foreach(var source in DocumentationModelSourceCases.Expected().Sources)
            {
                yield return [new DocumentationVerifyCase("bytes",source.ArtifactId)];
                yield return [new DocumentationVerifyCase("removed",source.ArtifactId)];
                yield return [new DocumentationVerifyCase("snapshot-route",source.ArtifactId)];
            }
            for(var index=0;index<3;index++)yield return [new DocumentationVerifyCase("appeared","canonical:"+DocumentationModelSourceCases.Contracts[index].Id,1<<index)];
            yield return [new DocumentationVerifyCase("read-failure",DocumentationModelSourceCases.Expected().Sources[0].ArtifactId)];
            yield return [new DocumentationVerifyCase("cancel")];
        }
    }
    public static IEnumerable<object[]> FieldControls=>Enumerable.Range(0,15).SelectMany(index=>new[]{"source-id","source-role","source-contract","source-path","source-media","source-pin","content-presence","content-path","content-media","content-bytes","identity-path","identity-hash","identity-length","identity-media"}.Select(memberName=>new object[]{index,memberName}));
    public static DocumentationModelSourceSnapshot WrongField(DocumentationModelSourceSnapshot original,int index,string memberName)
    {
        var rows=original.Sources.ToArray();var row=rows[index];var source=row.Observation.Source;var content=row.Observation.Content!;var identity=row.Identity!;
        var spec=memberName switch
        {
            "source-id"=>source with{ArtifactId="unadmitted"},"source-role"=>source with{Role=(DocumentationInputRole)(((int)source.Role+1)%3)},
            "source-contract"=>source with{ContractId=source.ContractId==DocumentationContractId.OpenApi?DocumentationContractId.ApiContract:DocumentationContractId.OpenApi},
            "source-path"=>source with{RelativePath="../unadmitted"},"source-media"=>source with{MediaType="wrong"},"source-pin"=>source with{ExpectedSha256=source.ExpectedSha256 is null?new string('1',64):null},_=>source
        };
        Artifact? bytes=memberName switch{"content-presence"=>null,"content-path"=>content with{RelativePath="wrong"},"content-media"=>content with{MediaType="wrong"},"content-bytes"=>content with{Bytes=new byte[]{0}},_=>content};
        var metadata=memberName switch{"identity-path"=>identity with{RelativePath="wrong"},"identity-hash"=>identity with{Sha256=new string('0',64)},"identity-length"=>identity with{Length=identity.Length+1},"identity-media"=>identity with{MediaType="wrong"},_=>identity};
        rows[index]=new(new(spec,bytes),metadata);return original with{Sources=rows};
    }
    public static async Task AssertCapture(SourceCollectionEnvironment e,IDocumentationSourceCollection provider)
    {
        var expectedError=e.Case.MissingId is not null?"MODEL_SOURCE_MISSING":e.Case.ChangedPinId is not null?"MODEL_SOURCE_PIN_MISMATCH":e.Case.FailRead?"MODEL_SOURCE_READ":null;
        if(e.Case.Cancel){await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>provider.CaptureAsync(e.Cancellation.Token));Assert.Empty(e.Reads);}
        else if(expectedError is not null)
        {
            var error=await Assert.ThrowsAsync<DocumentationSourceCollectionException>(()=>provider.CaptureAsync(e.Cancellation.Token));Assert.Equal(expectedError,error.Code);
            Assert.Equal(new[]{e.Case.MissingId??e.Case.ChangedPinId??e.Plan.Sources[0].ArtifactId},error.ArtifactIds);
        }
        else
        {
            var actual=await provider.CaptureAsync(e.Cancellation.Token);AssertSnapshot(e.Expected,actual);
            Assert.Equal(e.Plan.Sources.Select(source=>source.ArtifactId),e.Reads);
            // A store can retain a mutable buffer; a captured source must not alias it.
            var first=e.Plan.Sources[0];var store=e.Store(first);store[first.RelativePath][0]^=1;
            Assert.Equal(e.Expected.Sources[0].Observation.Content!.Bytes.ToArray(),actual.Sources[0].Observation.Content!.Bytes.ToArray());
            store[first.RelativePath][0]^=1;
        }
        Assert.Equal(1,e.Planner.Calls);Assert.Equal(0,e.Writes);Assert.All(e.Tokens,token=>Assert.Equal(e.Cancellation.Token,token));
        if(e.Case.MissingId is not null)Assert.Equal(e.Plan.Sources.Select(source=>source.ArtifactId),e.Reads);
    }
    public static void AssertSnapshot(DocumentationModelSourceSnapshot expected,DocumentationModelSourceSnapshot actual)
    {
        Assert.Equal(expected.Sources.Count,actual.Sources.Count);
        for(var index=0;index<expected.Sources.Count;index++)
        {
            var left=expected.Sources[index];var right=actual.Sources[index];Assert.Equal(left.Observation.Source,right.Observation.Source);Assert.Equal(left.Identity,right.Identity);
            if(left.Observation.Content is null)Assert.Null(right.Observation.Content);
            else{Assert.NotNull(right.Observation.Content);Assert.Equal(left.Observation.Content.RelativePath,right.Observation.Content.RelativePath);Assert.Equal(left.Observation.Content.MediaType,right.Observation.Content.MediaType);Assert.Equal(left.Observation.Content.Bytes.ToArray(),right.Observation.Content.Bytes.ToArray());}
        }
        Assert.Equal(expected.Receipt.RelativePath,actual.Receipt.RelativePath);Assert.Equal(expected.Receipt.MediaType,actual.Receipt.MediaType);Assert.Equal(expected.Receipt.Bytes.ToArray(),actual.Receipt.Bytes.ToArray());
    }
    public static async Task AssertVerify(DocumentationVerifyCase test,SourceCollectionEnvironment e,IDocumentationSourceCollection provider)
    {
        var captured=await provider.CaptureAsync(e.Cancellation.Token);AssertSnapshot(e.Expected,captured);var originalReceipt=captured.Receipt.Bytes.ToArray();
        e.Reads.Clear();e.Tokens.Clear();
        if(test.Mutation is "bytes" or "removed" or "appeared" or "read-failure")
        {
            e.VerifyError="MODEL_SOURCE_CHANGED";e.VerifyErrorId=test.ArtifactId;var source=e.Spec(test.ArtifactId!);
            if(test.Mutation=="bytes")e.Store(source)[source.RelativePath]=Encoding.UTF8.GetBytes("changed after preparation");
            if(test.Mutation=="removed")e.Store(source).Remove(source.RelativePath);
            if(test.Mutation=="appeared")
            {
                var snapshot=e.Plan.Sources.Single(item=>item.Role==DocumentationInputRole.ContractSnapshotRequired&&item.ContractId==source.ContractId);
                e.Store(source)[source.RelativePath]=e.Store(snapshot)[snapshot.RelativePath].ToArray();
            }
            if(test.Mutation=="read-failure")e.ReadFailureId=source.ArtifactId;
        }
        if(test.Mutation=="snapshot-route")
        {
            e.VerifyError="MODEL_SOURCE_SNAPSHOT";e.VerifyErrorId=test.ArtifactId;
            var rows=captured.Sources.ToArray();var index=Array.FindIndex(rows,row=>row.Observation.Source.ArtifactId==test.ArtifactId);
            rows[index]=rows[index] with{Observation=rows[index].Observation with{Source=rows[index].Observation.Source with{RelativePath="../unapproved"}}};
            captured=captured with{Sources=rows};
        }
        if(test.Mutation=="cancel")e.Cancellation.Cancel();
        if(test.Mutation=="cancel")await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>provider.VerifyUnchangedAsync(captured,e.Cancellation.Token));
        else if(e.VerifyError is {} expected)
        {
            var error=await Assert.ThrowsAsync<DocumentationSourceCollectionException>(()=>provider.VerifyUnchangedAsync(captured,e.Cancellation.Token));Assert.Equal(expected,error.Code);Assert.Equal<string?>(new[]{test.ArtifactId},error.ArtifactIds);
        }
        else await provider.VerifyUnchangedAsync(captured,e.Cancellation.Token);
        Assert.Equal(originalReceipt,captured.Receipt.Bytes.ToArray());Assert.Equal(1,e.Planner.Calls);Assert.Equal(0,e.Writes);
        Assert.All(e.Tokens,token=>Assert.Equal(e.Cancellation.Token,token));
        if(test.Mutation is "snapshot-route" or "cancel")Assert.Empty(e.Reads);
        else if(test.Mutation!="read-failure")Assert.Equal(e.Plan.Sources.Select(source=>source.ArtifactId),e.Reads);
    }
}
public sealed class ScriptedDocumentationSourceCollection:IDocumentationSourceCollection
{
    private readonly SourceCollectionEnvironment e;private readonly string mutation;
    public ScriptedDocumentationSourceCollection(SourceCollectionEnvironment environment,string incorrect=""){e=environment;mutation=incorrect;_ =e.Planner.Plan(e.Pins);}
    public async Task<DocumentationModelSourceSnapshot> CaptureAsync(CancellationToken cancellationToken=default)
    {
        if(e.Case.Cancel&&mutation=="outcome")throw new DocumentationSourceCollectionException("WRONG",[]);
        cancellationToken.ThrowIfCancellationRequested();var missing=new List<string>();
        foreach(var source in e.Plan.Sources)
        {
            if(mutation=="skip-read"&&source==e.Plan.Sources[^1])continue;
            try{await (source.Role==DocumentationInputRole.CanonicalContractOptional?e.Catalog:e.Project).ReadAsync(source.RelativePath,mutation=="read-token"?CancellationToken.None:cancellationToken);}
            catch(FileNotFoundException){if(source.Role!=DocumentationInputRole.CanonicalContractOptional)missing.Add(source.ArtifactId);}
            catch(IOException){throw new DocumentationSourceCollectionException(mutation=="outcome"?"WRONG":"MODEL_SOURCE_READ",[source.ArtifactId]);}
        }
        if(missing.Count>0)throw new DocumentationSourceCollectionException(mutation=="outcome"?"WRONG":"MODEL_SOURCE_MISSING",missing);
        if(e.Case.ChangedPinId is {} changed)throw new DocumentationSourceCollectionException(mutation=="outcome"?"WRONG":"MODEL_SOURCE_PIN_MISMATCH",[changed]);
        if(mutation=="outcome")throw new DocumentationSourceCollectionException("WRONG",[]);
        if(mutation=="replan")_ =e.Planner.Plan(e.Pins);
        if(mutation=="write-attempt")e.Writes++;
        if(mutation=="mutable-buffer")return e.Expected with{Sources=e.Expected.Sources.Select(row=>row with{Observation=row.Observation with{Content=row.Observation.Content is {} content?content with{Bytes=e.Store(row.Observation.Source)[content.RelativePath]}:null}}).ToArray()};
        return e.Expected;
    }
    public async Task VerifyUnchangedAsync(DocumentationModelSourceSnapshot snapshot,CancellationToken cancellationToken=default)
    {
        if(cancellationToken.IsCancellationRequested&&mutation=="verify-outcome")throw new DocumentationSourceCollectionException("WRONG",[]);
        cancellationToken.ThrowIfCancellationRequested();
        if(e.VerifyError=="MODEL_SOURCE_SNAPSHOT")throw new DocumentationSourceCollectionException(mutation=="verify-outcome"?"WRONG":e.VerifyError,[e.VerifyErrorId!]);
        foreach(var source in e.Plan.Sources)
        {
            try{await (source.Role==DocumentationInputRole.CanonicalContractOptional?e.Catalog:e.Project).ReadAsync(source.RelativePath,cancellationToken);}
            catch(FileNotFoundException){}
            catch(IOException){throw new DocumentationSourceCollectionException(mutation=="verify-outcome"?"WRONG":"MODEL_SOURCE_CHANGED",[source.ArtifactId]);}
        }
        if(e.VerifyError is {} code)throw new DocumentationSourceCollectionException(mutation=="verify-outcome"?"WRONG":code,[e.VerifyErrorId!]);
        if(mutation=="verify-outcome")throw new DocumentationSourceCollectionException("WRONG",[]);
    }
}
[Trait("Gate","Mock")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class DocumentationSourceCollectionMockTests
{
    [Theory,MemberData(nameof(DocumentationSourceCollectionCases.CaptureCases),MemberType=typeof(DocumentationSourceCollectionCases))]
    public Task CorrectCaptureAndExplicitAbsenceMocksPass(DocumentationCaptureCase test){var e=new SourceCollectionEnvironment(test);return DocumentationSourceCollectionCases.AssertCapture(e,new ScriptedDocumentationSourceCollection(e));}
    [Theory,MemberData(nameof(DocumentationSourceCollectionCases.CaptureCases),MemberType=typeof(DocumentationSourceCollectionCases))]
    public async Task EveryWrongCaptureOutcomeIsRejected(DocumentationCaptureCase test)
    {
        var e=new SourceCollectionEnvironment(test);Assert.NotNull(await Record.ExceptionAsync(()=>DocumentationSourceCollectionCases.AssertCapture(e,new ScriptedDocumentationSourceCollection(e,"outcome"))));
    }
    [Theory,MemberData(nameof(DocumentationSourceCollectionCases.VerifyCases),MemberType=typeof(DocumentationSourceCollectionCases))]
    public Task CorrectCommitTimeContentAndPresenceMocksPass(DocumentationVerifyCase test)
    {var e=new SourceCollectionEnvironment(new("VERIFY",test.AbsentCanonicalMask));return DocumentationSourceCollectionCases.AssertVerify(test,e,new ScriptedDocumentationSourceCollection(e));}
    [Theory,MemberData(nameof(DocumentationSourceCollectionCases.VerifyCases),MemberType=typeof(DocumentationSourceCollectionCases))]
    public async Task EveryWrongCommitTimeOutcomeIsRejected(DocumentationVerifyCase test)
    {var e=new SourceCollectionEnvironment(new("VERIFY",test.AbsentCanonicalMask));Assert.NotNull(await Record.ExceptionAsync(()=>DocumentationSourceCollectionCases.AssertVerify(test,e,new ScriptedDocumentationSourceCollection(e,"verify-outcome"))));}
    [Theory,MemberData(nameof(DocumentationSourceCollectionCases.FieldControls),MemberType=typeof(DocumentationSourceCollectionCases))]
    public void EveryCapturedDescriptorContentAndIdentityFieldRejectsDrift(int index,string memberName)
    {
        var e=new SourceCollectionEnvironment(new("FIELD"));var wrong=DocumentationSourceCollectionCases.WrongField(e.Expected,index,memberName);
        Assert.NotNull(Record.Exception(()=>DocumentationSourceCollectionCases.AssertSnapshot(e.Expected,wrong)));
    }
    [Theory]
    [InlineData("receipt-path")][InlineData("receipt-media")][InlineData("receipt-bytes")][InlineData("row-missing")][InlineData("row-duplicate")][InlineData("row-order")]
    public void EveryReceiptAndObservationSetDriftIsRejected(string mutation)
    {
        var e=new SourceCollectionEnvironment(new("FIELD"));var wrong=e.Expected;
        if(mutation=="receipt-path")wrong=wrong with{Receipt=wrong.Receipt with{RelativePath="wrong"}};
        if(mutation=="receipt-media")wrong=wrong with{Receipt=wrong.Receipt with{MediaType="wrong"}};
        if(mutation=="receipt-bytes")wrong=wrong with{Receipt=wrong.Receipt with{Bytes=new byte[]{0}}};
        if(mutation=="row-missing")wrong=wrong with{Sources=wrong.Sources.Skip(1).ToArray()};
        if(mutation=="row-duplicate")wrong=wrong with{Sources=wrong.Sources.Append(wrong.Sources[0]).ToArray()};
        if(mutation=="row-order")wrong=wrong with{Sources=wrong.Sources.Reverse().ToArray()};
        Assert.NotNull(Record.Exception(()=>DocumentationSourceCollectionCases.AssertSnapshot(e.Expected,wrong)));
    }
    [Theory]
    [InlineData("read-token")][InlineData("skip-read")][InlineData("replan")][InlineData("write-attempt")][InlineData("mutable-buffer")]
    public async Task EachCollectorInteractionDriftIsRejected(string mutation)
    {var e=new SourceCollectionEnvironment(new("INTERACTION"));Assert.NotNull(await Record.ExceptionAsync(()=>DocumentationSourceCollectionCases.AssertCapture(e,new ScriptedDocumentationSourceCollection(e,mutation))));}
    [Theory]
    [InlineData(0)][InlineData(1)][InlineData(2)]
    public void AnAbsentCanonicalObservationCannotBecomeAnEmptyPresentArtifact(int index)
    {
        var e=new SourceCollectionEnvironment(new("ABSENT",1<<index));var rows=e.Expected.Sources.ToArray();var position=12+index;var source=rows[position].Observation.Source;
        rows[position]=new(new(source,new Artifact(source.RelativePath,Array.Empty<byte>(),source.MediaType)),null);
        Assert.NotNull(Record.Exception(()=>DocumentationSourceCollectionCases.AssertSnapshot(e.Expected,e.Expected with{Sources=rows})));
    }
}
