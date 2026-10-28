using System.Text;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public enum AssetScenario
{
    Success,MissingBlueprint,MissingDesign,BothMissing,ProjectionFailure,DuplicatePath,CaseCollision,
    ReservedManifest,UnsafePath,ExistingFinalOutput,ExistingManifest,ProbePermission,WriteFailure,
    BadDigest,BadLength,BadPath,BadMedia,InputChanged,CancelBeforeLease,CancelPreflight,CancelWrite,CancelBeforeManifest,SameStore
}
public sealed record AssetCase(AssetScenario Scenario,string Error)
{
    public override string ToString()=>Scenario.ToString();
}

public sealed class AssetTestEnvironment
{
    public const string Generation="fixture-generation-001";
    public const string RootIdentity="fixture-output-root";
    public List<string> Trace {get;}=[];
    public CancellationTokenSource Cancellation {get;}=new();
    public AssetCase Case {get;}
    public RecordingAssetStore Source {get;}
    public RecordingAssetStore Target {get;}
    public RecordingAssetLease Leases {get;}
    public RecordingAssetProjector Projector {get;}
    public IContentHash Hashes {get;}=new NativeContentHash();
    public Artifact[] Data {get;}
    public string[] PlannedPaths=>Data.Select(a=>a.RelativePath).Append(DocumentationAssetPaths.Manifest).Order(StringComparer.Ordinal).ToArray();
    public AssetTestEnvironment(AssetCase test)
    {
        Case=test;var fixture=DocumentationFixture.Current;
        if(test.Scenario==AssetScenario.CancelBeforeLease)Cancellation.Cancel();
        Source=new(this,"source");Target=new(this,"target");Leases=new(this);
        Source.Bytes.Add(DocumentationAssetPaths.Blueprint,fixture.Input.BlueprintMarkdown.Bytes.ToArray());
        Source.Bytes.Add(DocumentationAssetPaths.Design,fixture.Input.DesignMarkdown.Bytes.ToArray());
        if(test.Scenario is AssetScenario.MissingBlueprint or AssetScenario.BothMissing)Source.Bytes.Remove(DocumentationAssetPaths.Blueprint);
        if(test.Scenario is AssetScenario.MissingDesign or AssetScenario.BothMissing)Source.Bytes.Remove(DocumentationAssetPaths.Design);
        Data=new[]{"docs/Android MCP Bridge Design.md","docs/screens/SCR01.svg","docs/storyboards.html"}.Select(p=>DocumentationFixture.Get(fixture.Correct,p)).OrderBy(a=>a.RelativePath,StringComparer.Ordinal).ToArray();
        if(test.Scenario==AssetScenario.ExistingFinalOutput)Target.Bytes[Data[^1].RelativePath]=[1,2,3];
        if(test.Scenario==AssetScenario.ExistingManifest)Target.Bytes[DocumentationAssetPaths.Manifest]=[1,2,3];
        Projector=new(this);
    }
    public StoredArtifact Metadata(Artifact a)=>new(a.RelativePath,DocumentationFixture.Hash(a.Bytes.Span),a.Bytes.Length,a.MediaType);
    public StoredArtifact[] Inputs=>new[]{DocumentationFixture.Current.Input.BlueprintMarkdown,DocumentationFixture.Current.Input.DesignMarkdown}.Select(a=>Metadata(a with{MediaType="text/markdown"})).ToArray();
    public JsonObject ExpectedManifest()=>new()
    {
        ["schema_version"]="1.0.0",["kind"]="android-mcp-design-generation",["generation_id"]=Generation,
        ["inputs"]=Rows(Inputs),["artifacts"]=Rows(Data.Select(Metadata))
    };
    public static JsonArray Rows(IEnumerable<StoredArtifact> rows)=>new(rows.Select(r=>(JsonNode?)new JsonObject{{"path",r.RelativePath},{"sha256",r.Sha256},{"size_bytes",r.Length},{"media_type",r.MediaType}}).ToArray());
}

public sealed class RecordingAssetStore(AssetTestEnvironment environment,string role) : IArtifactStore
{
    public Dictionary<string,byte[]> Bytes {get;}=new(StringComparer.Ordinal);
    public List<Artifact> Writes {get;}=[];
    public List<string> Reads {get;}=[];
    public int WriteAttempts {get;private set;}
    public Task<ReadOnlyMemory<byte>> ReadAsync(string path,CancellationToken cancellationToken=default)
    {
        environment.Trace.Add(role+".read:"+path);Reads.Add(path);
        if(role=="target"&&environment.Case.Scenario==AssetScenario.CancelPreflight)environment.Cancellation.Cancel();
        cancellationToken.ThrowIfCancellationRequested();
        if(path.Contains("..",StringComparison.Ordinal))throw new ArgumentException("Shared store rejected unsafe fixture path");
        if(role=="target"&&environment.Case.Scenario==AssetScenario.ProbePermission&&path==environment.PlannedPaths[^1])throw new IOException("Permission fixture");
        if(role=="source"&&environment.Case.Scenario==AssetScenario.InputChanged&&Reads.Count>2)return Task.FromResult<ReadOnlyMemory<byte>>(Encoding.UTF8.GetBytes("external concurrent change"));
        return Bytes.TryGetValue(path,out var value)?Task.FromResult<ReadOnlyMemory<byte>>(value):Task.FromException<ReadOnlyMemory<byte>>(new FileNotFoundException(path));
    }
    public Task<StoredArtifact> WriteAsync(Artifact artifact,CancellationToken cancellationToken=default)
    {
        environment.Trace.Add(role+".write:"+artifact.RelativePath);WriteAttempts++;
        if(role=="source")throw new InvalidOperationException("Canonical source writes forbidden");
        if(environment.Case.Scenario==AssetScenario.CancelWrite)environment.Cancellation.Cancel();cancellationToken.ThrowIfCancellationRequested();
        if(environment.Case.Scenario==AssetScenario.WriteFailure&&WriteAttempts==2)throw new IOException("Write fixture");
        if(Bytes.ContainsKey(artifact.RelativePath))throw new IOException("Immutable generation collision");
        Writes.Add(artifact);Bytes.Add(artifact.RelativePath,artifact.Bytes.ToArray());var receipt=environment.Metadata(artifact);
        if(environment.Case.Scenario==AssetScenario.CancelBeforeManifest&&Writes.Count==environment.Data.Length)environment.Cancellation.Cancel();
        if(WriteAttempts==1)receipt=environment.Case.Scenario switch{AssetScenario.BadDigest=>receipt with{Sha256="wrong"},AssetScenario.BadLength=>receipt with{Length=-1},AssetScenario.BadPath=>receipt with{RelativePath="wrong"},AssetScenario.BadMedia=>receipt with{MediaType="wrong"},_=>receipt};
        return Task.FromResult(receipt);
    }
}
public sealed class RecordingAssetLease(AssetTestEnvironment environment) : IArtifactLeaseProvider
{
    public List<string> Scopes {get;}=[];
    public ValueTask<IAsyncDisposable> AcquireAsync(string scope,CancellationToken cancellationToken=default){cancellationToken.ThrowIfCancellationRequested();environment.Trace.Add("lease.acquire");Scopes.Add(scope);return ValueTask.FromResult<IAsyncDisposable>(new Lease(environment));}
    private sealed class Lease(AssetTestEnvironment environment):IAsyncDisposable{public ValueTask DisposeAsync(){environment.Trace.Add("lease.release");return ValueTask.CompletedTask;}}
}
public sealed class RecordingAssetProjector(AssetTestEnvironment environment):IDocumentationProjector
{
    public int Calls {get;private set;}
    public DocumentationInput? Received {get;set;}
    public DocumentationProjection Project(DocumentationInput input)
    {
        environment.Trace.Add("project");Calls++;Received=input;
        if(environment.Case.Scenario==AssetScenario.ProjectionFailure)throw new InvalidDataException("Malformed fixture input");
        return environment.Case.Scenario switch
        {
            AssetScenario.DuplicatePath=>new(environment.Data.Concat([environment.Data[0]]).ToArray()),
            AssetScenario.CaseCollision=>new(environment.Data.Concat([environment.Data[0] with{RelativePath=environment.Data[0].RelativePath.ToUpperInvariant()}]).ToArray()),
            AssetScenario.ReservedManifest=>new(environment.Data.Concat([new Artifact(DocumentationAssetPaths.Manifest,new byte[]{1},"application/json")]).ToArray()),
            AssetScenario.UnsafePath=>new(environment.Data.Concat([new Artifact("../escape.svg",new byte[]{1},"image/svg+xml")]).ToArray()),
            _=>new(environment.Data)
        };
    }
}

public static class DocumentationAssetCases
{
    public static IEnumerable<object[]> Cases=>Enum.GetValues<AssetScenario>().Select(s=>new object[]{new AssetCase(s,s switch
    {
        AssetScenario.Success=>"",AssetScenario.MissingBlueprint or AssetScenario.MissingDesign or AssetScenario.BothMissing=>"INPUT_MISSING",
        AssetScenario.ProjectionFailure=>"PROJECTION_FAILED",AssetScenario.DuplicatePath or AssetScenario.CaseCollision=>"DUPLICATE_OUTPUT",
        AssetScenario.ReservedManifest=>"RESERVED_OUTPUT",AssetScenario.UnsafePath=>"UNSAFE_OUTPUT",AssetScenario.ExistingFinalOutput or AssetScenario.ExistingManifest=>"OUTPUT_EXISTS",
        AssetScenario.ProbePermission=>"OUTPUT_PREFLIGHT_FAILED",AssetScenario.WriteFailure=>"WRITE_FAILED",
        AssetScenario.BadDigest or AssetScenario.BadLength or AssetScenario.BadPath or AssetScenario.BadMedia=>"STORED_RECEIPT_MISMATCH",
        AssetScenario.InputChanged=>"INPUT_CHANGED",AssetScenario.CancelBeforeLease or AssetScenario.CancelPreflight or AssetScenario.CancelWrite or AssetScenario.CancelBeforeManifest=>"CANCELLED",AssetScenario.SameStore=>"SOURCE_OUTPUT_STORE",_=>throw new ArgumentOutOfRangeException()
    })});

    public static async Task AssertContract(AssetCase test,AssetTestEnvironment environment,IDocumentationAssetOperation provider)
    {
        var before=environment.Source.Bytes.ToDictionary(p=>p.Key,p=>DocumentationFixture.Hash(p.Value),StringComparer.Ordinal);
        DocumentationGeneration? result=null;var exception=await Record.ExceptionAsync(async()=>result=await provider.GenerateAsync(AssetTestEnvironment.Generation,environment.Cancellation.Token));
        if(test.Error=="CANCELLED")Assert.IsAssignableFrom<OperationCanceledException>(exception);
        else if(test.Error.Length>0)Assert.Equal(test.Error,Assert.IsType<DocumentationAssetException>(exception).Code);
        else Assert.Null(exception);
        Assert.Equal(0,environment.Source.WriteAttempts);
        Assert.Equal(before.OrderBy(p=>p.Key,StringComparer.Ordinal),environment.Source.Bytes.Select(p=>new KeyValuePair<string,string>(p.Key,DocumentationFixture.Hash(p.Value))).OrderBy(p=>p.Key,StringComparer.Ordinal));
        if(test.Scenario is AssetScenario.SameStore or AssetScenario.CancelBeforeLease){Assert.Empty(environment.Trace);return;}
        Assert.Equal("lease.acquire",environment.Trace[0]);Assert.Equal("lease.release",environment.Trace[^1]);Assert.Single(environment.Leases.Scopes);Assert.Equal("design-assets/"+environment.Hashes.Sha256(Encoding.UTF8.GetBytes(AssetTestEnvironment.RootIdentity)),environment.Leases.Scopes[0]);
        Assert.Equal(new[]{DocumentationAssetPaths.Blueprint,DocumentationAssetPaths.Design},environment.Source.Reads.Take(2));
        if(test.Scenario is AssetScenario.MissingBlueprint or AssetScenario.MissingDesign or AssetScenario.BothMissing){Assert.Equal(0,environment.Projector.Calls);Assert.Empty(environment.Target.Writes);return;}
        Assert.Equal(1,environment.Projector.Calls);
        Assert.True(environment.Projector.Received!.BlueprintMarkdown.Bytes.Span.SequenceEqual(DocumentationFixture.Current.Input.BlueprintMarkdown.Bytes.Span));
        Assert.True(environment.Projector.Received.DesignMarkdown.Bytes.Span.SequenceEqual(DocumentationFixture.Current.Input.DesignMarkdown.Bytes.Span));
        if(test.Error.Length>0){Assert.DoesNotContain(environment.Target.Writes,a=>a.RelativePath==DocumentationAssetPaths.Manifest);Assert.Null(result);}
        if(environment.Target.WriteAttempts>0)
        {
            var firstWrite=environment.Trace.FindIndex(s=>s.StartsWith("target.write:",StringComparison.Ordinal));
            foreach(var path in environment.PlannedPaths)Assert.Contains("target.read:"+path,environment.Trace.Take(firstWrite));
        }
        if(test.Scenario is AssetScenario.ProjectionFailure or AssetScenario.DuplicatePath or AssetScenario.CaseCollision or AssetScenario.ReservedManifest or AssetScenario.UnsafePath or AssetScenario.ExistingFinalOutput or AssetScenario.ExistingManifest or AssetScenario.ProbePermission or AssetScenario.CancelPreflight)Assert.Empty(environment.Target.Writes);
        if(test.Scenario==AssetScenario.WriteFailure)Assert.Single(environment.Target.Writes);
        if(test.Scenario is AssetScenario.BadDigest or AssetScenario.BadLength or AssetScenario.BadPath or AssetScenario.BadMedia)Assert.Single(environment.Target.Writes);
        if(test.Scenario!=AssetScenario.Success)return;
        Assert.NotNull(result);Assert.Equal(AssetTestEnvironment.Generation,result.GenerationId);Assert.Equal(environment.Inputs,result.Inputs);
        Assert.Equal(environment.Data.Select(environment.Metadata),result.Outputs);
        Assert.Equal(environment.Data.Select(a=>a.RelativePath).Append(DocumentationAssetPaths.Manifest),environment.Target.Writes.Select(a=>a.RelativePath));
        Assert.Equal(4,environment.Source.Reads.Count);Assert.Equal(DocumentationAssetPaths.Manifest,environment.Target.Writes[^1].RelativePath);Assert.Equal(environment.Metadata(environment.Target.Writes[^1]),result.Manifest);
        Assert.True(JsonNode.DeepEquals(environment.ExpectedManifest(),JsonNode.Parse(environment.Target.Writes[^1].Bytes.Span)));
    }
}

// Explicit dependency script validates the oracle before production orchestration.
// It drives the recording fakes, rather than fabricating an interaction trace.
public sealed class ScriptedDocumentationAssetOperation(AssetTestEnvironment environment):IDocumentationAssetOperation
{
    public async Task<DocumentationGeneration> GenerateAsync(string generationId,CancellationToken cancellationToken=default)
    {
        if(environment.Case.Scenario==AssetScenario.SameStore)throw new DocumentationAssetException("SOURCE_OUTPUT_STORE","Fixture shared binding");
        await using var lease=await environment.Leases.AcquireAsync("design-assets/"+environment.Hashes.Sha256(Encoding.UTF8.GetBytes(AssetTestEnvironment.RootIdentity)),cancellationToken);
        var input=new List<ReadOnlyMemory<byte>>();var missing=false;
        foreach(var path in new[]{DocumentationAssetPaths.Blueprint,DocumentationAssetPaths.Design})try{input.Add(await environment.Source.ReadAsync(path,cancellationToken));}catch(FileNotFoundException){missing=true;}
        if(missing)throw new DocumentationAssetException("INPUT_MISSING","Fixture input missing");
        DocumentationProjection projection;try{projection=environment.Projector.Project(new(new(DocumentationAssetPaths.Blueprint,input[0],"text/markdown"),new(DocumentationAssetPaths.Design,input[1],"text/markdown")));}catch(InvalidDataException e){throw new DocumentationAssetException("PROJECTION_FAILED",e.Message,e);}
        if(environment.Case.Scenario is AssetScenario.DuplicatePath or AssetScenario.CaseCollision)throw new DocumentationAssetException("DUPLICATE_OUTPUT","Fixture duplicate");
        if(environment.Case.Scenario==AssetScenario.ReservedManifest)throw new DocumentationAssetException("RESERVED_OUTPUT","Fixture reserved output");
        var exists=false;
        foreach(var path in projection.Artifacts.Select(a=>a.RelativePath).Append(DocumentationAssetPaths.Manifest).Order(StringComparer.Ordinal))
            try{await environment.Target.ReadAsync(path,cancellationToken);exists=true;}catch(FileNotFoundException){}catch(ArgumentException e){throw new DocumentationAssetException("UNSAFE_OUTPUT",e.Message,e);}catch(IOException e){throw new DocumentationAssetException("OUTPUT_PREFLIGHT_FAILED",e.Message,e);}
        if(exists)throw new DocumentationAssetException("OUTPUT_EXISTS","Fixture occupied generation");
        var stored=new List<StoredArtifact>();
        foreach(var artifact in projection.Artifacts.OrderBy(a=>a.RelativePath,StringComparer.Ordinal))
        {
            try{stored.Add(await environment.Target.WriteAsync(artifact,cancellationToken));}catch(IOException e){throw new DocumentationAssetException("WRITE_FAILED",e.Message,e);}
            if(environment.Case.Scenario is AssetScenario.BadDigest or AssetScenario.BadLength or AssetScenario.BadPath or AssetScenario.BadMedia)throw new DocumentationAssetException("STORED_RECEIPT_MISMATCH","Fixture bad receipt");
        }
        var changed=false;foreach(var path in new[]{DocumentationAssetPaths.Blueprint,DocumentationAssetPaths.Design}){var latest=await environment.Source.ReadAsync(path,cancellationToken);if(!latest.Span.SequenceEqual(environment.Source.Bytes[path]))changed=true;}
        if(changed)throw new DocumentationAssetException("INPUT_CHANGED","Fixture source changed");
        cancellationToken.ThrowIfCancellationRequested();var manifest=new Artifact(DocumentationAssetPaths.Manifest,Encoding.UTF8.GetBytes(environment.ExpectedManifest().ToJsonString()),"application/json");var commit=await environment.Target.WriteAsync(manifest,cancellationToken);
        return new(generationId,environment.Inputs,stored,commit);
    }
}

[Trait("Gate","Mock")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class DocumentationAssetMockTests
{
    [Theory,MemberData(nameof(DocumentationAssetCases.Cases),MemberType=typeof(DocumentationAssetCases))]
    public async Task CorrectDependencyScriptsExerciseTheAssertions(AssetCase test){var environment=new AssetTestEnvironment(test);await DocumentationAssetCases.AssertContract(test,environment,new ScriptedDocumentationAssetOperation(environment));}

    [Theory,MemberData(nameof(DocumentationAssetCases.Cases),MemberType=typeof(DocumentationAssetCases))]
    public async Task WrongOutcomeIsRejectedInsidePassingHarness(AssetCase test)
    {
        var environment=new AssetTestEnvironment(test);
        Assert.NotNull(await Record.ExceptionAsync(()=>DocumentationAssetCases.AssertContract(test,environment,new WrongOutcome())));
    }

    [Theory]
    [InlineData("lease.acquire")][InlineData("lease.release")][InlineData("lease.scope")]
    [InlineData("source.read")][InlineData("source.write")][InlineData("source.bytes")]
    [InlineData("preflight")][InlineData("project.calls")][InlineData("project.blueprint")][InlineData("project.design")]
    [InlineData("result.generation")][InlineData("result.inputs")][InlineData("result.outputs")]
    [InlineData("write.order")][InlineData("source.recheck")][InlineData("manifest.receipt")][InlineData("manifest.body")]
    public async Task IndividualWrongReceiptsAndInteractionsAreRejected(string fault)
    {
        var test=new AssetCase(AssetScenario.Success,"");var environment=new AssetTestEnvironment(test);
        Assert.NotNull(await Record.ExceptionAsync(()=>DocumentationAssetCases.AssertContract(test,environment,new FaultySuccess(environment,fault))));
    }

    private sealed class WrongOutcome:IDocumentationAssetOperation
    {public Task<DocumentationGeneration> GenerateAsync(string generationId,CancellationToken cancellationToken=default)=>Task.FromException<DocumentationGeneration>(new DocumentationAssetException("WRONG_OUTCOME","Negative control"));}

    private sealed class FaultySuccess(AssetTestEnvironment environment,string fault):IDocumentationAssetOperation
    {
        public async Task<DocumentationGeneration> GenerateAsync(string generationId,CancellationToken cancellationToken=default)
        {
            var result=await new ScriptedDocumentationAssetOperation(environment).GenerateAsync(generationId,cancellationToken);
            switch(fault)
            {
                case "lease.acquire":environment.Trace.Remove("lease.acquire");break;
                case "lease.release":environment.Trace.Remove("lease.release");break;
                case "lease.scope":environment.Leases.Scopes[0]="wrong";break;
                case "source.read":environment.Source.Reads[0]="wrong";break;
                case "source.write":try{await environment.Source.WriteAsync(new("wrong",new byte[]{1},"text/plain"),cancellationToken);}catch(InvalidOperationException){}break;
                case "source.bytes":environment.Source.Bytes[DocumentationAssetPaths.Blueprint]=[1];break;
                case "preflight":environment.Trace.Remove("target.read:"+DocumentationAssetPaths.Manifest);break;
                case "project.calls":environment.Projector.Project(environment.Projector.Received!);break;
                case "project.blueprint":environment.Projector.Received=environment.Projector.Received! with{BlueprintMarkdown=new(DocumentationAssetPaths.Blueprint,new byte[]{1},"text/markdown")};break;
                case "project.design":environment.Projector.Received=environment.Projector.Received! with{DesignMarkdown=new(DocumentationAssetPaths.Design,new byte[]{1},"text/markdown")};break;
                case "result.generation":result=result with{GenerationId="wrong"};break;
                case "result.inputs":result=result with{Inputs=result.Inputs.Skip(1).ToArray()};break;
                case "result.outputs":result=result with{Outputs=result.Outputs.Skip(1).ToArray()};break;
                case "write.order":environment.Target.Writes.Reverse();break;
                case "source.recheck":environment.Source.Reads.RemoveAt(environment.Source.Reads.Count-1);break;
                case "manifest.receipt":result=result with{Manifest=result.Manifest with{Sha256="wrong"}};break;
                case "manifest.body":
                    var json=JsonNode.Parse(environment.Target.Writes[^1].Bytes.Span)!;json["generation_id"]="wrong";
                    var changed=environment.Target.Writes[^1] with{Bytes=Encoding.UTF8.GetBytes(json.ToJsonString())};environment.Target.Writes[^1]=changed;result=result with{Manifest=environment.Metadata(changed)};break;
                default:throw new InvalidOperationException("Unknown negative control");
            }
            return result;
        }
    }
}
