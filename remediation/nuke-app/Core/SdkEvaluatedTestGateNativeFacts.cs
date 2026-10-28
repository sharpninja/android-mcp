using System.Text.Json;
using System.Text.Json.Serialization;

namespace AndroidMcp.BuildTool.Core;

// Source/PE portion of the one collector. SDK execution and native strict-store
// runtime proof remain separate from injected collector unit evidence.
public sealed partial class SdkEvaluatedTestGateNativeFacts
{
    private readonly TestGateNativeFactsConfiguration configuration;
    private readonly ITestGateSdkEvaluator evaluator;
    private readonly ITestGateNativeFileReader files;
    private readonly ITestGateLoadedModuleSource loaded;
    private readonly IArtifactStore store;
    private readonly CanonicalJson canonical=new();
    private TestGateTestsOutput? builtTests;
    private TestGateProducerRequest? builtRun;
    public SdkEvaluatedTestGateNativeFacts(TestGateNativeFactsConfiguration configuration,ITestGateSdkEvaluator evaluator,
        ITestGateNativeFileReader files,ITestGateLoadedModuleSource loaded,IArtifactStore store,
        ITestGateNonceSource nonce,ITestGateOriginArtifactWriter newArtifacts)
    {
        this.configuration=configuration;this.evaluator=evaluator;this.files=files;this.loaded=loaded;this.store=store;
        this.nonce=nonce;this.newArtifacts=newArtifacts;
    }

    public Guid ReadRunningHostModuleId()=>loaded.ReadHostModuleId();
    public async Task<TestGateSourceSnapshot> CaptureSourceAsync(TestGateProducerRequest request,TestGateSnapshotPoint point,
        CancellationToken cancellationToken=default)
    {
        foreach(var pin in configuration.Sdk.AssemblyFiles)
        {
            var path=ArtifactPaths.Resolve(configuration.Sdk.SdkDirectory,pin.RelativePath);
            var bytes=await ReadAsync(path,"SdkAssembly",cancellationToken);
            if(bytes.Length!=pin.Length||ContentHash.Sha256(bytes.Span)!=pin.Sha256)throw Error("SDK_PIN_MISMATCH");
        }
        var evaluation=await EvaluateAsync(request,cancellationToken);
        var inputs=evaluation.Projects.SelectMany(project=>project.Inputs.Concat(project.Imports.Select(path=>new TestGateEvaluatedInput("Import",path,project.ProjectPath))))
            .OrderBy(input=>input.Kind,StringComparer.Ordinal).ThenBy(input=>input.AbsolutePath,StringComparer.Ordinal).ThenBy(input=>input.DefiningProject,StringComparer.Ordinal).ToArray();
        foreach(var input in inputs){Admit(input.AbsolutePath);Admit(input.DefiningProject);}
        var paths=inputs.Select(input=>input.AbsolutePath).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var original=new Dictionary<string,byte[]>(StringComparer.Ordinal);var rechecked=new Dictionary<string,byte[]>(StringComparer.Ordinal);
        foreach(var path in paths)original.Add(path,(await ReadAsync(path,"SourceInput",cancellationToken)).ToArray());
        foreach(var path in paths)rechecked.Add(path,(await ReadAsync(path,"SourceInputRecheck",cancellationToken)).ToArray());
        if(paths.Any(path=>!original[path].AsSpan().SequenceEqual(rechecked[path])))throw Error("INPUT_CHANGED");
        var manifest=new TestGateSourceManifest(request.HostProject,request.TestsProject,configuration.Configuration,configuration.TargetFramework,
            inputs.Select(input=>new TestGateHashedInput(input.Kind,input.AbsolutePath,input.DefiningProject,
                ContentHash.Sha256(original[input.AbsolutePath]),original[input.AbsolutePath].LongLength)).ToArray());
        var manifestBytes=Bytes(manifest);var relative=RunRelative(request);
        var native=await store.WriteAsync(new(relative+"/sdk-"+point+".json",Bytes(evaluation),"application/json"),cancellationToken);
        var saved=await store.WriteAsync(new(relative+"/source-"+point+".json",manifestBytes,"application/json"),cancellationToken);
        return new(saved,ContentHash.Sha256(manifestBytes),native);
    }
    public async Task<TestGateBinaryIdentity> ReadBuiltHostAsync(TestGateProducerRequest request,CancellationToken cancellationToken=default)
    {
        var evaluation=await EvaluateAsync(request,cancellationToken);
        return await BinaryAsync(Project(evaluation,request.HostProject).TargetPath,"BuiltHost",cancellationToken);
    }
    public async Task<TestGateTestsOutput> ReadBuiltTestsAsync(TestGateProducerRequest request,CancellationToken cancellationToken=default)
    {
        var evaluation=await EvaluateAsync(request,cancellationToken,TestGateSdkEvaluationPhase.ResolvedBuildOutputs);
        var project=Project(evaluation,request.TestsProject);var hostProject=Project(evaluation,request.HostProject);
        if(!project.ProjectReferences.Contains(request.HostProject,StringComparer.Ordinal))throw Error("SOURCE_INPUTS_UNRESOLVED");
        var matches=(project.ResolvedCopies??[]).Where(copy=>copy.ReferencedProject==request.HostProject).ToArray();
        if(matches.Length==0)throw Error("RESOLVED_REFERENCE_MISSING");
        if(matches.Length!=1)throw Error("RESOLVED_REFERENCE_CONFLICT");
        var copy=matches[0];
        if(copy.SourcePath!=hostProject.TargetPath)throw Error("RESOLVED_REFERENCE_MISMATCH");
        Admit(copy.SourcePath);Admit(copy.DestinationPath);
        await VerifyCopyProofAsync(request,copy,cancellationToken);
        var tests=await BinaryAsync(project.TargetPath,"BuiltTests",cancellationToken);
        var host=await BinaryAsync(copy.DestinationPath,"HostReference",cancellationToken);
        var resolution=await store.WriteAsync(new(RunRelative(request)+"/test-resolution.json",
            Bytes(new{Evaluation=evaluation,Tests=tests,Host=host}),"application/json"),cancellationToken);
        builtTests=new(tests,host,resolution);builtRun=request;return builtTests;
    }
    private async Task VerifyCopyProofAsync(TestGateProducerRequest request,TestGateResolvedCopy copy,CancellationToken cancellationToken)
    {
        var proof=copy.NativeProof;
        if(proof is null||!ArtifactPaths.IsRelative(proof.RelativePath)||
            !proof.RelativePath.StartsWith(RunRelative(request)+"/",StringComparison.Ordinal))throw Error("RESOLVED_REFERENCE_PROOF_INVALID");
        ReadOnlyMemory<byte> bytes;
        try{ArtifactPaths.Resolve(request.WorkspaceRoot,proof.RelativePath);bytes=await store.ReadAsync(proof.RelativePath,cancellationToken);}
        catch(Exception error)when(error is FileNotFoundException or DirectoryNotFoundException or ArgumentException){throw Error("RESOLVED_REFERENCE_PROOF_INVALID");}
        if(bytes.Length!=proof.Length||ContentHash.Sha256(bytes.Span)!=proof.Sha256)throw Error("RESOLVED_REFERENCE_PROOF_INVALID");
        TestGateResolvedCopyProof? observed;
        try{observed=JsonSerializer.Deserialize<TestGateResolvedCopyProof>(bytes.Span,new JsonSerializerOptions{UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow});}
        catch(JsonException){throw Error("RESOLVED_REFERENCE_PROOF_INVALID");}
        if(observed is null||observed.RunDirectory!=request.ReceiptDirectory||observed.ProjectPath!=request.TestsProject||
            observed.SourcePath!=copy.SourcePath||observed.DestinationPath!=copy.DestinationPath||observed.ReferencedProject!=copy.ReferencedProject||
            observed.DotNet!=request.DotNet||observed.Configuration!=configuration.Configuration||observed.TargetFramework!=configuration.TargetFramework)
            throw Error("RESOLVED_REFERENCE_PROOF_INVALID");
    }
    private async Task<TestGateSdkEvaluation> EvaluateAsync(TestGateProducerRequest request,CancellationToken cancellationToken,
        TestGateSdkEvaluationPhase phase=TestGateSdkEvaluationPhase.SourceInputs)
    {
        var result=await evaluator.EvaluateAsync(new(request,configuration.Sdk,configuration.Configuration,configuration.TargetFramework,phase),cancellationToken);
        if(!Bytes(result.SdkFiles).AsSpan().SequenceEqual(Bytes(configuration.Sdk.AssemblyFiles)))throw Error("SDK_PIN_MISMATCH");
        var projects=result.Projects.Select(project=>project.ProjectPath).ToHashSet(StringComparer.Ordinal);
        if(result.Projects.Count!=projects.Count||!projects.Contains(request.HostProject)||!projects.Contains(request.TestsProject)||
            result.Projects.Any(project=>project.Issues.Count!=0||project.ProjectReferences.Any(reference=>!projects.Contains(reference))))
            throw Error("SOURCE_INPUTS_UNRESOLVED");
        return result;
    }
    private static TestGateEvaluatedProject Project(TestGateSdkEvaluation evaluation,string project)
        =>evaluation.Projects.Single(item=>item.ProjectPath==project);
    private async Task<ReadOnlyMemory<byte>> ReadAsync(string path,string role,CancellationToken cancellationToken)
    {Admit(path);return await files.ReadAsync(new(path,role,configuration.ApprovedReadRoots),cancellationToken);}
    private void Admit(string path)
    {
        foreach(var root in configuration.ApprovedReadRoots)
        {
            var relative=Path.GetRelativePath(Path.GetFullPath(root),Path.GetFullPath(path)).Replace('\\','/');
            if(!ArtifactPaths.IsRelative(relative))continue;
            try{ArtifactPaths.Resolve(root,relative);return;}catch(ArgumentException){throw Error("INPUT_SCOPE_VIOLATION");}
        }
        throw Error("INPUT_SCOPE_VIOLATION");
    }
    private async Task<TestGateBinaryIdentity> BinaryAsync(string path,string role,CancellationToken cancellationToken)
    {
        var bytes=await ReadAsync(path,role,cancellationToken);
        return TestGateManagedPeIdentity.Observe(path,bytes);
    }
    private byte[] Bytes<T>(T value)=>canonical.Serialize(JsonSerializer.SerializeToElement(value));
    private static string RunRelative(TestGateProducerRequest request)
    {
        var relative=Path.GetRelativePath(Path.GetFullPath(request.WorkspaceRoot),Path.GetFullPath(request.ReceiptDirectory)).Replace('\\','/');
        var segments=ArtifactPaths.Segments(relative);
        if(segments.Length!=3||segments[0]!="artifacts"||segments[1]!="tests")throw Error("INPUT_SCOPE_VIOLATION");
        ArtifactPaths.Resolve(request.WorkspaceRoot,relative);return relative;
    }
    private static TestGateFactsException Error(string code)=>new(code,"Native normal-test evidence is unavailable or inconsistent: "+code);
}
