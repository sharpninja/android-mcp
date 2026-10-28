using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review.Staging;

public sealed record ProducerFactCall(string Kind,TestGateProducerRequest? Request,
    TestGateSnapshotPoint? Point,ProcessResult? Process,object? Response);
public sealed record ProducerCaptureCall(ProcessResult Result,bool StandardError,string BytesBase64);
public sealed record ProducerBuildEvaluation(TestBuildPlan Plan,TestBuildEvidence Evidence);
public sealed record ProducerTrxEvaluation(string? BytesBase64,IReadOnlyList<string> ExpectedClasses);
public sealed record ProducerObservation(TestGateProducerRequest Request,TestGateProducerResult Result,
    IReadOnlyList<ProcessRequest> ProcessCalls,IReadOnlyList<ProcessResult> ProcessResults,
    IReadOnlyList<ProducerFactCall> FactCalls,IReadOnlyList<ProducerCaptureCall> CaptureCalls,
    IReadOnlyList<TestBuildPlanRequest> PlanCalls,IReadOnlyList<ProducerBuildEvaluation> BuildEvaluations,
    IReadOnlyList<ProducerTrxEvaluation> TrxEvaluations,IReadOnlyList<string> ArtifactReads,
    IReadOnlyList<string> ArtifactWrites,IReadOnlyList<string> Order);

public sealed class TestGateProducerFixture
{
    public static readonly string[] Names=["fresh-native-sequence","preserved-mtime-content-changed",
        "old-loaded-host-new-disk","source-changes-during-build","old-host-test-reference",
        "different-executed-test-binary","host-build-failure","exit-zero-zero-discovery"];
    public string Name { get; }
    public TestGateProducerRequest Request { get; }=new("/fixture/app","/fixture/app/App.csproj",
        "/fixture/app/Tests/Tests.csproj",new("/fixture/dotnet",new string('d',64),"10.0"),
        "/fixture/app/artifacts/tests/current-run","FullyQualifiedName~Fixture.Unit",[]);
    public TestBuildPlan Plan { get; }
    public Guid LoadedHost { get; }
    public TestGateBinaryIdentity Host { get; }
    public TestGateTestsOutput BuiltTests { get; }
    public TestGateBinaryIdentity ExecutedTests { get; }
    public IReadOnlyList<TestGateSourceSnapshot> Snapshots { get; }
    public IReadOnlyList<ProcessRequest> ExpectedProcesses { get; }
    public IReadOnlyList<ProcessResult> Processes { get; }
    public TestBuildEvidence? ExpectedBuild { get; }
    public TrxAdmissionFixture Trx { get; }
    public TrxAdmissionResult TrxExpected=>Trx.Expected with { MissingExpectedClasses=[] };
    public TestGateProducerResult ExpectedResult { get; }
    public IReadOnlyList<string> ExpectedOrder { get; }
    public string TrxPath=>Path.GetRelativePath(Request.WorkspaceRoot,Request.ReceiptDirectory+"/tests.trx").Replace('\\','/');
    public static Guid Module(int value)=>Guid.Parse(value==1 ? "11111111-1111-1111-1111-111111111111" : "22222222-2222-2222-2222-222222222222");
    public static string Hash(char value)=>new(value,64);
    public static StoredArtifact Receipt(string path,char hash,long length=1)=>new(path,Hash(hash),length,"application/json");
    public TestGateProducerFixture(string name)
    {
        Name=name;
        var steps=new[]{new TestBuildStep(TestBuildStepKind.ForceHostBuild,Request.HostProject,true,true,false,true),
            new TestBuildStep(TestBuildStepKind.ForceTestsBuild,Request.TestsProject,true,true,false,false),
            new TestBuildStep(TestBuildStepKind.TestWithoutBuild,Request.TestsProject,false,true,true,false)};
        Plan=new(Request.DotNet,steps);
        Host=new("/fixture/app/bin/App.dll",Hash('b'),Module(1));
        LoadedHost=name=="old-loaded-host-new-disk" ? Module(2) : Module(1);
        BuiltTests=new(new("/fixture/app/Tests/bin/Tests.dll",Hash('c'),Module(2)),
            Host with { AbsolutePath="/fixture/app/Tests/bin/App.dll",Sha256=name=="old-host-test-reference" ? Hash('e') : Host.Sha256 },Receipt("native-resolution.json",'f'));
        ExecutedTests=BuiltTests.Tests with { Sha256=name=="different-executed-test-binary" ? Hash('e') : BuiltTests.Tests.Sha256 };
        var initial=name=="preserved-mtime-content-changed" ? 'e' : 'a';
        Snapshots=Enumerable.Range(0,4).Select(i=>new TestGateSourceSnapshot(Receipt($"source-{i}.json",initial),
            name=="source-changes-during-build"&&i>0 ? Hash('e') : Hash(initial),Receipt($"evaluation-{i}.json",'f'))).ToArray();
        var count=name switch {"old-loaded-host-new-disk" or "source-changes-during-build" or "host-build-failure"=>1,"old-host-test-reference"=>2,_=>3};
        ExpectedProcesses=Enumerable.Range(0,count).Select(i=>new ProcessRequest(Request.DotNet,Arguments(i),Request.WorkspaceRoot,
            new Dictionary<string,string>(),Request.ReceiptDirectory+"/process-"+i)).ToArray();
        Processes=Enumerable.Range(0,count).Select(i=>new ProcessResult(name=="host-build-failure"&&i==0 ? 1 : 0,
            Request.ReceiptDirectory+$"/process-{i}/stdout.bin",Request.ReceiptDirectory+$"/process-{i}/stderr.bin",
            DateTimeOffset.UnixEpoch,DateTimeOffset.UnixEpoch,false)).ToArray();
        Trx=new(name=="exit-zero-zero-discovery" ? "zero-discovered" : "pass-without-expected-classes");
        var code=name switch {"old-loaded-host-new-disk"=>"STALE_HOST","source-changes-during-build"=>"SOURCE_CHANGED",
            "old-host-test-reference"=>"STALE_TEST_REFERENCE","different-executed-test-binary"=>"STALE_TEST_BINARY",
            "host-build-failure"=>"PROCESS_FAILED","exit-zero-zero-discovery"=>"NO_TESTS",_=>"OK"};
        if(count==3)ExpectedBuild=new(steps,Snapshots[0].ContentFingerprint,Snapshots[1].ContentFingerprint,
            Snapshots[3].ContentFingerprint,Host.Sha256,Host.ModuleId,LoadedHost,BuiltTests.HostReference.Sha256,
            BuiltTests.Tests.Sha256,ExecutedTests.Sha256);
        var proofs=new List<StoredArtifact>();
        var snapshotCount=name=="host-build-failure" ? 1 : count==1 ? 2 : count==2 ? 3 : 4;
        foreach(var snapshot in Snapshots.Take(snapshotCount))proofs.AddRange([snapshot.Manifest,snapshot.NativeEvaluationReceipt]);
        if(count>1)proofs.Add(BuiltTests.NativeResolutionReceipt);
        for(var index=0;index<count;index++)
        {
            var output=Encoding.UTF8.GetBytes("output-"+index);var error=Encoding.UTF8.GetBytes("error-"+index);
            proofs.Add(new(Path.GetRelativePath(Request.WorkspaceRoot,Processes[index].StandardOutputPath).Replace('\\','/'),ContentHash.Sha256(output),output.Length,"application/octet-stream"));
            proofs.Add(new(Path.GetRelativePath(Request.WorkspaceRoot,Processes[index].StandardErrorPath).Replace('\\','/'),ContentHash.Sha256(error),error.Length,"application/octet-stream"));
        }
        ExpectedResult=new(code=="OK",code,ExpectedBuild,
            name is "fresh-native-sequence" or "preserved-mtime-content-changed" or "exit-zero-zero-discovery" ? TrxExpected : null,proofs);
        var order=new List<string>{"plan","source:BeforeBuild","loaded-host","process:0","stdout:0","stderr:0"};
        if(name!="host-build-failure")
        {
            order.AddRange(["built-host","source:AfterHostBuild"]);
            if(count>1)
            {
                order.AddRange(["process:1","stdout:1","stderr:1","built-tests","source:AfterTestsBuild"]);
                if(count==3)
                {
                    order.AddRange(["process:2","stdout:2","stderr:2","executed-tests","source:AfterExecution","build-admission"]);
                    if(name!="different-executed-test-binary")order.AddRange(["read:"+TrxPath,"trx-admission"]);
                }
            }
        }
        ExpectedOrder=order;
    }
    public IReadOnlyList<string> Arguments(int index)=>index switch
    {
        0=>["build",Request.HostProject,"--no-incremental","--no-restore"],
        1=>["build",Request.TestsProject,"--no-incremental","--no-restore","-p:BuildProjectReferences=false"],
        _=>["test",Request.TestsProject,"--no-build","--no-restore","--filter",Request.Filter!,"--results-directory",Request.ReceiptDirectory,"--logger","trx;LogFileName=tests.trx"]
    };
}

// All collaborators receive and preserve typed inputs. The scripted producer is a TEST MOCK,
// not production orchestration or a positive fallback, and never invokes native processes.
public sealed class ProducerMockDependencies(TestGateProducerFixture fixture):IProcessRunner,IProcessCaptureReader,
    ITestGateNativeFacts,ITestBuildPlanAdmission,ITrxResultAdmission,IArtifactStore
{
    public List<string> Order { get; }=[];
    public List<ProcessRequest> Processes { get; }=[];
    public List<ProcessResult> Results { get; }=[];
    public List<ProducerFactCall> Facts { get; }=[];
    public List<ProducerCaptureCall> Captures { get; }=[];
    public List<TestBuildPlanRequest> Plans { get; }=[];
    public List<ProducerBuildEvaluation> Builds { get; }=[];
    public List<ProducerTrxEvaluation> Trxs { get; }=[];
    public List<string> Reads { get; }=[];
    public List<string> Writes { get; }=[];
    public Task<ProcessResult> RunAsync(ProcessRequest request,CancellationToken cancellationToken=default)
    {var index=Processes.Count;Order.Add("process:"+index);Processes.Add(request);var result=fixture.Processes[index];Results.Add(result);return Task.FromResult(result);}
    public Task<ReadOnlyMemory<byte>> ReadAsync(ProcessResult result,bool standardError,CancellationToken cancellationToken=default)
    {
        var index=Results.IndexOf(result);Order.Add((standardError ? "stderr:" : "stdout:")+index);
        var bytes=Encoding.UTF8.GetBytes((standardError ? "error-" : "output-")+index);
        Captures.Add(new(result,standardError,Convert.ToBase64String(bytes)));return Task.FromResult<ReadOnlyMemory<byte>>(bytes);
    }
    public Task<TestGateSourceSnapshot> CaptureSourceAsync(TestGateProducerRequest request,TestGateSnapshotPoint point,CancellationToken cancellationToken=default)
    {Order.Add("source:"+point);var value=fixture.Snapshots[(int)point];Facts.Add(new("source",request,point,null,value));return Task.FromResult(value);}
    public Guid ReadRunningHostModuleId(){Order.Add("loaded-host");Facts.Add(new("loaded-host",null,null,null,fixture.LoadedHost));return fixture.LoadedHost;}
    public Task<TestGateBinaryIdentity> ReadBuiltHostAsync(TestGateProducerRequest request,CancellationToken cancellationToken=default)
    {Order.Add("built-host");Facts.Add(new("built-host",request,null,null,fixture.Host));return Task.FromResult(fixture.Host);}
    public Task<TestGateTestsOutput> ReadBuiltTestsAsync(TestGateProducerRequest request,CancellationToken cancellationToken=default)
    {Order.Add("built-tests");Facts.Add(new("built-tests",request,null,null,fixture.BuiltTests));return Task.FromResult(fixture.BuiltTests);}
    public Task<TestGateBinaryIdentity> ReadExecutedTestsAsync(TestGateProducerRequest request,ProcessResult result,CancellationToken cancellationToken=default)
    {Order.Add("executed-tests");Facts.Add(new("executed-tests",request,null,result,fixture.ExecutedTests));return Task.FromResult(fixture.ExecutedTests);}
    public TestBuildPlan Plan(TestBuildPlanRequest request){Order.Add("plan");Plans.Add(request);return fixture.Plan;}
    public TestBuildAdmission Evaluate(TestBuildPlan plan,TestBuildEvidence evidence)
    {Order.Add("build-admission");Builds.Add(new(plan,evidence));return new(fixture.Name!="different-executed-test-binary",fixture.Name=="different-executed-test-binary" ? "STALE_TEST_BINARY" : "OK");}
    public TrxAdmissionResult Evaluate(TrxAdmissionRequest request)
    {Order.Add("trx-admission");Trxs.Add(new(request.TrxBytes is {} bytes ? Convert.ToBase64String(bytes.Span) : null,request.ExpectedClasses));return fixture.TrxExpected;}
    public Task<ReadOnlyMemory<byte>> ReadAsync(string relativePath,CancellationToken cancellationToken=default)
    {Order.Add("read:"+relativePath);Reads.Add(relativePath);return Task.FromResult(fixture.Trx.Request.TrxBytes!.Value);}
    public Task<StoredArtifact> WriteAsync(Artifact artifact,CancellationToken cancellationToken=default)
    {Writes.Add("unexpected-write");throw new InvalidOperationException("No output format is authorized by this producer contract increment.");}
    public ProducerObservation Observe(TestGateProducerResult result)=>new(fixture.Request,result,Processes,Results,Facts,Captures,Plans,Builds,Trxs,Reads,Writes,Order);
}

public sealed class ScriptedTestGateProducer(TestGateProducerFixture fixture,ProducerMockDependencies dependencies):ITestGateEvidenceProducer
{
    public async Task<TestGateProducerResult> ProduceAsync(TestGateProducerRequest request,CancellationToken cancellationToken=default)
    {
        // Intentionally scripted from the independent expected interaction schedule; not a native implementation.
        var plan=dependencies.Plan(new(request.HostProject,request.TestsProject,request.DotNet));
        await dependencies.CaptureSourceAsync(request,TestGateSnapshotPoint.BeforeBuild,cancellationToken);
        dependencies.ReadRunningHostModuleId();
        for(var index=0;index<fixture.ExpectedProcesses.Count;index++)
        {
            var result=await dependencies.RunAsync(fixture.ExpectedProcesses[index],cancellationToken);
            await dependencies.ReadAsync(result,false,cancellationToken);await dependencies.ReadAsync(result,true,cancellationToken);
            if(index==0&&fixture.Name!="host-build-failure")
            {await dependencies.ReadBuiltHostAsync(request,cancellationToken);await dependencies.CaptureSourceAsync(request,TestGateSnapshotPoint.AfterHostBuild,cancellationToken);}
            if(index==1)
            {await dependencies.ReadBuiltTestsAsync(request,cancellationToken);await dependencies.CaptureSourceAsync(request,TestGateSnapshotPoint.AfterTestsBuild,cancellationToken);}
            if(index==2)
            {
                await dependencies.ReadExecutedTestsAsync(request,result,cancellationToken);
                await dependencies.CaptureSourceAsync(request,TestGateSnapshotPoint.AfterExecution,cancellationToken);
            }
        }
        if(fixture.ExpectedBuild is {} evidence)
        {
            dependencies.Evaluate(plan,evidence);
            if(fixture.ExpectedResult.TrxAdmission is not null)
            {var bytes=await dependencies.ReadAsync(fixture.TrxPath,cancellationToken);dependencies.Evaluate(new(bytes,request.ExpectedClasses));}
        }
        return fixture.ExpectedResult;
    }
}

public static class TestGateProducerCases
{
    public static IEnumerable<object[]> Data=>TestGateProducerFixture.Names.Select(name=>new object[]{name});
    public static JsonNode Json(ProducerObservation value)=>JsonSerializer.SerializeToNode(value)!;
    public static void AssertContract(TestGateProducerFixture fixture,JsonNode actual)
    {
        var expected=Expected(fixture);
        Assert.Equal(new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(expected)),new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(actual)));
    }
    public static ProducerObservation Expected(TestGateProducerFixture fixture)
    {
        var facts=new List<ProducerFactCall>{new("source",fixture.Request,TestGateSnapshotPoint.BeforeBuild,null,fixture.Snapshots[0]),new("loaded-host",null,null,null,fixture.LoadedHost)};
        if(fixture.Name!="host-build-failure")facts.AddRange([new("built-host",fixture.Request,null,null,fixture.Host),new("source",fixture.Request,TestGateSnapshotPoint.AfterHostBuild,null,fixture.Snapshots[1])]);
        if(fixture.ExpectedProcesses.Count>1)facts.AddRange([new("built-tests",fixture.Request,null,null,fixture.BuiltTests),new("source",fixture.Request,TestGateSnapshotPoint.AfterTestsBuild,null,fixture.Snapshots[2])]);
        if(fixture.ExpectedProcesses.Count==3)facts.AddRange([new("executed-tests",fixture.Request,null,fixture.Processes[2],fixture.ExecutedTests),new("source",fixture.Request,TestGateSnapshotPoint.AfterExecution,null,fixture.Snapshots[3])]);
        var captures=fixture.Processes.SelectMany((process,index)=>new[]{new ProducerCaptureCall(process,false,Convert.ToBase64String(Encoding.UTF8.GetBytes("output-"+index))),new ProducerCaptureCall(process,true,Convert.ToBase64String(Encoding.UTF8.GetBytes("error-"+index)))}).ToArray();
        return new(fixture.Request,fixture.ExpectedResult,fixture.ExpectedProcesses,fixture.Processes,facts,captures,
            [new(fixture.Request.HostProject,fixture.Request.TestsProject,fixture.Request.DotNet)],
            fixture.ExpectedBuild is {} build ? [new(fixture.Plan,build)] : [],
            fixture.ExpectedResult.TrxAdmission is not null ? [new(Convert.ToBase64String(fixture.Trx.Request.TrxBytes!.Value.Span),fixture.Request.ExpectedClasses)] : [],
            fixture.ExpectedResult.TrxAdmission is not null ? [fixture.TrxPath] : [],[],fixture.ExpectedOrder);
    }
    public static IEnumerable<object[]> Controls
    {
        get
        {
            foreach(var name in TestGateProducerFixture.Names)
            {
                var node=Json(Expected(new(name)));var index=0;
                foreach(var member in ReviewOfficialTransportCases.Leaves(node))yield return new object[]{name,index++,member.Label};
            }
        }
    }
}

[Trait("Gate","Mock")]
public sealed class TestGateProducerMockTests
{
    [Theory,MemberData(nameof(TestGateProducerCases.Data),MemberType=typeof(TestGateProducerCases))]
    public async Task CorrectTypedProducerMock(string name)
    {
        var fixture=new TestGateProducerFixture(name);var dependencies=new ProducerMockDependencies(fixture);
        var result=await new ScriptedTestGateProducer(fixture,dependencies).ProduceAsync(fixture.Request);
        TestGateProducerCases.AssertContract(fixture,TestGateProducerCases.Json(dependencies.Observe(result)));
    }
    [Theory,MemberData(nameof(TestGateProducerCases.Controls),MemberType=typeof(TestGateProducerCases))]
    public void EachFullTypedInputFactAndInteractionControlIsRejected(string name,int memberIndex,string label)
    {
        var fixture=new TestGateProducerFixture(name);var actual=TestGateProducerCases.Json(TestGateProducerCases.Expected(fixture));
        var members=ReviewOfficialTransportCases.Leaves(actual).ToArray();Assert.Equal(label,members[memberIndex].Label);
        ReviewOfficialTransportCases.Mutate(actual,members[memberIndex].Path);
        Assert.NotNull(Record.Exception(()=>TestGateProducerCases.AssertContract(fixture,actual)));
    }
}
