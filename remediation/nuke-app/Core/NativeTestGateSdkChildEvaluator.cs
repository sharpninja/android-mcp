using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AndroidMcp.BuildTool.Core;

// Parent-side only: the existing process and artifact authorities carry the
// exact current monolith into a short-lived SDK evaluation process.
public sealed class NativeTestGateSdkChildEvaluator(TestGateNativeFactsConfiguration configuration,
    TestGateBinaryIdentity monolith,long monolithLength,IReadOnlyList<StoredArtifact> runtimeFiles,
    IPinnedArtifactReader pins,ICreateNewArtifactStore artifacts,IProcessRunner processes,
    IProcessCaptureReader captures,ITestGateNonceSource nonces):ITestGateSdkEvaluator
{
    private readonly CanonicalJson canonical=new();
    private static readonly JsonSerializerOptions Options=new(){UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow};

    public Task<TestGateSdkEvaluation> EvaluateAsync(TestGateSdkEvaluationRequest request,CancellationToken cancellationToken=default)
    {
        var run=Relative(request.Run.WorkspaceRoot,request.Run.ReceiptDirectory);
        var nonce=nonces.CreateNonce();
        if(nonce.Length!=64||nonce.Any(character=>!char.IsAsciiHexDigitLower(character)))throw Error("INPUT_SCOPE_VIOLATION");
        return EvaluateAsync(new TestGateSdkChildRequest(new(request,configuration.ApprovedReadRoots),monolith,
            runtimeFiles,run+"/sdk-evaluation-"+nonce+"/evaluation.json"),cancellationToken);
    }

    // Explicit envelope seam permits the SAME typed finite transport cases to
    // observe every process argument, read, strict-new write and raw capture.
    public async Task<TestGateSdkEvaluation> EvaluateAsync(TestGateSdkChildRequest request,CancellationToken cancellationToken=default)
    {
        var native=request.Native;var evaluation=native.Evaluation;var run=evaluation.Run;
        string requestPath;string processDirectory;
        try
        {
            var runPath=Relative(run.WorkspaceRoot,run.ReceiptDirectory);
            var components=ArtifactPaths.Segments(runPath);
            if(components.Length!=3||components[0]!="artifacts"||components[1]!="tests")throw new ArgumentException("Invalid run layout.");
            ArtifactPaths.Resolve(run.WorkspaceRoot,request.EvaluationRelativePath);
            var output=ArtifactPaths.Segments(request.EvaluationRelativePath);
            if(output.Length!=5||!request.EvaluationRelativePath.StartsWith(runPath+"/sdk-evaluation-",StringComparison.Ordinal)||output[^1]!="evaluation.json")
                throw new ArgumentException("Invalid evaluation output scope.");
            var parent=request.EvaluationRelativePath[..request.EvaluationRelativePath.LastIndexOf('/')];
            requestPath=parent+"/request.json";
            processDirectory=ArtifactPaths.Resolve(run.WorkspaceRoot,parent+"/process");
            if(request.RuntimeFiles.Count!=2)throw new ArgumentException("Explicit runtimeconfig and deps pins required.");
            Relative(run.WorkspaceRoot,request.Monolith.AbsolutePath);
            foreach(var pin in request.RuntimeFiles)ArtifactPaths.Resolve(run.WorkspaceRoot,pin.RelativePath);
        }
        catch(ArgumentException){throw Error("INPUT_SCOPE_VIOLATION");}
        if(evaluation.Configuration!=configuration.Configuration||evaluation.TargetFramework!=configuration.TargetFramework)
            throw Error("SDK_CONFIGURATION_MISMATCH");
        NativeTestGateSdkEvaluator.ValidateInventoryRequest(evaluation,configuration);
        if(!Same(evaluation.Sdk,configuration.Sdk)||!Same(native.ApprovedReadRoots,configuration.ApprovedReadRoots))throw Error("SDK_PIN_MISMATCH");

        var imagePin=new StoredArtifact(Relative(run.WorkspaceRoot,request.Monolith.AbsolutePath),request.Monolith.Sha256,
            monolithLength,"application/octet-stream");
        ReadOnlyMemory<byte> imageBytes;
        try{imageBytes=await pins.ReadAsync(imagePin,cancellationToken);}
        catch(Exception error)when(error is PinnedArtifactException or FileNotFoundException){throw Error("SDK_CHILD_CODE_PIN_INVALID");}
        var identity=TestGateManagedPeIdentity.Observe(request.Monolith.AbsolutePath,imageBytes);
        if(identity.ModuleId!=request.Monolith.ModuleId)throw Error("SDK_CHILD_CODE_MVID_INVALID");
        foreach(var runtime in request.RuntimeFiles)
            try{await pins.ReadAsync(runtime,cancellationToken);}
            catch(Exception error)when(error is PinnedArtifactException or FileNotFoundException){throw Error("SDK_CHILD_CODE_PIN_INVALID");}

        var requestBytes=canonical.Serialize(JsonSerializer.SerializeToElement(request));
        var requestPin=await artifacts.WriteNewAsync(new(requestPath,requestBytes,"application/json"),cancellationToken);
        var process=new ProcessRequest(run.DotNet,["exec","--runtimeconfig",ArtifactPaths.Resolve(run.WorkspaceRoot,request.RuntimeFiles[0].RelativePath),
            "--depsfile",ArtifactPaths.Resolve(run.WorkspaceRoot,request.RuntimeFiles[1].RelativePath),request.Monolith.AbsolutePath,
            "--sdk-evaluation-once","--pipeline-request",ArtifactPaths.Resolve(run.WorkspaceRoot,requestPath),
            "--pipeline-request-sha256",requestPin.Sha256,"--pipeline-request-length",requestPin.Length.ToString(CultureInfo.InvariantCulture)],
            run.WorkspaceRoot,new Dictionary<string,string>(),processDirectory);
        ProcessResult terminal;
        try{terminal=await processes.RunAsync(process,cancellationToken);}
        catch(IOException){throw Error("SDK_CHILD_OUTCOME_UNCERTAIN");}
        var stdout=await captures.ReadAsync(terminal,false,cancellationToken);
        await captures.ReadAsync(terminal,true,cancellationToken);
        if(terminal.ExitCode!=0||terminal.Cancelled)throw Error("SDK_CHILD_PROCESS_FAILED");
        TestGateSdkChildReceipt? receipt;
        try{receipt=JsonSerializer.Deserialize<TestGateSdkChildReceipt>(canonical.Serialize(stdout),Options);}
        catch(Exception error)when(error is JsonException or FormatException){throw Error("SDK_CHILD_RECEIPT_INVALID");}
        if(receipt is null)throw Error("SDK_CHILD_RECEIPT_INVALID");
        if(receipt.RequestSha256!=requestPin.Sha256||receipt.LoadedMonolith!=request.Monolith||
            receipt.Evaluation?.RelativePath!=request.EvaluationRelativePath)throw Error("SDK_CHILD_RECEIPT_MISMATCH");
        ReadOnlyMemory<byte> evaluatedBytes;
        try{evaluatedBytes=await pins.ReadAsync(receipt.Evaluation,cancellationToken);}
        catch(Exception error)when(error is PinnedArtifactException or FileNotFoundException){throw Error("SDK_CHILD_EVALUATION_PIN_INVALID");}
        TestGateSdkEvaluation? result;
        try{result=JsonSerializer.Deserialize<TestGateSdkEvaluation>(canonical.Serialize(evaluatedBytes),Options);}
        catch(Exception error)when(error is JsonException or FormatException){throw Error("SDK_CHILD_EVALUATION_PIN_INVALID");}
        if(result?.SdkFiles is null||!Same(result.SdkFiles,evaluation.Sdk.AssemblyFiles))throw Error("SDK_PIN_MISMATCH");
        NativeTestGateSdkEvaluator.ValidateInventoryResult(evaluation.ProjectInventory,result.Projects);
        return result;
    }

    private bool Same<T>(T left,T right)=>canonical.Serialize(JsonSerializer.SerializeToElement(left)).AsSpan().SequenceEqual(
        canonical.Serialize(JsonSerializer.SerializeToElement(right)));
    private static string Relative(string root,string path)
    {
        var relative=Path.GetRelativePath(root,path).Replace('\\','/');ArtifactPaths.Resolve(root,relative);return relative;
    }
    private static TestGateFactsException Error(string code)=>new(code,"Scoped SDK child evaluation rejected: "+code);
}
