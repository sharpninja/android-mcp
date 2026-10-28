using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review.Staging;

public sealed record SdkChildObservation(string Code,TestGateSdkEvaluation? Evaluation,
    IReadOnlyList<StoredArtifact> PinReads,IReadOnlyList<NativeFactsWrite> RequestWrites,
    IReadOnlyList<ProcessRequest> Processes,IReadOnlyList<ProducerCaptureCall> Captures,
    IReadOnlyList<string> ArtifactReads,IReadOnlyList<string> Order);
public sealed class SdkChildFixture
{
    public const string Nonce="11111111111111111111111111111111";
    public static readonly string[] Names=["native-source-graph","image-hash-mismatch","image-mvid-mismatch",
        "runtime-pin-mismatch","escaped-output","child-process-failed","child-cancelled","uncertain-process",
        "malformed-receipt","wrong-request-receipt","wrong-loaded-image","wrong-evaluation-scope",
        "missing-evaluation","evaluation-hash-mismatch","wrong-sdk-evaluation"];
    public string Name { get; }
    private readonly string requestNonce;
    public TestGateSdkNativeOpenRequest Native { get; }
    public TestGateSdkChildRequest Request { get; }
    public TestGateSdkEvaluation Evaluation { get; }
    public Dictionary<string,byte[]> Files { get; }=new(StringComparer.Ordinal);
    public string Prefix=>"artifacts/tests/current-run/sdk-evaluation-"+requestNonce;
    public string RequestPath=>Prefix+"/request.json";
    public string EvaluationPath=>Prefix+"/evaluation.json";
    public byte[] RequestBytes=>NativeFactsFixture.Canonical(Request);
    public StoredArtifact RequestPin=>NativeFactsFixture.Stored(RequestPath,RequestBytes);
    public TestGateSdkChildReceipt Receipt { get; }
    public byte[] StandardOutput { get; }
    public byte[] StandardError { get; }=Encoding.UTF8.GetBytes("retained private child SDK diagnostics");
    public ProcessRequest Process { get; }
    public ProcessResult Terminal { get; }
    public string Code=>Name switch
    {
        "image-hash-mismatch" or "runtime-pin-mismatch"=>"SDK_CHILD_CODE_PIN_INVALID",
        "image-mvid-mismatch"=>"SDK_CHILD_CODE_MVID_INVALID","escaped-output"=>"INPUT_SCOPE_VIOLATION",
        "child-process-failed" or "child-cancelled"=>"SDK_CHILD_PROCESS_FAILED","uncertain-process"=>"SDK_CHILD_OUTCOME_UNCERTAIN",
        "malformed-receipt"=>"SDK_CHILD_RECEIPT_INVALID","wrong-request-receipt" or "wrong-loaded-image" or "wrong-evaluation-scope"=>"SDK_CHILD_RECEIPT_MISMATCH",
        "missing-evaluation" or "evaluation-hash-mismatch"=>"SDK_CHILD_EVALUATION_PIN_INVALID","wrong-sdk-evaluation"=>"SDK_PIN_MISMATCH",_=>"OK"
    };
    public bool Launches=>Name is not ("image-hash-mismatch" or "image-mvid-mismatch" or "runtime-pin-mismatch" or "escaped-output");
    public bool HasCaptures=>Launches&&Name!="uncertain-process";
    public bool ReadsEvaluation=>Name is "native-source-graph" or "missing-evaluation" or "evaluation-hash-mismatch" or "wrong-sdk-evaluation";
    public IReadOnlyList<StoredArtifact> ExpectedPins
    {
        get
        {
            var image=new StoredArtifact("bin/App.dll",Request.Monolith.Sha256,Files["bin/App.dll"].Length,"application/octet-stream");
            if(Name=="escaped-output")return [];
            if(Name is "image-hash-mismatch" or "image-mvid-mismatch")return [image];
            if(Name=="runtime-pin-mismatch")return [image,Request.RuntimeFiles[0]];
            return [image,..Request.RuntimeFiles];
        }
    }
    public SdkChildFixture(string name,string? nonce=null)
    {
        Name=name;requestNonce=nonce??Nonce;var source=new SdkSourceEvaluationFixture("source-graph");Native=source.OpenRequest;Evaluation=source.Expected;
        Files["bin/App.dll"]=NativeFactsFixture.Pe(NativeFactsFixture.Module(3));
        Files["bin/App.runtimeconfig.json"]=JsonSerializer.SerializeToUtf8Bytes(new{runtimeOptions=new{tfm="net10.0"}});
        Files["bin/App.deps.json"]=JsonSerializer.SerializeToUtf8Bytes(new{runtimeTarget=new{name=".NETCoreApp,Version=v10.0"}});
        var image=new TestGateBinaryIdentity("/fixture/app/bin/App.dll",ContentHash.Sha256(Files["bin/App.dll"]),NativeFactsFixture.Module(3));
        if(name=="image-hash-mismatch")image=image with {Sha256=new string('e',64)};
        if(name=="image-mvid-mismatch")image=image with {ModuleId=NativeFactsFixture.Module(2)};
        var runtime=new[]{NativeFactsFixture.Stored("bin/App.runtimeconfig.json",Files["bin/App.runtimeconfig.json"]),
            NativeFactsFixture.Stored("bin/App.deps.json",Files["bin/App.deps.json"])};
        if(name=="runtime-pin-mismatch")runtime[0]=runtime[0] with {Sha256=new string('e',64)};
        Request=new(Native,image,runtime,name=="escaped-output" ? "../outside/evaluation.json" : EvaluationPath);
        var returned=name=="wrong-sdk-evaluation" ? Evaluation with {SdkFiles=[]} : Evaluation;
        var evaluationBytes=NativeFactsFixture.Canonical(returned);Files[EvaluationPath]=evaluationBytes;
        var evaluationPin=NativeFactsFixture.Stored(EvaluationPath,evaluationBytes);
        if(name=="evaluation-hash-mismatch")evaluationPin=evaluationPin with {Sha256=new string('e',64)};
        if(name=="wrong-evaluation-scope")evaluationPin=evaluationPin with {RelativePath="artifacts/tests/other-run/evaluation.json"};
        if(name=="missing-evaluation")Files.Remove(EvaluationPath);
        Receipt=new(name=="wrong-request-receipt" ? new string('f',64) : RequestPin.Sha256,
            name=="wrong-loaded-image" ? image with {ModuleId=NativeFactsFixture.Module(2)} : image,evaluationPin);
        var stdout=NativeFactsFixture.Canonical(Receipt);StandardOutput=name=="malformed-receipt" ? stdout[..^1] : stdout;
        Process=new(Native.Evaluation.Run.DotNet,["exec","--runtimeconfig","/fixture/app/bin/App.runtimeconfig.json",
            "--depsfile","/fixture/app/bin/App.deps.json",image.AbsolutePath,"--sdk-evaluation-once",
            "--pipeline-request","/fixture/app/"+RequestPath,"--pipeline-request-sha256",RequestPin.Sha256,
            "--pipeline-request-length",RequestPin.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)],
            Native.Evaluation.Run.WorkspaceRoot,new Dictionary<string,string>(),"/fixture/app/"+Prefix+"/process");
        Terminal=new(name=="child-process-failed" ? 1 : 0,Process.ReceiptDirectory+"/stdout.bin",Process.ReceiptDirectory+"/stderr.bin",
            DateTimeOffset.UnixEpoch,DateTimeOffset.UnixEpoch,name=="child-cancelled");
    }
    public SdkChildObservation Expected
    {
        get
        {
            var reads=ExpectedPins.Select(pin=>pin.RelativePath).ToList();
            var order=ExpectedPins.Select(pin=>"pin:"+pin.RelativePath).ToList();
            if(Launches)order.AddRange(["write-new:"+RequestPath,"process"]);
            if(HasCaptures)order.AddRange(["stdout","stderr"]);
            if(ReadsEvaluation){order.Add("pin:"+EvaluationPath);reads.Add(EvaluationPath);}
            return new(Code,Code=="OK" ? Evaluation : null,
                ReadsEvaluation ? [..ExpectedPins,Receipt.Evaluation] : ExpectedPins,
                Launches ? [new(RequestPath,Convert.ToBase64String(RequestBytes),"application/json",true)] : [],
                Launches ? [Process] : [],HasCaptures ? [new(Terminal,false,Convert.ToBase64String(StandardOutput)),
                    new(Terminal,true,Convert.ToBase64String(StandardError))] : [],reads,order);
        }
    }
}
public sealed class SdkChildDependencies(SdkChildFixture fixture):IPinnedArtifactReader,IProcessRunner,IProcessCaptureReader,ICreateNewArtifactStore
{
    public List<StoredArtifact> Pins { get; }=[];public List<NativeFactsWrite> Writes { get; }=[];
    public List<ProcessRequest> Processes { get; }=[];public List<ProducerCaptureCall> Captures { get; }=[];
    public List<string> Reads { get; }=[];public List<string> Order { get; }=[];
    public async Task<ReadOnlyMemory<byte>> ReadAsync(StoredArtifact pin,CancellationToken cancellationToken=default)
    {Pins.Add(pin);Order.Add("pin:"+pin.RelativePath);return await new PinnedArtifactReader(this).ReadAsync(pin,cancellationToken);}
    public Task<ReadOnlyMemory<byte>> ReadAsync(string relativePath,CancellationToken cancellationToken=default)
    {Reads.Add(relativePath);return fixture.Files.TryGetValue(relativePath,out var bytes)
        ? Task.FromResult<ReadOnlyMemory<byte>>(bytes) : throw new FileNotFoundException("Exact child output is missing.",relativePath);}
    public Task<StoredArtifact> WriteNewAsync(Artifact artifact,CancellationToken cancellationToken=default)
    {
        Order.Add("write-new:"+artifact.RelativePath);Writes.Add(new(artifact.RelativePath,Convert.ToBase64String(artifact.Bytes.Span),artifact.MediaType,true));
        if(fixture.Files.ContainsKey(artifact.RelativePath))throw new IOException("Existing request retained; no reuse.");
        fixture.Files.Add(artifact.RelativePath,artifact.Bytes.ToArray());return Task.FromResult(NativeFactsFixture.Stored(artifact.RelativePath,artifact.Bytes.ToArray()));
    }
    public Task<StoredArtifact> WriteAsync(Artifact artifact,CancellationToken cancellationToken=default)
        =>throw new InvalidOperationException("Child SDK transport requires strict CreateNew.");
    public Task<ProcessResult> RunAsync(ProcessRequest request,CancellationToken cancellationToken=default)
    {
        Order.Add("process");Processes.Add(request);
        var prefix=Path.GetRelativePath(fixture.Native.Evaluation.Run.WorkspaceRoot,request.ReceiptDirectory).Replace('\\','/').TrimEnd('/')+"/";
        if(Writes.Any(write=>write.Path.StartsWith(prefix,StringComparison.Ordinal)))throw new InvalidOperationException("Process receipt directory must be new or empty.");
        if(fixture.Name=="uncertain-process")throw new IOException("Child outcome uncertain; do not retry.");
        return Task.FromResult(fixture.Terminal);
    }
    public Task<ReadOnlyMemory<byte>> ReadAsync(ProcessResult result,bool standardError,CancellationToken cancellationToken=default)
    {
        Order.Add(standardError ? "stderr" : "stdout");var bytes=standardError ? fixture.StandardError : fixture.StandardOutput;
        Captures.Add(new(result,standardError,Convert.ToBase64String(bytes)));return Task.FromResult<ReadOnlyMemory<byte>>(bytes);
    }
    public SdkChildObservation Observe(string code,TestGateSdkEvaluation? evaluation)=>new(code,evaluation,Pins,Writes,Processes,Captures,Reads,Order);
}
public sealed class ScriptedSdkChildCases
{
    public async Task<SdkChildObservation> ExecuteAsync(SdkChildFixture fixture,SdkChildDependencies? dependencies=null)
    {
        var io=dependencies??new SdkChildDependencies(fixture);
        foreach(var pin in fixture.ExpectedPins)
            try{await io.ReadAsync(pin);}catch(PinnedArtifactException){return io.Observe(fixture.Code,null);}
        if(!fixture.Launches)return io.Observe(fixture.Code,null);
        await io.WriteNewAsync(new(fixture.RequestPath,fixture.RequestBytes,"application/json"));
        try{await io.RunAsync(fixture.Process);}catch(IOException){return io.Observe(fixture.Code,null);}
        await io.ReadAsync(fixture.Terminal,false);await io.ReadAsync(fixture.Terminal,true);
        if(fixture.ReadsEvaluation)
            try{await io.ReadAsync(fixture.Receipt.Evaluation);}catch(Exception error)when(error is PinnedArtifactException or FileNotFoundException){return io.Observe(fixture.Code,null);}
        return io.Observe(fixture.Code,fixture.Code=="OK" ? fixture.Evaluation : null);
    }
}
public static class SdkChildCases
{
    public static IEnumerable<object[]> Data=>SdkChildFixture.Names.Select(name=>new object[]{name});
    public static void AssertContract(SdkChildFixture fixture,JsonNode actual)=>Assert.Equal(
        new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(fixture.Expected)),new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(actual)));
    public static IEnumerable<object[]> Controls
    {
        get
        {
            foreach(var name in SdkChildFixture.Names)
            {
                var members=ReviewOfficialTransportCases.Leaves(JsonSerializer.SerializeToNode(new SdkChildFixture(name).Expected)!).ToArray();
                for(var memberIndex=0;memberIndex<members.Length;memberIndex++)yield return new object[]{name,memberIndex,members[memberIndex].Label};
            }
        }
    }
}
public sealed class TestGateSdkChildMockTests
{
    [Theory,MemberData(nameof(SdkChildCases.Data),MemberType=typeof(SdkChildCases))]
    public async Task Exact_same_monolith_request_response_and_no_retry(string name)
    {
        var fixture=new SdkChildFixture(name);var actual=await new ScriptedSdkChildCases().ExecuteAsync(fixture);
        SdkChildCases.AssertContract(fixture,JsonSerializer.SerializeToNode(actual)!);
    }
    [Theory,MemberData(nameof(SdkChildCases.Controls),MemberType=typeof(SdkChildCases))]
    public void Each_code_tool_scope_request_response_and_coverage_field_mutation_is_rejected(string name,int memberIndex,string label)
    {
        var fixture=new SdkChildFixture(name);var actual=JsonSerializer.SerializeToNode(fixture.Expected)!;
        var member=ReviewOfficialTransportCases.Leaves(actual).ElementAt(memberIndex);Assert.Equal(label,member.Label);
        ReviewOfficialTransportCases.Mutate(actual,member.Path);Assert.ThrowsAny<Exception>(()=>SdkChildCases.AssertContract(fixture,actual));
    }
}
