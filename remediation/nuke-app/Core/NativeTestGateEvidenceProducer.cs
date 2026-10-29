namespace AndroidMcp.BuildTool.Core;

/// <summary>
/// Produces normal test admission from observed native phases. The native fact source is mandatory;
/// no public input supplies source fingerprints, loaded MVIDs, reference hashes or positive evidence.
/// This service never relaunches itself, retries an uncertain process or substitutes an old TRX.
/// </summary>
public sealed class NativeTestGateEvidenceProducer(IProcessRunner runner,IProcessCaptureReader captures,
    ITestGateNativeFacts facts,ITestBuildPlanAdmission buildAdmission,ITrxResultAdmission trxAdmission,
    IArtifactStore workspaceArtifacts):ITestGateEvidenceProducer
{
    public async Task<TestGateProducerResult> ProduceAsync(TestGateProducerRequest request,CancellationToken cancellationToken=default)
    {
        var trxRelative=Relative(request.WorkspaceRoot,Path.Combine(request.ReceiptDirectory,"tests.trx"));
        Relative(request.WorkspaceRoot,request.HostProject);Relative(request.WorkspaceRoot,request.TestsProject);
        var plan=buildAdmission.Plan(new(request.HostProject,request.TestsProject,request.DotNet));
        var sourceProofs=new List<StoredArtifact>();var captureProofs=new List<StoredArtifact>();
        StoredArtifact? resolution=null;var executedSteps=new List<TestBuildStep>();
        TestBuildEvidence? buildEvidence=null;TrxAdmissionResult? trxResult=null;
        TestGateProducerResult Result(string code)
        {
            var proof=new List<StoredArtifact>(sourceProofs);
            if(resolution is {} resolved)proof.Add(resolved);
            proof.AddRange(captureProofs);return new(code=="OK",code,buildEvidence,trxResult,proof);
        }
        async Task<TestGateSourceSnapshot> SourceAsync(TestGateSnapshotPoint point)
        {
            var snapshot=await facts.CaptureSourceAsync(request,point,cancellationToken);
            sourceProofs.Add(snapshot.Manifest);sourceProofs.Add(snapshot.NativeEvaluationReceipt);return snapshot;
        }
        async Task<ProcessResult> RunAsync(int index)
        {
            var step=plan.Steps[index];
            IReadOnlyList<string> arguments=step.Kind switch
            {
                TestBuildStepKind.ForceHostBuild=>["build",step.Project,"--no-incremental","--no-restore"],
                TestBuildStepKind.ForceTestsBuild=>["build",step.Project,"--no-incremental","--no-restore","-p:BuildProjectReferences=false"],
                TestBuildStepKind.TestWithoutBuild=>TestArguments(step.Project,request),
                _=>throw new InvalidOperationException("Unknown normal test build phase.")
            };
            // A failed/locked overwrite on Windows is a failed process, never permission to
            // label the old loaded host fresh or to advance through a self-reported receipt.
            IReadOnlyDictionary<string,string> environment=new Dictionary<string,string>();
            if(index==2&&facts is ITestGateTestHostOriginProvider origin)
            {
                environment=await origin.PrepareTestHostEnvironmentAsync(request,cancellationToken);
                const string originKey="ANDROIDMCP_TEST_GATE_ORIGIN_REQUEST";
                if(environment.Count!=1||!environment.TryGetValue(originKey,out var path)||string.IsNullOrWhiteSpace(path))
                    throw new TestGateFactsException("ORIGIN_ENVIRONMENT_INVALID","The origin provider must supply only the fixed challenge key.");
                try
                {
                    var expected=Path.Combine(request.ReceiptDirectory,"origin-request.json");
                    if(Path.GetFullPath(path)!=Path.GetFullPath(expected))throw new ArgumentException("Challenge belongs to another run.");
                    ArtifactPaths.Resolve(request.WorkspaceRoot,Relative(request.WorkspaceRoot,path));
                }
                catch(ArgumentException){throw new TestGateFactsException("ORIGIN_ENVIRONMENT_INVALID","Challenge path is outside this admitted run.");}
            }
            var result=await runner.RunAsync(new(request.DotNet,arguments,request.WorkspaceRoot,
                environment,Path.Combine(request.ReceiptDirectory,"process-"+index)),cancellationToken);
            executedSteps.Add(step);
            var output=await captures.ReadAsync(result,false,cancellationToken);
            var error=await captures.ReadAsync(result,true,cancellationToken);
            captureProofs.Add(new(Relative(request.WorkspaceRoot,result.StandardOutputPath),ContentHash.Sha256(output.Span),output.Length,"application/octet-stream"));
            captureProofs.Add(new(Relative(request.WorkspaceRoot,result.StandardErrorPath),ContentHash.Sha256(error.Span),error.Length,"application/octet-stream"));
            return result;
        }
        var before=await SourceAsync(TestGateSnapshotPoint.BeforeBuild);
        var loadedModule=facts.ReadRunningHostModuleId();
        var hostProcess=await RunAsync(0);
        if(hostProcess.ExitCode!=0||hostProcess.Cancelled)return Result("PROCESS_FAILED");
        var host=await facts.ReadBuiltHostAsync(request,cancellationToken);
        var afterHost=await SourceAsync(TestGateSnapshotPoint.AfterHostBuild);
        if(host.ModuleId!=loadedModule||loadedModule==Guid.Empty)return Result("STALE_HOST");
        if(before.ContentFingerprint!=afterHost.ContentFingerprint)return Result("SOURCE_CHANGED");
        var testsBuild=await RunAsync(1);
        if(testsBuild.ExitCode!=0||testsBuild.Cancelled)return Result("PROCESS_FAILED");
        var tests=await facts.ReadBuiltTestsAsync(request,cancellationToken);resolution=tests.NativeResolutionReceipt;
        var afterTests=await SourceAsync(TestGateSnapshotPoint.AfterTestsBuild);
        if(before.ContentFingerprint!=afterTests.ContentFingerprint)return Result("SOURCE_CHANGED");
        if(tests.HostReference.Sha256!=host.Sha256||tests.HostReference.ModuleId!=host.ModuleId)return Result("STALE_TEST_REFERENCE");
        var testProcess=await RunAsync(2);
        if(testProcess.ExitCode!=0||testProcess.Cancelled)return Result("PROCESS_FAILED");
        var executed=await facts.ReadExecutedTestsAsync(request,testProcess,cancellationToken);
        var final=await SourceAsync(TestGateSnapshotPoint.AfterExecution);
        buildEvidence=new(executedSteps,before.ContentFingerprint,afterHost.ContentFingerprint,final.ContentFingerprint,
            host.Sha256,host.ModuleId,loadedModule,tests.HostReference.Sha256,tests.Tests.Sha256,executed.Sha256);
        var admission=buildAdmission.Evaluate(plan,buildEvidence);
        if(!admission.Admitted)return Result(admission.Code);
        if(tests.Tests.ModuleId!=executed.ModuleId||Path.GetFullPath(tests.Tests.AbsolutePath)!=Path.GetFullPath(executed.AbsolutePath))return Result("STALE_TEST_BINARY");
        ReadOnlyMemory<byte>? trx;
        try{trx=await workspaceArtifacts.ReadAsync(trxRelative,cancellationToken);}
        catch(FileNotFoundException){trx=null;}
        catch(DirectoryNotFoundException){trx=null;}
        trxResult=trxAdmission.Evaluate(new(trx,request.ExpectedClasses));
        return Result(trxResult.Code);
    }
    private static IReadOnlyList<string> TestArguments(string project,TestGateProducerRequest request)
    {
        var args=new List<string>{"test",project,"--no-build","--no-restore"};
        if(!string.IsNullOrWhiteSpace(request.Filter))args.AddRange(["--filter",request.Filter]);
        args.AddRange(["--results-directory",request.ReceiptDirectory,"--logger","trx;LogFileName=tests.trx"]);return args;
    }
    private static string Relative(string root,string path)
    {
        var relative=Path.GetRelativePath(Path.GetFullPath(root),Path.GetFullPath(path)).Replace('\\','/');
        ArtifactPaths.Segments(relative);return relative;
    }
}
