using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review.Staging;

public sealed record NativeFactsRead(TestGateNativeFileRequest Request,string BytesBase64);
public sealed record NativeFactsWrite(string Path,string BytesBase64,string MediaType,bool CreateNew=false);
public sealed record NativeFactsObservation(string Code,TestGateSourceSnapshot? Snapshot,TestGateBinaryIdentity? Host,
    Guid? LoadedHost,TestGateTestsOutput? Tests,TestGateBinaryIdentity? Executed,
    IReadOnlyDictionary<string,string>? OriginEnvironment,IReadOnlyList<TestGateSdkEvaluationRequest> Evaluations,
    IReadOnlyList<NativeFactsRead> FileReads,IReadOnlyList<string> ArtifactReads,IReadOnlyList<NativeFactsWrite> ArtifactWrites,
    int NonceCalls,int LoadedCalls,IReadOnlyList<string> Order);

public sealed class NativeFactsFixture
{
    public static readonly string[] Names=["sdk-source-graph","preserved-mtime-content-change","resource-content-change","import-content-change","new-evaluated-compile","sdk-pin-mismatch",
        "unresolved-import","out-of-scope-input","input-changes-during-read","disk-pe-and-loaded-mvid-differ",
        "copied-host-reference-is-old","copy-renamed-destination","copy-missing-destination","copy-conflicting-destinations","copy-out-of-scope-destination","copy-wrong-source","copy-proof-tamper","copy-wrong-run","invalid-managed-pe","origin-complete","origin-wrong-nonce","origin-wrong-run",
        "origin-wrong-loaded-tests-mvid","origin-wrong-loaded-host-mvid","origin-wrong-tests-hash","origin-wrong-challenge-hash","origin-path-escape","origin-replay","origin-challenge-collision","origin-missing"];
    public string Name { get; }
    public string Operation { get; }
    public TestGateProducerRequest Request { get; }=new TestGateProducerFixture("fresh-native-sequence").Request;
    public TestGateNativeFactsConfiguration Configuration { get; }
    public TestGateSdkEvaluationRequest EvaluationRequest=>new(Request,Configuration.Sdk,"Debug","net10.0",
        Operation is "tests" or "origin" ? TestGateSdkEvaluationPhase.ResolvedBuildOutputs : TestGateSdkEvaluationPhase.SourceInputs);
    public TestGateSdkEvaluation Evaluation { get; }
    public Dictionary<string,byte[]> Files { get; }=new(StringComparer.Ordinal);
    public TestGateBinaryIdentity Host { get; }
    public TestGateTestsOutput Tests { get; }
    public Guid Loaded=>Module(3);
    public const string Nonce="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    public string RunRelative=>"artifacts/tests/current-run";
    public string ChallengePath=>RunRelative+"/origin-request.json";
    public string ResponsePath=>RunRelative+"/origin-response.json";
    public TestGateOriginChallenge Challenge=>new(Request.ReceiptDirectory,Nonce,ResponsePath,Tests.Tests,Tests.HostReference);
    public TestGateOriginResponse Response { get; }
    public byte[] ChallengeBytes=>Canonical(Challenge);
    public byte[] ResponseBytes=>Canonical(Response);
    public string ManifestPath=>RunRelative+"/source-BeforeBuild.json";
    public string EvaluationPath=>RunRelative+"/sdk-BeforeBuild.json";
    public byte[] ManifestBytes { get; }
    public byte[] EvaluationBytes=>Canonical(Evaluation);
    public TestGateSourceSnapshot Snapshot=>new(Stored(ManifestPath,ManifestBytes),ContentHash.Sha256(ManifestBytes),Stored(EvaluationPath,EvaluationBytes));
    public StoredArtifact Resolution=>Stored(RunRelative+"/test-resolution.json",Canonical(new{Evaluation,Tests=Tests.Tests,Host=Tests.HostReference}));
    public string ExpectedCode { get; }
    public string CopyProofPath=>RunRelative+"/resolved-reference-native.json";
    public TestGateResolvedCopyProof CopyProof { get; }
    public byte[] CopyProofBytes=>Canonical(CopyProof);
    public StoredArtifact CopyProofArtifact=>Stored(CopyProofPath,CopyProofBytes);
    public bool CopyMetadataRejected=>Name is "copy-missing-destination" or "copy-conflicting-destinations" or "copy-out-of-scope-destination" or "copy-wrong-source";
    public bool CopyProofRejected=>Name is "copy-proof-tamper" or "copy-wrong-run";
    public byte[] ObservedCopyProofBytes=>Name=="copy-proof-tamper" ? Encoding.UTF8.GetBytes("tampered-native-reference-proof") : CopyProofBytes;
    public static Guid Module(int number)=>Guid.Parse(number switch{1=>"11111111-1111-1111-1111-111111111111",2=>"22222222-2222-2222-2222-222222222222",_=>"33333333-3333-3333-3333-333333333333"});
    public static byte[] Canonical<T>(T value)=>new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(value));
    public static StoredArtifact Stored(string path,byte[] bytes)=>new(path,ContentHash.Sha256(bytes),bytes.Length,"application/json");
    public static byte[] Pe(Guid id)
    {
        var metadata=new MetadataBuilder();metadata.AddModule(0,metadata.GetOrAddString("fixture.dll"),metadata.GetOrAddGuid(id),default,default);
        var image=new ManagedPEBuilder(new PEHeaderBuilder(imageCharacteristics:Characteristics.ExecutableImage|Characteristics.Dll),
            new MetadataRootBuilder(metadata),new BlobBuilder());var blob=new BlobBuilder();image.Serialize(blob);return blob.ToArray();
    }
    public NativeFactsFixture(string name)
    {
        Name=name;Operation=name.StartsWith("origin-",StringComparison.Ordinal) ? "origin" : name is "disk-pe-and-loaded-mvid-differ" or "invalid-managed-pe" ? "host" : name=="copied-host-reference-is-old"||name.StartsWith("copy-",StringComparison.Ordinal) ? "tests" : "source";
        Files["/fixture/sdk/Microsoft.Build.dll"]=Encoding.UTF8.GetBytes("pinned-sdk-build");
        var sdkFiles=new[]{Stored("Microsoft.Build.dll",Files["/fixture/sdk/Microsoft.Build.dll"])};
        Configuration=new(new("/fixture/sdk",sdkFiles),["/fixture/app","/fixture/sdk"],"Debug","net10.0");
        if(name=="sdk-pin-mismatch")Files["/fixture/sdk/Microsoft.Build.dll"]=Encoding.UTF8.GetBytes("tampered-sdk-build");
        var hostInputs=new List<TestGateEvaluatedInput>{new("Project",Request.HostProject,Request.HostProject),
            new("Compile","/fixture/app/Source.cs",Request.HostProject),new("EmbeddedResource","/fixture/app/Resources/schema.json",Request.HostProject),
            new("Assets","/fixture/app/obj/project.assets.json",Request.HostProject)};
        if(name=="new-evaluated-compile")hostInputs.Add(new("Compile","/fixture/app/New.cs",Request.HostProject));
        if(name=="out-of-scope-input")hostInputs.Add(new("Compile","/private/not-approved.cs",Request.HostProject));
        var testInputs=new[]{new TestGateEvaluatedInput("Project",Request.TestsProject,Request.TestsProject),
            new TestGateEvaluatedInput("Compile","/fixture/app/Tests/Origin.cs",Request.TestsProject),
            new TestGateEvaluatedInput("Assets","/fixture/app/Tests/obj/project.assets.json",Request.TestsProject)};
        var import="/fixture/sdk/Sdk.props";
        var copyPath=name=="copy-renamed-destination" ? "/fixture/app/Tests/bin/deps/RenamedHost.dll" : name=="copy-out-of-scope-destination" ? "/private/not-approved.dll" : "/fixture/app/Tests/bin/App.dll";
        var copySource=name=="copy-wrong-source" ? "/fixture/app/bin/Stale.dll" : "/fixture/app/bin/App.dll";
        CopyProof=new(name=="copy-wrong-run" ? "/fixture/app/artifacts/tests/old-run" : Request.ReceiptDirectory,
            Request.TestsProject,copySource,copyPath,Request.HostProject,Request.DotNet,"Debug","net10.0");
        TestGateResolvedCopy[] copies=Operation is "tests" or "origin" ? [new(copySource,copyPath,Request.HostProject,CopyProofArtifact)] : [];
        if(name=="copy-missing-destination")copies=[];
        if(name=="copy-conflicting-destinations")copies=[copies[0],copies[0] with {DestinationPath="/fixture/app/Tests/bin/Other.dll"}];
        Evaluation=new([new(Request.HostProject,"/fixture/app/bin/App.dll",hostInputs,[import],[],name=="unresolved-import" ? ["UnresolvedImport"] : []),
            new(Request.TestsProject,"/fixture/app/Tests/bin/Tests.dll",testInputs,[import],[Request.HostProject],[],copies)],sdkFiles);
        foreach(var input in Evaluation.Projects.SelectMany(project=>project.Inputs))Files[input.AbsolutePath]=Encoding.UTF8.GetBytes("input:"+input.AbsolutePath);
        Files[import]=Encoding.UTF8.GetBytes("import-bytes");
        if(name=="preserved-mtime-content-change")Files["/fixture/app/Source.cs"]=Encoding.UTF8.GetBytes("changed-bytes-same-mtime");
        if(name=="resource-content-change")Files["/fixture/app/Resources/schema.json"]=Encoding.UTF8.GetBytes("changed-resource-bytes");
        if(name=="import-content-change")Files[import]=Encoding.UTF8.GetBytes("changed-import-bytes");
        Files["/fixture/app/bin/App.dll"]=name=="invalid-managed-pe" ? Encoding.UTF8.GetBytes("not-a-managed-pe") : Pe(Module(1));
        Files["/fixture/app/Tests/bin/Tests.dll"]=Pe(Module(2));
        Files[copyPath]=Pe(name=="copied-host-reference-is-old" ? Module(3) : Module(1));
        Host=new("/fixture/app/bin/App.dll",ContentHash.Sha256(Files["/fixture/app/bin/App.dll"]),Module(1));
        var copied=new TestGateBinaryIdentity(copyPath,ContentHash.Sha256(Files[copyPath]),name=="copied-host-reference-is-old" ? Module(3) : Module(1));
        var tested=new TestGateBinaryIdentity("/fixture/app/Tests/bin/Tests.dll",ContentHash.Sha256(Files["/fixture/app/Tests/bin/Tests.dll"]),Module(2));
        // The native resolution proof is assigned independently below; no output derived from a collector implementation.
        Tests=new(tested,copied,new("unused","",0,"application/json"));Tests=Tests with {NativeResolutionReceipt=Resolution};
        var inputs=Evaluation.Projects.SelectMany(project=>project.Inputs.Concat(project.Imports.Select(path=>new TestGateEvaluatedInput("Import",path,project.ProjectPath))))
            .OrderBy(input=>input.Kind,StringComparer.Ordinal).ThenBy(input=>input.AbsolutePath,StringComparer.Ordinal).ThenBy(input=>input.DefiningProject,StringComparer.Ordinal)
            .Select(input=>new TestGateHashedInput(input.Kind,input.AbsolutePath,input.DefiningProject,ContentHash.Sha256(Files[input.AbsolutePath]),Files[input.AbsolutePath].LongLength)).ToArray();
        ManifestBytes=Canonical(new TestGateSourceManifest(Request.HostProject,Request.TestsProject,"Debug","net10.0",inputs));
        Response=new(Request.ReceiptDirectory,Nonce,ContentHash.Sha256(ChallengeBytes),tested.AbsolutePath,tested.ModuleId,tested.Sha256,copied.AbsolutePath,copied.ModuleId,copied.Sha256);
        Response=name switch
        {
            "origin-wrong-nonce"=>Response with {Nonce=new string('b',64)},"origin-wrong-run"=>Response with {RunPath="/fixture/app/artifacts/tests/old-run"},
            "origin-wrong-loaded-tests-mvid"=>Response with {LoadedTestsModuleId=Module(3)},"origin-wrong-loaded-host-mvid"=>Response with {LoadedHostModuleId=Module(3)},
            "origin-wrong-tests-hash"=>Response with {TestsSha256=new string('e',64)},
            "origin-wrong-challenge-hash"=>Response with {ChallengeSha256=new string('e',64)},
            "origin-path-escape"=>Response with {TestsPath="/private/not-approved.dll"},_=>Response
        };
        ExpectedCode=name switch
        {
            "sdk-pin-mismatch"=>"SDK_PIN_MISMATCH","unresolved-import"=>"SOURCE_INPUTS_UNRESOLVED","out-of-scope-input"=>"INPUT_SCOPE_VIOLATION",
            "input-changes-during-read"=>"INPUT_CHANGED","invalid-managed-pe"=>"BINARY_METADATA_INVALID",
            "copy-missing-destination"=>"RESOLVED_REFERENCE_MISSING","copy-conflicting-destinations"=>"RESOLVED_REFERENCE_CONFLICT",
            "copy-out-of-scope-destination"=>"INPUT_SCOPE_VIOLATION","copy-wrong-source"=>"RESOLVED_REFERENCE_MISMATCH",
            "copy-proof-tamper" or "copy-wrong-run"=>"RESOLVED_REFERENCE_PROOF_INVALID",
            "origin-missing"=>"ORIGIN_MISSING","origin-replay"=>"ORIGIN_ALREADY_PREPARED","origin-challenge-collision"=>"ORIGIN_ALREADY_EXISTS",_ when name.StartsWith("origin-",StringComparison.Ordinal)&&name!="origin-complete"=>"ORIGIN_MISMATCH",_=>"OK"
        };
    }
    public TestGateNativeFileRequest ReadRequest(string path,string role)=>new(path,role,Configuration.ApprovedReadRoots);
    public IEnumerable<TestGateNativeFileRequest> SourceReads()
    {
        yield return ReadRequest("/fixture/sdk/Microsoft.Build.dll","SdkAssembly");
        if(Name is "sdk-pin-mismatch" or "unresolved-import" or "out-of-scope-input")yield break;
        var paths=Evaluation.Projects.SelectMany(project=>project.Inputs.Select(input=>input.AbsolutePath).Concat(project.Imports)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        foreach(var path in paths)yield return ReadRequest(path,"SourceInput");
        foreach(var path in paths)yield return ReadRequest(path,"SourceInputRecheck");
    }
}

public sealed class NativeFactsDependencies(NativeFactsFixture fixture):ITestGateSdkEvaluator,ITestGateNativeFileReader,
    ITestGateLoadedModuleSource,ITestGateNonceSource,IArtifactStore,ITestGateOriginArtifactWriter
{
    public List<TestGateSdkEvaluationRequest> Evaluations { get; }=[];
    public List<NativeFactsRead> Files { get; }=[];
    public List<string> Reads { get; }=[];public List<NativeFactsWrite> Writes { get; }=[];public List<string> Order { get; }=[];
    public int NonceCalls { get; private set; }public int LoadedCalls { get; private set; }
    public Task<TestGateSdkEvaluation> EvaluateAsync(TestGateSdkEvaluationRequest request,CancellationToken cancellationToken=default)
    {Order.Add("sdk-evaluate");Evaluations.Add(request);return Task.FromResult(fixture.Evaluation);}
    public Task<ReadOnlyMemory<byte>> ReadAsync(TestGateNativeFileRequest request,CancellationToken cancellationToken=default)
    {
        Order.Add("file:"+request.Role+":"+request.AbsolutePath);var bytes=fixture.Files[request.AbsolutePath];
        if(fixture.Name=="input-changes-during-read"&&request.Role=="SourceInputRecheck"&&request.AbsolutePath.EndsWith("Source.cs",StringComparison.Ordinal))bytes=Encoding.UTF8.GetBytes("changed-during-read");
        Files.Add(new(request,Convert.ToBase64String(bytes)));return Task.FromResult<ReadOnlyMemory<byte>>(bytes);
    }
    public Guid ReadHostModuleId(){Order.Add("loaded-host");LoadedCalls++;return fixture.Loaded;}
    public string CreateNonce(){Order.Add("nonce");NonceCalls++;return NativeFactsFixture.Nonce;}
    public Task<ReadOnlyMemory<byte>> ReadAsync(string relativePath,CancellationToken cancellationToken=default)
    {
        Order.Add("artifact-read:"+relativePath);Reads.Add(relativePath);
        if(relativePath==fixture.CopyProofPath)return Task.FromResult<ReadOnlyMemory<byte>>(fixture.ObservedCopyProofBytes);
        if(fixture.Name=="origin-missing")throw new FileNotFoundException("Expected current origin receipt is absent.");
        return Task.FromResult<ReadOnlyMemory<byte>>(fixture.ResponseBytes);
    }
    public Task<StoredArtifact> WriteAsync(Artifact artifact,CancellationToken cancellationToken=default)
    {Order.Add("artifact-write:"+artifact.RelativePath);var bytes=artifact.Bytes.ToArray();Writes.Add(new(artifact.RelativePath,Convert.ToBase64String(bytes),artifact.MediaType));return Task.FromResult(NativeFactsFixture.Stored(artifact.RelativePath,bytes));}
    public Task<StoredArtifact> WriteNewAsync(Artifact artifact,CancellationToken cancellationToken=default)
    {
        Order.Add("artifact-write-new:"+artifact.RelativePath);var bytes=artifact.Bytes.ToArray();Writes.Add(new(artifact.RelativePath,Convert.ToBase64String(bytes),artifact.MediaType,true));
        if(fixture.Name=="origin-challenge-collision")throw new TestGateFactsException("ORIGIN_ALREADY_EXISTS","Current challenge path already exists, even if its bytes match.");
        return Task.FromResult(NativeFactsFixture.Stored(artifact.RelativePath,bytes));
    }
    public NativeFactsObservation Observe(string code,TestGateSourceSnapshot? snapshot=null,TestGateBinaryIdentity? host=null,
        Guid? loaded=null,TestGateTestsOutput? tests=null,TestGateBinaryIdentity? executed=null,IReadOnlyDictionary<string,string>? environment=null)
        =>new(code,snapshot,host,loaded,tests,executed,environment,Evaluations,Files,Reads,Writes,NonceCalls,LoadedCalls,Order);
}

public interface INativeFactsCaseSystem { Task<NativeFactsObservation> ExecuteAsync(NativeFactsFixture fixture); }
// Scripted collaborator interactions and independently specified results, not collector implementation.
public sealed class ScriptedNativeFactsCases:INativeFactsCaseSystem
{
    public async Task<NativeFactsObservation> ExecuteAsync(NativeFactsFixture fixture)
    {
        var io=new NativeFactsDependencies(fixture);
        if(fixture.Operation=="source")
        {
            var reads=fixture.SourceReads().ToArray();await io.ReadAsync(reads[0]);
            if(fixture.Name=="sdk-pin-mismatch")return io.Observe(fixture.ExpectedCode);
            await io.EvaluateAsync(fixture.EvaluationRequest);
            foreach(var read in reads.Skip(1))await io.ReadAsync(read);
            if(fixture.ExpectedCode=="OK")
            {
                await io.WriteAsync(new(fixture.EvaluationPath,fixture.EvaluationBytes,"application/json"));
                await io.WriteAsync(new(fixture.ManifestPath,fixture.ManifestBytes,"application/json"));
            }
            return io.Observe(fixture.ExpectedCode,snapshot:fixture.ExpectedCode=="OK" ? fixture.Snapshot : null);
        }
        if(fixture.Operation=="host")
        {
            var loaded=io.ReadHostModuleId();await io.EvaluateAsync(fixture.EvaluationRequest);
            await io.ReadAsync(fixture.ReadRequest(fixture.Host.AbsolutePath,"BuiltHost"));
            return io.Observe(fixture.ExpectedCode,host:fixture.ExpectedCode=="OK" ? fixture.Host : null,loaded:loaded);
        }
        await io.EvaluateAsync(fixture.EvaluationRequest);
        if(fixture.CopyMetadataRejected)return io.Observe(fixture.ExpectedCode);
        await io.ReadAsync(fixture.CopyProofPath);
        if(fixture.CopyProofRejected)return io.Observe(fixture.ExpectedCode);
        await io.ReadAsync(fixture.ReadRequest(fixture.Tests.Tests.AbsolutePath,"BuiltTests"));
        await io.ReadAsync(fixture.ReadRequest(fixture.Tests.HostReference.AbsolutePath,"HostReference"));
        await io.WriteAsync(new(fixture.Resolution.RelativePath,NativeFactsFixture.Canonical(new{fixture.Evaluation,Tests=fixture.Tests.Tests,Host=fixture.Tests.HostReference}),"application/json"));
        if(fixture.Operation=="tests")return io.Observe("OK",tests:fixture.Tests);
        io.CreateNonce();
        try{await io.WriteNewAsync(new(fixture.ChallengePath,fixture.ChallengeBytes,"application/json"));}
        catch(TestGateFactsException error){return io.Observe(error.Code,tests:fixture.Tests);}
        var environment=new Dictionary<string,string>{{"ANDROIDMCP_TEST_GATE_ORIGIN_REQUEST",fixture.Request.WorkspaceRoot+"/"+fixture.ChallengePath}};
        if(fixture.Name=="origin-replay")return io.Observe(fixture.ExpectedCode,tests:fixture.Tests,environment:environment);
        try{await io.ReadAsync(fixture.ResponsePath);}catch(FileNotFoundException){return io.Observe("ORIGIN_MISSING",tests:fixture.Tests,environment:environment);}
        if(fixture.ExpectedCode=="OK")
        {
            await io.ReadAsync(fixture.ReadRequest(fixture.Tests.Tests.AbsolutePath,"ExecutedTests"));
            await io.ReadAsync(fixture.ReadRequest(fixture.Tests.HostReference.AbsolutePath,"ExecutedHost"));
        }
        return io.Observe(fixture.ExpectedCode,tests:fixture.Tests,executed:fixture.ExpectedCode=="OK" ? fixture.Tests.Tests : null,environment:environment);
    }
}

public static class NativeFactsCases
{
    public static IEnumerable<object[]> Data=>NativeFactsFixture.Names.Select(name=>new object[]{name});
    public static JsonNode Json(NativeFactsObservation value)=>JsonSerializer.SerializeToNode(value)!;
    public static void AssertContract(NativeFactsFixture fixture,JsonNode actual)
    {
        var expected=Json(NativeFactsAssertions.Expected(fixture));
        Assert.Equal(fixture.ExpectedCode,actual["Code"]!.GetValue<string>());
        Assert.Equal(new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(expected)),new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(actual)));
    }
    public static IEnumerable<object[]> Controls
    {
        get
        {
            foreach(var name in NativeFactsFixture.Names)
            {
                var node=Json(NativeFactsAssertions.Expected(new(name)));var index=0;
                foreach(var member in ReviewOfficialTransportCases.Leaves(node))yield return new object[]{name,index++,member.Label};
            }
        }
    }
}

[Trait("Gate","Mock")]
public sealed class NativeTestGateFactsMockTests
{
    [Theory,MemberData(nameof(NativeFactsCases.Data),MemberType=typeof(NativeFactsCases))]
    public async Task CorrectTypedNativeFactCollectorMock(string name)
    {
        var fixture=new NativeFactsFixture(name);var actual=await new ScriptedNativeFactsCases().ExecuteAsync(fixture);
        NativeFactsCases.AssertContract(fixture,NativeFactsCases.Json(actual));
    }
    [Theory,MemberData(nameof(NativeFactsCases.Controls),MemberType=typeof(NativeFactsCases))]
    public async Task EachFullInputProofByteScopeMvidNonceAndInteractionFieldRejects(string name,int memberIndex,string label)
    {
        var fixture=new NativeFactsFixture(name);var actual=await new ScriptedNativeFactsCases().ExecuteAsync(fixture);
        var correct=NativeFactsCases.Json(actual);NativeFactsCases.AssertContract(fixture,correct);
        var members=ReviewOfficialTransportCases.Leaves(correct).ToArray();Assert.Equal(label,members[memberIndex].Label);
        ReviewOfficialTransportCases.Mutate(correct,members[memberIndex].Path);
        Assert.NotNull(Record.Exception(()=>NativeFactsCases.AssertContract(fixture,correct)));
    }
}

// Independently asserted full typed effects, with no production parser/collector available.
public static class NativeFactsAssertions
{
    public static NativeFactsObservation Expected(NativeFactsFixture fixture)
    {
        var reads=new List<TestGateNativeFileRequest>();var writes=new List<NativeFactsWrite>();var order=new List<string>();var artifactReads=new List<string>();
        TestGateSourceSnapshot? snapshot=null;TestGateBinaryIdentity? host=null;Guid? loaded=null;
        TestGateTestsOutput? tests=null;TestGateBinaryIdentity? executed=null;IReadOnlyDictionary<string,string>? environment=null;
        if(fixture.Operation=="source")
        {
            reads.AddRange(fixture.SourceReads());order.Add("file:SdkAssembly:/fixture/sdk/Microsoft.Build.dll");
            if(fixture.Name!="sdk-pin-mismatch")order.Add("sdk-evaluate");
            order.AddRange(reads.Skip(1).Select(read=>"file:"+read.Role+":"+read.AbsolutePath));
            if(fixture.ExpectedCode=="OK")
            {
                snapshot=fixture.Snapshot;
                writes.Add(new(fixture.EvaluationPath,Convert.ToBase64String(fixture.EvaluationBytes),"application/json"));
                writes.Add(new(fixture.ManifestPath,Convert.ToBase64String(fixture.ManifestBytes),"application/json"));
            }
        }
        else if(fixture.Operation=="host")
        {
            order.AddRange(["loaded-host","sdk-evaluate"]);reads.Add(fixture.ReadRequest(fixture.Host.AbsolutePath,"BuiltHost"));order.Add("file:BuiltHost:"+fixture.Host.AbsolutePath);
            loaded=fixture.Loaded;host=fixture.ExpectedCode=="OK" ? fixture.Host : null;
        }
        else
        {
            order.Add("sdk-evaluate");
            if(!fixture.CopyMetadataRejected){artifactReads.Add(fixture.CopyProofPath);order.Add("artifact-read:"+fixture.CopyProofPath);}
            if(!fixture.CopyMetadataRejected&&!fixture.CopyProofRejected)
            {
            reads.Add(fixture.ReadRequest(fixture.Tests.Tests.AbsolutePath,"BuiltTests"));reads.Add(fixture.ReadRequest(fixture.Tests.HostReference.AbsolutePath,"HostReference"));
            order.AddRange(reads.Select(read=>"file:"+read.Role+":"+read.AbsolutePath));tests=fixture.Tests;
            writes.Add(new(fixture.Resolution.RelativePath,Convert.ToBase64String(NativeFactsFixture.Canonical(new{fixture.Evaluation,Tests=fixture.Tests.Tests,Host=fixture.Tests.HostReference})),"application/json"));
            order.Add("artifact-write:"+fixture.Resolution.RelativePath);
            if(fixture.Operation=="origin")
            {
                order.Add("nonce");writes.Add(new(fixture.ChallengePath,Convert.ToBase64String(fixture.ChallengeBytes),"application/json",true));order.Add("artifact-write-new:"+fixture.ChallengePath);
                if(fixture.Name is not ("origin-replay" or "origin-challenge-collision")){artifactReads.Add(fixture.ResponsePath);order.Add("artifact-read:"+fixture.ResponsePath);}
                if(fixture.Name!="origin-challenge-collision")environment=new Dictionary<string,string>{{"ANDROIDMCP_TEST_GATE_ORIGIN_REQUEST",fixture.Request.WorkspaceRoot+"/"+fixture.ChallengePath}};
                if(fixture.ExpectedCode=="OK")
                {
                    reads.Add(fixture.ReadRequest(fixture.Tests.Tests.AbsolutePath,"ExecutedTests"));reads.Add(fixture.ReadRequest(fixture.Tests.HostReference.AbsolutePath,"ExecutedHost"));
                    order.AddRange(reads.TakeLast(2).Select(read=>"file:"+read.Role+":"+read.AbsolutePath));executed=fixture.Tests.Tests;
                }
            }
            }
        }
        if(fixture.Operation=="source")order.AddRange(writes.Select(write=>"artifact-write:"+write.Path));
        var observedReads=new List<NativeFactsRead>();
        foreach(var read in reads)
        {
            var bytes=fixture.Name=="input-changes-during-read"&&read.Role=="SourceInputRecheck"&&read.AbsolutePath.EndsWith("Source.cs",StringComparison.Ordinal) ? Encoding.UTF8.GetBytes("changed-during-read") : fixture.Files[read.AbsolutePath];
            observedReads.Add(new(read,Convert.ToBase64String(bytes)));
        }
        return new(fixture.ExpectedCode,snapshot,host,loaded,tests,executed,environment,fixture.Name=="sdk-pin-mismatch" ? [] : [fixture.EvaluationRequest],observedReads,
            artifactReads,writes,fixture.Operation=="origin" ? 1 : 0,fixture.Operation=="host" ? 1 : 0,order);
    }
}
