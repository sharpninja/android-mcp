using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review.Staging;

public sealed record ReferenceQueryObservation(TestGateResolvedReferenceQueryRequest Request,TestGateResolvedReferenceQueryResult Result,
    IReadOnlyList<ProcessRequest> Processes,IReadOnlyList<ProducerCaptureCall> Captures,
    IReadOnlyList<NativeFactsWrite> Writes,IReadOnlyList<string> Reads,IReadOnlyList<string> Order);
public sealed class ReferenceQueryFixture
{
    public const string Target="ReferenceCopyLocalPathsOutputGroup";
    public static readonly string[] Names=["resolved-copy","renamed-copy","missing-target-path","conflicting-copy",
        "wrong-project","wrong-config","wrong-sdk","missing-source-metadata","escape-target-path","missing-copy",
        "malformed-json","target-failed","process-failed","process-uncertain","property-injection"];
    public string Name { get; }
    public TestGateResolvedReferenceQueryRequest Request { get; }
    public ProcessRequest Process { get; }
    public ProcessResult Terminal { get; }
    public byte[] StandardOutput { get; }
    public byte[] StandardError { get; }=Encoding.UTF8.GetBytes("retained-sdk-diagnostic");
    public TestGateResolvedReferenceQueryResult Result { get; }
    public string Prefix=>"artifacts/tests/current-run/native-reference-query";
    public byte[] RequestBytes=>NativeFactsFixture.Canonical(Request);
    public IReadOnlyList<(string Path,byte[] Bytes)> Proofs { get; }
    public ReferenceQueryFixture(string name)
    {
        Name=name;var native=new NativeFactsFixture("origin-complete");
        Request=new(native.Request,native.Configuration.Sdk,native.Host.AbsolutePath,name=="property-injection" ? "Debug;Injected=true" : "Debug","net10.0");
        Process=new(Request.Run.DotNet,["msbuild",Request.Run.TestsProject,
            "-getTargetResult:"+Target,"-getProperty:OutDir,MSBuildProjectFullPath,Configuration,TargetFramework,MSBuildToolsPath",
            "-property:BuildProjectReferences=false","-property:Configuration="+Request.Configuration,
            "-property:TargetFramework="+Request.TargetFramework,"-nologo","-verbosity:quiet"],Request.Run.WorkspaceRoot,
            new Dictionary<string,string>(),Request.Run.WorkspaceRoot+"/"+Prefix+"/process");
        Terminal=new(name=="process-failed" ? 1 : 0,Process.ReceiptDirectory+"/stdout.bin",Process.ReceiptDirectory+"/stderr.bin",DateTimeOffset.UnixEpoch,DateTimeOffset.UnixEpoch,false);
        var targetPath=name=="renamed-copy" ? "deps/ActualRenamedHost.dll" : name=="escape-target-path" ? "../../../../../private/escape.dll" : "App.dll";
        var item=new Dictionary<string,string>{{"FullPath",Request.HostOutputPath},{"MSBuildSourceProjectFile",Request.Run.HostProject},{"TargetPath",targetPath}};
        if(name=="missing-target-path")item.Remove("TargetPath");if(name=="missing-source-metadata")item.Remove("MSBuildSourceProjectFile");
        var items=new List<Dictionary<string,string>>{item};
        if(name=="conflicting-copy")items.Add(new(item){["TargetPath"]="other/Second.dll"});
        if(name=="missing-copy")items.Clear();
        var properties=new Dictionary<string,string>{{"OutDir","bin/"},{"MSBuildProjectFullPath",name=="wrong-project" ? "/fixture/app/Other.csproj" : Request.Run.TestsProject},
            {"Configuration",name=="wrong-config" ? "Release" : "Debug"},{"TargetFramework","net10.0"},{"MSBuildToolsPath",name=="wrong-sdk" ? "/fixture/other-sdk" : Request.Sdk.SdkDirectory}};
        var stdout=JsonSerializer.SerializeToUtf8Bytes(new{Properties=properties,TargetResults=new Dictionary<string,object>{{Target,new{Result=name=="target-failed" ? "Failure" : "Success",Items=items}}}});
        StandardOutput=name=="malformed-json" ? stdout[..^1] : stdout;
        var code=name switch
        {
            "resolved-copy" or "renamed-copy"=>"OK","property-injection"=>"RESOLUTION_ARGUMENT_INVALID",
            "process-uncertain"=>"RESOLUTION_OUTCOME_UNCERTAIN","process-failed"=>"RESOLUTION_PROCESS_FAILED","target-failed"=>"RESOLUTION_TARGET_FAILED",
            "wrong-sdk"=>"SDK_PIN_MISMATCH","wrong-project" or "wrong-config"=>"RESOLUTION_IDENTITY_MISMATCH",
            "escape-target-path"=>"INPUT_SCOPE_VIOLATION","missing-copy"=>"RESOLVED_REFERENCE_MISSING","conflicting-copy"=>"RESOLVED_REFERENCE_CONFLICT",_=>"RESOLUTION_METADATA_INVALID"
        };
        var proofs=new List<(string Path,byte[] Bytes)>();var copies=new List<TestGateResolvedCopy>();
        var receipts=new List<StoredArtifact>();
        if(name!="property-injection")receipts.Add(NativeFactsFixture.Stored(Prefix+"/query.json",RequestBytes));
        if(name is not ("property-injection" or "process-uncertain"))
        {
            receipts.Add(new(Prefix+"/process/stdout.bin",ContentHash.Sha256(StandardOutput),StandardOutput.Length,"application/octet-stream"));
            receipts.Add(new(Prefix+"/process/stderr.bin",ContentHash.Sha256(StandardError),StandardError.Length,"application/octet-stream"));
        }
        if(code=="OK")
        {
            var destination="/fixture/app/Tests/bin/"+targetPath;
            var proof=new TestGateResolvedCopyProof(Request.Run.ReceiptDirectory,Request.Run.TestsProject,Request.HostOutputPath,destination,
                Request.Run.HostProject,Request.Run.DotNet,Request.Configuration,Request.TargetFramework);
            var bytes=NativeFactsFixture.Canonical(proof);var stored=NativeFactsFixture.Stored(Prefix+"/reference-0.json",bytes);
            proofs.Add((stored.RelativePath,bytes));receipts.Add(stored);copies.Add(new(Request.HostOutputPath,destination,Request.Run.HostProject,stored));
        }
        Proofs=proofs;Result=new(code,copies,receipts);
    }
}
public sealed class ReferenceQueryDependencies(ReferenceQueryFixture fixture):IProcessRunner,IProcessCaptureReader,IArtifactStore
{
    public List<ProcessRequest> Processes { get; }=[];public List<ProducerCaptureCall> Captures { get; }=[];
    public List<NativeFactsWrite> Writes { get; }=[];public List<string> Reads { get; }=[];public List<string> Order { get; }=[];
    public Task<ProcessResult> RunAsync(ProcessRequest request,CancellationToken cancellationToken=default)
    {
        Order.Add("process");Processes.Add(request);
        var receiptPrefix=Path.GetRelativePath(fixture.Request.Run.WorkspaceRoot,request.ReceiptDirectory).Replace('\\','/').TrimEnd('/')+"/";
        if(Writes.Any(entry=>entry.Path.StartsWith(receiptPrefix,StringComparison.Ordinal)))throw new InvalidOperationException("Process receipt directory must be new or empty.");
        if(fixture.Name=="process-uncertain")throw new IOException("Native process outcome is uncertain; no retry.");
        return Task.FromResult(fixture.Terminal);
    }
    public Task<ReadOnlyMemory<byte>> ReadAsync(ProcessResult result,bool standardError,CancellationToken cancellationToken=default)
    {Order.Add(standardError ? "stderr" : "stdout");var bytes=standardError ? fixture.StandardError : fixture.StandardOutput;Captures.Add(new(result,standardError,Convert.ToBase64String(bytes)));return Task.FromResult<ReadOnlyMemory<byte>>(bytes);}
    public Task<StoredArtifact> WriteAsync(Artifact artifact,CancellationToken cancellationToken=default)
    {Order.Add("write:"+artifact.RelativePath);var bytes=artifact.Bytes.ToArray();Writes.Add(new(artifact.RelativePath,Convert.ToBase64String(bytes),artifact.MediaType));return Task.FromResult(NativeFactsFixture.Stored(artifact.RelativePath,bytes));}
    public Task<ReadOnlyMemory<byte>> ReadAsync(string relativePath,CancellationToken cancellationToken=default)
    {Reads.Add(relativePath);throw new InvalidOperationException("Native query never reads caller-supplied positive metadata.");}
    public ReferenceQueryObservation Observe(TestGateResolvedReferenceQueryResult result)=>new(fixture.Request,result,Processes,Captures,Writes,Reads,Order);
}
public sealed class ScriptedReferenceQueryCases
{
    public async Task<ReferenceQueryObservation> ExecuteAsync(ReferenceQueryFixture fixture)
    {
        var io=new ReferenceQueryDependencies(fixture);
        if(fixture.Name=="property-injection")return io.Observe(fixture.Result);
        await io.WriteAsync(new(fixture.Prefix+"/query.json",fixture.RequestBytes,"application/json"));
        ProcessResult process;
        try{process=await io.RunAsync(fixture.Process);}catch(IOException){return io.Observe(fixture.Result);}
        await io.ReadAsync(process,false);await io.ReadAsync(process,true);
        foreach(var proof in fixture.Proofs)await io.WriteAsync(new(proof.Path,proof.Bytes,"application/json"));
        return io.Observe(fixture.Result);
    }
}
public static class ReferenceQueryCases
{
    public static IEnumerable<object[]> Data=>ReferenceQueryFixture.Names.Select(name=>new object[]{name});
    public static ReferenceQueryObservation Expected(ReferenceQueryFixture fixture)
    {
        var processes=new List<ProcessRequest>();var captures=new List<ProducerCaptureCall>();var writes=new List<NativeFactsWrite>();var order=new List<string>();
        if(fixture.Name!="property-injection")
        {
            writes.Add(new(fixture.Prefix+"/query.json",Convert.ToBase64String(fixture.RequestBytes),"application/json"));order.Add("write:"+fixture.Prefix+"/query.json");
            processes.Add(fixture.Process);order.Add("process");
            if(fixture.Name!="process-uncertain")
            {
                captures.Add(new(fixture.Terminal,false,Convert.ToBase64String(fixture.StandardOutput)));captures.Add(new(fixture.Terminal,true,Convert.ToBase64String(fixture.StandardError)));order.AddRange(["stdout","stderr"]);
                foreach(var proof in fixture.Proofs){writes.Add(new(proof.Path,Convert.ToBase64String(proof.Bytes),"application/json"));order.Add("write:"+proof.Path);}
            }
        }
        return new(fixture.Request,fixture.Result,processes,captures,writes,[],order);
    }
    public static JsonNode Json(ReferenceQueryObservation value)=>JsonSerializer.SerializeToNode(value)!;
    public static void AssertContract(ReferenceQueryFixture fixture,JsonNode actual)=>Assert.Equal(
        new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(Expected(fixture))),new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(actual)));
    public static IEnumerable<object[]> Controls
    {
        get
        {
            foreach(var name in ReferenceQueryFixture.Names)
            {
                var node=Json(Expected(new(name)));var index=0;
                foreach(var member in ReviewOfficialTransportCases.Leaves(node))yield return new object[]{name,index++,member.Label};
            }
        }
    }
}
[Trait("Gate","Mock")]
public sealed class TestGateResolvedReferenceQueryMockTests
{
    [Fact]
    public async Task MetadataParentDoesNotPrepopulateFreshProcessChild()
    {
        var fixture=new ReferenceQueryFixture("resolved-copy");var io=new ReferenceQueryDependencies(fixture);
        await io.WriteAsync(new(fixture.Prefix+"/query.json",fixture.RequestBytes,"application/json"));
        Assert.Equal(fixture.Terminal,await io.RunAsync(fixture.Process));
        Assert.Equal(fixture.Request.Run.WorkspaceRoot+"/"+fixture.Prefix+"/process",io.Processes.Single().ReceiptDirectory);
        Assert.Single(io.Writes);Assert.Empty(io.Captures);
    }
    [Fact]
    public async Task MetadataParentCannotBeReusedAsProcessDirectory()
    {
        var fixture=new ReferenceQueryFixture("resolved-copy");var io=new ReferenceQueryDependencies(fixture);
        await io.WriteAsync(new(fixture.Prefix+"/query.json",fixture.RequestBytes,"application/json"));
        var error=await Assert.ThrowsAsync<InvalidOperationException>(()=>io.RunAsync(fixture.Process with {ReceiptDirectory=fixture.Request.Run.WorkspaceRoot+"/"+fixture.Prefix}));
        Assert.Equal("Process receipt directory must be new or empty.",error.Message);Assert.Single(io.Processes);Assert.Single(io.Writes);Assert.Empty(io.Captures);
    }
    [Fact]
    public async Task PrepopulatedProcessChildIsRejectedBeforeCaptureOrSuccess()
    {
        var fixture=new ReferenceQueryFixture("resolved-copy");var io=new ReferenceQueryDependencies(fixture);
        await io.WriteAsync(new(fixture.Prefix+"/process/query.json",fixture.RequestBytes,"application/json"));
        var error=await Assert.ThrowsAsync<InvalidOperationException>(()=>io.RunAsync(fixture.Process));
        Assert.Equal("Process receipt directory must be new or empty.",error.Message);Assert.Single(io.Processes);Assert.Single(io.Writes);Assert.Empty(io.Captures);
    }
    [Theory,MemberData(nameof(ReferenceQueryCases.Data),MemberType=typeof(ReferenceQueryCases))]
    public async Task CorrectTypedNativeReferenceQueryMock(string name)
    {var fixture=new ReferenceQueryFixture(name);ReferenceQueryCases.AssertContract(fixture,ReferenceQueryCases.Json(await new ScriptedReferenceQueryCases().ExecuteAsync(fixture)));}
    [Theory,MemberData(nameof(ReferenceQueryCases.Controls),MemberType=typeof(ReferenceQueryCases))]
    public async Task EachExactArgScopeRawCaptureHashMetadataAndNoRetryFieldRejects(string name,int memberIndex,string label)
    {
        var fixture=new ReferenceQueryFixture(name);var actual=ReferenceQueryCases.Json(await new ScriptedReferenceQueryCases().ExecuteAsync(fixture));
        ReferenceQueryCases.AssertContract(fixture,actual);var members=ReviewOfficialTransportCases.Leaves(actual).ToArray();Assert.Equal(label,members[memberIndex].Label);
        ReviewOfficialTransportCases.Mutate(actual,members[memberIndex].Path);
        Assert.NotNull(Record.Exception(()=>ReferenceQueryCases.AssertContract(fixture,actual)));
    }
}
