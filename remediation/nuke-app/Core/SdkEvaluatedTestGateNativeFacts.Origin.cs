using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AndroidMcp.BuildTool.Core;

public sealed class CryptographicTestGateNonceSource:ITestGateNonceSource
{public string CreateNonce()=>Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));}
public sealed class RuntimeHostTestGateModuleSource(Assembly host):ITestGateLoadedModuleSource
{public Guid ReadHostModuleId()=>host.ManifestModule.ModuleVersionId;}
public sealed class ScopedTestGateNativeFileReader(IReadOnlyList<string> approvedRoots):ITestGateNativeFileReader
{
    public async Task<ReadOnlyMemory<byte>> ReadAsync(TestGateNativeFileRequest request,CancellationToken cancellationToken=default)
    {
        if(!request.ApprovedReadRoots.SequenceEqual(approvedRoots,StringComparer.Ordinal))throw new TestGateFactsException("INPUT_SCOPE_VIOLATION","Read scopes differ from the configured native authority.");
        foreach(var root in approvedRoots)
        {
            var relative=Path.GetRelativePath(Path.GetFullPath(root),Path.GetFullPath(request.AbsolutePath)).Replace('\\','/');
            if(!ArtifactPaths.IsRelative(relative))continue;
            // Reuse the one file-store path/byte authority, including ancestor and leaf link rejection.
            return await new FileArtifactStore(root,bytes=>ContentHash.Sha256(bytes.Span)).ReadAsync(relative,cancellationToken);
        }
        throw new TestGateFactsException("INPUT_SCOPE_VIOLATION","Requested native input is outside admitted roots.");
    }
}
public sealed partial class SdkEvaluatedTestGateNativeFacts:ITestGateNativeFacts,ITestGateTestHostOriginProvider
{
    private readonly ITestGateNonceSource nonce;
    private readonly ITestGateOriginArtifactWriter newArtifacts;
    private bool prepared;
    private TestGateOriginChallenge? challenge;
    private byte[]? challengeBytes;
    private static readonly JsonSerializerOptions OriginJson=new(){UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow};
    public async Task<IReadOnlyDictionary<string,string>> PrepareTestHostEnvironmentAsync(TestGateProducerRequest request,CancellationToken cancellationToken=default)
    {
        if(prepared)throw Error("ORIGIN_ALREADY_PREPARED");
        if(builtTests is null||builtRun is null||!SameRun(builtRun,request))throw Error("ORIGIN_NOT_PREPARED");
        var relative=RunRelative(request);var value=nonce.CreateNonce();
        if(!Hex(value))throw Error("ORIGIN_NONCE_INVALID");
        challenge=new(request.ReceiptDirectory,value,relative+"/origin-response.json",builtTests.Tests,builtTests.HostReference);
        challengeBytes=Bytes(challenge);prepared=true; // An uncertain or failed write never authorizes another mint/retry.
        var path=relative+"/origin-request.json";
        await newArtifacts.WriteNewAsync(new(path,challengeBytes,"application/json"),cancellationToken);
        return new Dictionary<string,string>{{"ANDROIDMCP_TEST_GATE_ORIGIN_REQUEST",ArtifactPaths.Resolve(request.WorkspaceRoot,path)}};
    }
    public async Task<TestGateBinaryIdentity> ReadExecutedTestsAsync(TestGateProducerRequest request,ProcessResult result,CancellationToken cancellationToken=default)
    {
        if(!prepared||challenge is null||challengeBytes is null||builtRun is null||!SameRun(builtRun,request))throw Error("ORIGIN_NOT_PREPARED");
        if(result.ExitCode!=0||result.Cancelled)throw Error("ORIGIN_PROCESS_FAILED");
        ReadOnlyMemory<byte> raw;
        try{raw=await store.ReadAsync(challenge.ReceiptPath,cancellationToken);}
        catch(FileNotFoundException){throw Error("ORIGIN_MISSING");}
        catch(DirectoryNotFoundException){throw Error("ORIGIN_MISSING");}
        TestGateOriginResponse? response;
        try{response=JsonSerializer.Deserialize<TestGateOriginResponse>(raw.Span,OriginJson);}
        catch(JsonException){throw Error("ORIGIN_MISMATCH");}
        if(response is null||response.RunPath!=challenge.RunPath||response.Nonce!=challenge.Nonce||
            response.ChallengeSha256!=ContentHash.Sha256(challengeBytes)||
            response.TestsPath!=challenge.Tests.AbsolutePath||response.HostPath!=challenge.Host.AbsolutePath||
            response.LoadedTestsModuleId!=challenge.Tests.ModuleId||response.LoadedHostModuleId!=challenge.Host.ModuleId||
            response.TestsSha256!=challenge.Tests.Sha256||response.HostSha256!=challenge.Host.Sha256)throw Error("ORIGIN_MISMATCH");
        var tests=await BinaryAsync(response.TestsPath,"ExecutedTests",cancellationToken);
        var host=await BinaryAsync(response.HostPath,"ExecutedHost",cancellationToken);
        if(tests.Sha256!=response.TestsSha256||host.Sha256!=response.HostSha256||
            tests.ModuleId!=response.LoadedTestsModuleId||host.ModuleId!=response.LoadedHostModuleId)throw Error("ORIGIN_MISMATCH");
        return tests;
    }
    private static bool SameRun(TestGateProducerRequest one,TestGateProducerRequest two)
        =>one.WorkspaceRoot==two.WorkspaceRoot&&one.ReceiptDirectory==two.ReceiptDirectory&&one.HostProject==two.HostProject&&
            one.TestsProject==two.TestsProject&&one.DotNet==two.DotNet;
    private static bool Hex(string value)=>value.Length==64&&value.All(Uri.IsHexDigit);
}
