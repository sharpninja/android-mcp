using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Review;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Review;

public sealed class ReviewNativeNormalizationNativeTests
{
    [Theory,MemberData(nameof(ReviewNativeNormalizationCases.Data),MemberType=typeof(ReviewNativeNormalizationCases))]
    public async Task Same_projection_cases_consume_actual_shared_producer_with_fake_native_boundaries(string name)
    {
        var fixture=new NormalizationProducerFixture(name);
        var producer=new NativeTestGateEvidenceProducer(fixture,fixture,fixture,new TestBuildPlanAdmission(),new TrxResultAdmission(),fixture.Store);
        var produced=await producer.ProduceAsync(fixture.Run);
        Assert.True(produced.Admitted,produced.Code);Assert.Equal(3,fixture.ProcessCalls);
        Assert.All(fixture.Processes.Take(2),x=>Assert.Contains("--no-incremental",x.Arguments));
        Assert.DoesNotContain("--filter",fixture.Processes[2].Arguments);
        var request=fixture.Request(produced);
        NativeNormObservation observation;
        try
        {
            var result=await new ReviewNativeProjectNormalizer(new PinnedArtifactReader(fixture.Store),new CanonicalJson(),new TestBuildPlanAdmission(),new TrxResultAdmission()).NormalizeAsync(request);
            var body=JsonNode.Parse(result.Json)!;var projects=body["projects"]!.AsArray();
            var roles=string.Join(',',projects.Select(x=>x!["path"]!.GetValue<string>()+":"+x["kind"]!.GetValue<string>()).Order(StringComparer.OrdinalIgnoreCase));
            observation=new("normalized",projects.Count,roles,body["fullUnfilteredTrx"]!.AsArray().Count,body["source"] is null,0);
            Assert.Equal(3,fixture.ProcessCalls); // Normalization never executes a process.
            Assert.Null(body["fullUnfilteredTrx"]![0]!["filter"]);
        }
        catch(ReviewException error){observation=new(error.Code,0,"",0,true,0);}
        catch(PinnedArtifactException error){observation=new(error.Code,0,"",0,true,0);}
        ReviewNativeNormalizationCases.AssertContract(name,observation);
    }
}

// Local boundary fakes supply native observations; the actual shared producer, build/TRX
// admission and pinned reader run unchanged. No SDK, build, test process or provider is launched.
internal sealed class NormalizationProducerFixture:IProcessRunner,IProcessCaptureReader,ITestGateNativeFacts
{
    private readonly string name;
    public ReviewMemoryStore Store {get;}=new();
    public TestGateProducerRequest Run {get;}=new("/fixture","/fixture/app.csproj","/fixture/Tests.csproj",new("/fixture/dotnet",new string('d',64),"10.0"),"/fixture/run",null,["Fixture.Coverage"]);
    public List<ProcessRequest> Processes {get;}=[];
    public int ProcessCalls=>Processes.Count;
    private readonly string head=new('a',40);
    private readonly Guid module=Guid.Parse("11111111-1111-1111-1111-111111111111");
    private StoredArtifact evaluation=null!,manifest=null!,policy=null!,trx=null!;
    private readonly List<ReviewProjectDefinition> definitions=[];
    private TestGateBinaryIdentity Host=>new("/fixture/app.dll",new string('b',64),module);
    private TestGateBinaryIdentity Tests=>new("/fixture/Tests.dll",new string('c',64),module);
    public NormalizationProducerFixture(string name)
    {
        this.name=name;
        var appSha=ReviewFixture.Hash(Encoding.UTF8.GetBytes("explicit-app-project"));var testsSha=ReviewFixture.Hash(Encoding.UTF8.GetBytes("explicit-mixed-project"));
        var gradleSha=ReviewFixture.Hash(Encoding.UTF8.GetBytes("plugins base"));
        definitions.AddRange([new("app.csproj",name=="wrong-definition-sha"?new string('0',64):appSha),new("Tests.csproj",testsSha),new("design/build.gradle.kts",gradleSha)]);
        TestGateEvaluatedProject Project(string path,string sha,bool test)=>new(path,path+".dll",[new("Project",path,path)],[],[],[],null,
            name=="missing-metadata"&&test?null:new(test?"true":"false",test?(name=="unknown-role"?"unknown":"mixed"):"none"));
        var projects=new List<TestGateEvaluatedProject>{Project(Run.HostProject,appSha,false),Project(Run.TestsProject,testsSha,true)};
        if(name=="duplicate-native-project")projects.Add(projects[0]);
        if(name=="dropped-native-project")projects.RemoveAt(0);
        evaluation=Put("run/evaluation.json",new TestGateSdkEvaluation(projects,[]));
        manifest=Put("run/source.json",new TestGateSourceManifest(Run.HostProject,Run.TestsProject,"Debug","net10.0",
            [new("Project",Run.HostProject,Run.HostProject,name=="source-definition-mismatch"?new string('0',64):appSha,20),new("Project",Run.TestsProject,Run.TestsProject,testsSha,22)]));
        policy=Put("policy.json",new{head=name=="wrong-policy-head"?new string('b',40):head,
            projects=name=="missing-gradle-policy"?Array.Empty<object>():new object[]{new{path="design/build.gradle.kts",definitionSha256=gradleSha,isTestProject=name=="gradle-fake-test",kind="none"}}});
        const string xml="<TestRun xmlns='http://microsoft.com/schemas/VisualStudio/TeamTest/2010'><ResultSummary outcome='Completed'><Counters total='1' executed='1' passed='1' failed='0' notExecuted='0'/></ResultSummary><Results><UnitTestResult testId='t1' executionId='e1' outcome='Passed'/></Results><TestDefinitions><UnitTest id='t1'><TestMethod className='Fixture.Coverage' name='NamedCriterion'/></UnitTest></TestDefinitions></TestRun>";
        var bytes=Encoding.UTF8.GetBytes(xml);Store.Values["run/tests.trx"]=bytes;trx=new("run/tests.trx",ReviewFixture.Hash(bytes),bytes.Length,"application/xml");
    }
    private StoredArtifact Put(string path,object value)
    {var bytes=new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(value));Store.Values[path]=bytes;return new(path,ReviewFixture.Hash(bytes),bytes.Length,"application/json");}
    public ReviewNativeNormalizationRequest Request(TestGateProducerResult result)
    {
        var ev=evaluation;
        if(name=="rejected-producer")result=result with{Admitted=false};
        if(name=="evaluation-not-in-producer")result=result with{Evidence=result.Evidence.Where(x=>x!=ev).ToArray()};
        if(name=="changed-evaluation-pin")
        {var wrong=ev with{Sha256=new string('0',64)};result=result with{Evidence=result.Evidence.Select(x=>x==ev?wrong:x).ToArray()};ev=wrong;}
        if(name=="stale-source-fingerprint")result=result with{BuildEvidence=result.BuildEvidence! with{CurrentSourceFingerprint=new string('0',64),BuiltSourceFingerprint=new string('0',64),FinalSourceFingerprint=new string('0',64)}};
        if(name=="wrong-trx-origin-hash")result=result with{TrxAdmission=result.TrxAdmission! with{RawTrxSha256=new string('0',64)}};
        return new(head,name=="filtered-run"?Run with{Filter="Selected"}:Run,result,ev,manifest,policy,definitions,trx,
            new(trx.RelativePath,trx.Sha256,"unit-trx","Tests.csproj","run","fixture",head,"dotnet test Tests.csproj --no-build --no-restore --logger trx"));
    }
    public Task<ProcessResult> RunAsync(ProcessRequest request,CancellationToken token=default)
    {
        Processes.Add(request);var stdout=Path.Combine(request.ReceiptDirectory,"stdout.bin");var stderr=Path.Combine(request.ReceiptDirectory,"stderr.bin");
        Store.Values[Path.GetRelativePath(Run.WorkspaceRoot,stdout)]=[];Store.Values[Path.GetRelativePath(Run.WorkspaceRoot,stderr)]=[];
        return Task.FromResult(new ProcessResult(0,stdout,stderr,DateTimeOffset.UnixEpoch,DateTimeOffset.UnixEpoch,false));
    }
    public Task<ReadOnlyMemory<byte>> ReadAsync(ProcessResult result,bool standardError,CancellationToken token=default)=>Task.FromResult<ReadOnlyMemory<byte>>(ReadOnlyMemory<byte>.Empty);
    public Task<TestGateSourceSnapshot> CaptureSourceAsync(TestGateProducerRequest request,TestGateSnapshotPoint point,CancellationToken token=default)=>Task.FromResult(new TestGateSourceSnapshot(manifest,manifest.Sha256,evaluation));
    public Guid ReadRunningHostModuleId()=>module;
    public Task<TestGateBinaryIdentity> ReadBuiltHostAsync(TestGateProducerRequest request,CancellationToken token=default)=>Task.FromResult(Host);
    public Task<TestGateTestsOutput> ReadBuiltTestsAsync(TestGateProducerRequest request,CancellationToken token=default)=>Task.FromResult(new TestGateTestsOutput(Tests,Host,Put("run/resolution.json",new{observed=true})));
    public Task<TestGateBinaryIdentity> ReadExecutedTestsAsync(TestGateProducerRequest request,ProcessResult result,CancellationToken token=default)=>Task.FromResult(Tests);
}
