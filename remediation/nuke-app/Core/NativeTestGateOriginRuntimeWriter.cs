using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AndroidMcp.BuildTool.Core;

public sealed class SharedStoreTestGateOriginArtifactWriter(ICreateNewArtifactStore store):ITestGateOriginArtifactWriter
{
    // No second filesystem/hash engine, pre-read or idempotent success fallback.
    public Task<StoredArtifact> WriteNewAsync(Artifact artifact,CancellationToken cancellationToken=default)
        =>store.WriteNewAsync(artifact,cancellationToken);
}
public sealed class RuntimeTestGateLoadedAssemblyPairSource(Assembly tests,Assembly host):ITestGateLoadedAssemblyPairSource
{
    public TestGateLoadedAssemblyPair ObserveLoadedAssemblies()
        =>new(new(tests.Location,tests.ManifestModule.ModuleVersionId),new(host.Location,host.ManifestModule.ModuleVersionId));
}
public sealed class NativeTestGateOriginRuntimeWriter(IArtifactStore artifacts,ITestGateNativeFileReader files,
    ITestGateOriginArtifactWriter newArtifacts,ITestGateLoadedAssemblyPairSource loaded):ITestGateOriginRuntimeWriter
{
    private static readonly JsonSerializerOptions JsonOptions=new(){UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow};
    public async Task<TestGateOriginRuntimeResult> WriteIfRequestedAsync(TestGateOriginRuntimeRequest request,CancellationToken cancellationToken=default)
    {
        if(request.ChallengePath is null||request.ChallengePath.Length==0)return new("NO_ORIGIN_REQUEST",null);
        string relative;string directory;string responsePath;
        try
        {
            relative=Path.GetRelativePath(Path.GetFullPath(request.CompiledWorkspaceRoot),Path.GetFullPath(request.ChallengePath)).Replace('\\','/');
            var segments=ArtifactPaths.Segments(relative);
            if(segments.Length!=4||segments[0]!="artifacts"||segments[1]!="tests"||segments[3]!="origin-request.json")return new("ORIGIN_PATH_SCOPE",null);
            ArtifactPaths.Resolve(request.CompiledWorkspaceRoot,relative);
            directory=Path.GetDirectoryName(Path.GetFullPath(request.ChallengePath))!;
            responsePath=string.Join('/',segments.Take(3))+"/origin-response.json";
            ArtifactPaths.Resolve(request.CompiledWorkspaceRoot,responsePath);
        }
        catch(ArgumentException){return new("ORIGIN_PATH_SCOPE",null);}
        var raw=await artifacts.ReadAsync(relative,cancellationToken);
        TestGateOriginChallenge? challenge;
        try{challenge=JsonSerializer.Deserialize<TestGateOriginChallenge>(raw.Span,JsonOptions);}
        catch(JsonException){return new("ORIGIN_CHALLENGE_INVALID",null);}
        try
        {
            if(challenge is null||challenge.Tests is null||challenge.Host is null||
                Path.GetFullPath(challenge.RunPath)!=directory||challenge.ReceiptPath!=responsePath||
                !Hash(challenge.Nonce)||!Identity(challenge.Tests)||!Identity(challenge.Host))return new("ORIGIN_CHALLENGE_INVALID",null);
            Admit(request.CompiledWorkspaceRoot,challenge.Tests.AbsolutePath);Admit(request.CompiledWorkspaceRoot,challenge.Host.AbsolutePath);
        }
        catch(ArgumentException){return new("ORIGIN_CHALLENGE_INVALID",null);}
        var runtime=loaded.ObserveLoadedAssemblies();
        try{Admit(request.CompiledWorkspaceRoot,runtime.Tests.AbsolutePath);Admit(request.CompiledWorkspaceRoot,runtime.Host.AbsolutePath);}
        catch(ArgumentException){return new("ORIGIN_OUTPUT_CHANGED",null);}
        var testsRaw=await files.ReadAsync(new(runtime.Tests.AbsolutePath,"RuntimeTests",[request.CompiledWorkspaceRoot]),cancellationToken);
        var hostRaw=await files.ReadAsync(new(runtime.Host.AbsolutePath,"RuntimeHost",[request.CompiledWorkspaceRoot]),cancellationToken);
        var tests=TestGateManagedPeIdentity.Observe(runtime.Tests.AbsolutePath,testsRaw);
        var host=TestGateManagedPeIdentity.Observe(runtime.Host.AbsolutePath,hostRaw);
        if(!Matches(runtime.Tests,tests,challenge.Tests)||!Matches(runtime.Host,host,challenge.Host))return new("ORIGIN_OUTPUT_CHANGED",null);
        var response=new TestGateOriginResponse(challenge.RunPath,challenge.Nonce,ContentHash.Sha256(raw.Span),
            runtime.Tests.AbsolutePath,runtime.Tests.LoadedModuleId,tests.Sha256,runtime.Host.AbsolutePath,runtime.Host.LoadedModuleId,host.Sha256);
        var bytes=new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(response));
        try{return new("RECORDED",await newArtifacts.WriteNewAsync(new(responsePath,bytes,"application/json"),cancellationToken));}
        catch(TestGateFactsException error)when(error.Code=="ORIGIN_ALREADY_EXISTS"){return new(error.Code,null);}
    }
    private static bool Matches(TestGateLoadedAssembly runtime,TestGateBinaryIdentity disk,TestGateBinaryIdentity expected)
        =>Path.GetFullPath(runtime.AbsolutePath)==Path.GetFullPath(expected.AbsolutePath)&&runtime.LoadedModuleId==disk.ModuleId&&
            disk.ModuleId==expected.ModuleId&&disk.Sha256==expected.Sha256;
    private static bool Identity(TestGateBinaryIdentity identity)=>Hash(identity.Sha256)&&identity.ModuleId!=Guid.Empty;
    private static bool Hash(string? value)=>value is {Length:64}&&value.All(Uri.IsHexDigit);
    private static void Admit(string root,string path)
    {
        var relative=Path.GetRelativePath(Path.GetFullPath(root),Path.GetFullPath(path)).Replace('\\','/');
        ArtifactPaths.Resolve(root,relative);
    }
}
