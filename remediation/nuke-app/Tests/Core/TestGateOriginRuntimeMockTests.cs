using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review.Staging;

public sealed record OriginRuntimeObservation(TestGateOriginRuntimeRequest Request,TestGateOriginRuntimeResult Result,
    IReadOnlyList<string> ArtifactReads,IReadOnlyList<NativeFactsRead> FileReads,IReadOnlyList<NativeFactsWrite> Writes,
    IReadOnlyList<TestGateLoadedAssemblyPair> LoadedObservations,IReadOnlyList<string> Order);

public sealed class OriginRuntimeFixture
{
    public static readonly string[] Names=["no-origin-env","valid-runtime-origin","challenge-outside-root","challenge-wrong-layout",
        "challenge-wrong-filename","compiled-root-prefix-conflict","malformed-challenge","challenge-wrong-run",
        "challenge-reply-path-escape","loaded-tests-mvid-changed","loaded-host-mvid-changed","test-bytes-changed","reply-already-exists"];
    public string Name { get; }
    public NativeFactsFixture Native { get; }=new("origin-complete");
    public TestGateOriginRuntimeRequest Request { get; }
    public TestGateOriginChallenge Challenge { get; }
    public byte[] ChallengeBytes { get; }
    public TestGateLoadedAssemblyPair Loaded { get; }
    public byte[] TestBytes { get; }
    public byte[] HostBytes=>Native.Files[Native.Tests.HostReference.AbsolutePath];
    public TestGateOriginResponse Reply { get; }
    public byte[] ReplyBytes=>NativeFactsFixture.Canonical(Reply);
    public string Code { get; }
    public bool ReadsChallenge=>Name is not ("no-origin-env" or "challenge-outside-root" or "challenge-wrong-layout" or "challenge-wrong-filename" or "compiled-root-prefix-conflict");
    public bool ReadsAssemblies=>Name is "valid-runtime-origin" or "loaded-tests-mvid-changed" or "loaded-host-mvid-changed" or "test-bytes-changed" or "reply-already-exists";
    public bool AttemptsWrite=>Name is "valid-runtime-origin" or "reply-already-exists";
    public OriginRuntimeFixture(string name)
    {
        Name=name;var path=Native.Request.WorkspaceRoot+"/"+Native.ChallengePath;
        Request=new(name=="compiled-root-prefix-conflict" ? "/fixture/application" : Native.Request.WorkspaceRoot,name switch
        {"no-origin-env"=>null,"challenge-outside-root"=>"/private/challenge.json","challenge-wrong-layout"=>"/fixture/app/other/current-run/origin-request.json",
            "challenge-wrong-filename"=>"/fixture/app/artifacts/tests/current-run/other.json",_=>path});
        Challenge=Native.Challenge;
        if(name=="challenge-wrong-run")Challenge=Challenge with {RunPath="/fixture/app/artifacts/tests/other-run"};
        if(name=="challenge-reply-path-escape")Challenge=Challenge with {ReceiptPath="../private/reply.json"};
        ChallengeBytes=name=="malformed-challenge" ? Encoding.UTF8.GetBytes("not-json") : NativeFactsFixture.Canonical(Challenge);
        Loaded=new(new(Native.Tests.Tests.AbsolutePath,name=="loaded-tests-mvid-changed" ? NativeFactsFixture.Module(3) : Native.Tests.Tests.ModuleId),
            new(Native.Tests.HostReference.AbsolutePath,name=="loaded-host-mvid-changed" ? NativeFactsFixture.Module(3) : Native.Tests.HostReference.ModuleId));
        TestBytes=name=="test-bytes-changed" ? NativeFactsFixture.Pe(NativeFactsFixture.Module(3)) : Native.Files[Native.Tests.Tests.AbsolutePath];
        Reply=new(Challenge.RunPath,Challenge.Nonce,ContentHash.Sha256(ChallengeBytes),Loaded.Tests.AbsolutePath,
            Loaded.Tests.LoadedModuleId,ContentHash.Sha256(TestBytes),Loaded.Host.AbsolutePath,Loaded.Host.LoadedModuleId,ContentHash.Sha256(HostBytes));
        Code=name switch
        {
            "no-origin-env"=>"NO_ORIGIN_REQUEST","valid-runtime-origin"=>"RECORDED",
            "challenge-outside-root" or "challenge-wrong-layout" or "challenge-wrong-filename" or "compiled-root-prefix-conflict"=>"ORIGIN_PATH_SCOPE",
            "malformed-challenge" or "challenge-wrong-run" or "challenge-reply-path-escape"=>"ORIGIN_CHALLENGE_INVALID",
            "loaded-tests-mvid-changed" or "loaded-host-mvid-changed" or "test-bytes-changed"=>"ORIGIN_OUTPUT_CHANGED",_=>"ORIGIN_ALREADY_EXISTS"
        };
    }
    public TestGateNativeFileRequest FileRequest(string path,string role)=>new(path,role,[Request.CompiledWorkspaceRoot]);
    public TestGateOriginRuntimeResult Result=>new(Code,Code=="RECORDED" ? NativeFactsFixture.Stored(Native.ResponsePath,ReplyBytes) : null);
}

public sealed class OriginRuntimeDependencies(OriginRuntimeFixture fixture):IArtifactStore,ITestGateNativeFileReader,
    ITestGateOriginArtifactWriter,ITestGateLoadedAssemblyPairSource
{
    public List<string> Reads { get; }=[];public List<NativeFactsRead> Files { get; }=[];public List<NativeFactsWrite> Writes { get; }=[];
    public List<TestGateLoadedAssemblyPair> Loaded { get; }=[];public List<string> Order { get; }=[];
    public Task<ReadOnlyMemory<byte>> ReadAsync(string relativePath,CancellationToken cancellationToken=default)
    {Order.Add("read:"+relativePath);Reads.Add(relativePath);return Task.FromResult<ReadOnlyMemory<byte>>(fixture.ChallengeBytes);}
    public Task<ReadOnlyMemory<byte>> ReadAsync(TestGateNativeFileRequest request,CancellationToken cancellationToken=default)
    {
        Order.Add("file:"+request.Role+":"+request.AbsolutePath);var bytes=request.Role=="RuntimeTests" ? fixture.TestBytes : fixture.HostBytes;
        Files.Add(new(request,Convert.ToBase64String(bytes)));return Task.FromResult<ReadOnlyMemory<byte>>(bytes);
    }
    public TestGateLoadedAssemblyPair ObserveLoadedAssemblies(){Order.Add("loaded-assemblies");Loaded.Add(fixture.Loaded);return fixture.Loaded;}
    public Task<StoredArtifact> WriteNewAsync(Artifact artifact,CancellationToken cancellationToken=default)
    {
        Order.Add("write-new:"+artifact.RelativePath);var bytes=artifact.Bytes.ToArray();Writes.Add(new(artifact.RelativePath,Convert.ToBase64String(bytes),artifact.MediaType,true));
        if(fixture.Name=="reply-already-exists")throw new TestGateFactsException("ORIGIN_ALREADY_EXISTS","Identical existing reply is still a CreateNew collision.");
        return Task.FromResult(NativeFactsFixture.Stored(artifact.RelativePath,bytes));
    }
    public Task<StoredArtifact> WriteAsync(Artifact artifact,CancellationToken cancellationToken=default)
    {Order.Add("forbidden-idempotent-write");throw new InvalidOperationException("Runtime origin requires shared strict WriteNew.");}
    public OriginRuntimeObservation Observe()=>new(fixture.Request,fixture.Result,Reads,Files,Writes,Loaded,Order);
}

public sealed class ScriptedOriginRuntimeCases
{
    public async Task<OriginRuntimeObservation> ExecuteAsync(OriginRuntimeFixture fixture)
    {
        var io=new OriginRuntimeDependencies(fixture);
        if(fixture.ReadsChallenge)await io.ReadAsync(fixture.Native.ChallengePath);
        if(fixture.ReadsAssemblies)
        {
            io.ObserveLoadedAssemblies();await io.ReadAsync(fixture.FileRequest(fixture.Loaded.Tests.AbsolutePath,"RuntimeTests"));
            await io.ReadAsync(fixture.FileRequest(fixture.Loaded.Host.AbsolutePath,"RuntimeHost"));
        }
        if(fixture.AttemptsWrite)
            try{await io.WriteNewAsync(new(fixture.Native.ResponsePath,fixture.ReplyBytes,"application/json"));}
            catch(TestGateFactsException){ /* Scripted expected collision, never a successful fallback. */ }
        return io.Observe();
    }
}
public static class OriginRuntimeCases
{
    public static IEnumerable<object[]> Data=>OriginRuntimeFixture.Names.Select(name=>new object[]{name});
    public static OriginRuntimeObservation Expected(OriginRuntimeFixture fixture)
    {
        var order=new List<string>();var reads=new List<string>();var files=new List<NativeFactsRead>();var writes=new List<NativeFactsWrite>();
        var loaded=new List<TestGateLoadedAssemblyPair>();
        if(fixture.ReadsChallenge){reads.Add(fixture.Native.ChallengePath);order.Add("read:"+fixture.Native.ChallengePath);}
        if(fixture.ReadsAssemblies)
        {
            loaded.Add(fixture.Loaded);order.Add("loaded-assemblies");
            files.Add(new(fixture.FileRequest(fixture.Loaded.Tests.AbsolutePath,"RuntimeTests"),Convert.ToBase64String(fixture.TestBytes)));
            files.Add(new(fixture.FileRequest(fixture.Loaded.Host.AbsolutePath,"RuntimeHost"),Convert.ToBase64String(fixture.HostBytes)));
            order.AddRange(files.Select(read=>"file:"+read.Request.Role+":"+read.Request.AbsolutePath));
        }
        if(fixture.AttemptsWrite){writes.Add(new(fixture.Native.ResponsePath,Convert.ToBase64String(fixture.ReplyBytes),"application/json",true));order.Add("write-new:"+fixture.Native.ResponsePath);}
        return new(fixture.Request,fixture.Result,reads,files,writes,loaded,order);
    }
    public static JsonNode Json(OriginRuntimeObservation value)=>JsonSerializer.SerializeToNode(value)!;
    public static void AssertContract(OriginRuntimeFixture fixture,JsonNode actual)
        =>Assert.Equal(new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(Expected(fixture))),new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(actual)));
    public static IEnumerable<object[]> Controls
    {
        get
        {
            foreach(var name in OriginRuntimeFixture.Names)
            {
                var node=Json(Expected(new(name)));var index=0;
                foreach(var member in ReviewOfficialTransportCases.Leaves(node))yield return new object[]{name,index++,member.Label};
            }
        }
    }
}
[Trait("Gate","Mock")]
public sealed class TestGateOriginRuntimeMockTests
{
    [Theory,MemberData(nameof(OriginRuntimeCases.Data),MemberType=typeof(OriginRuntimeCases))]
    public async Task CorrectTypedRuntimeWriterMock(string name)
    {
        var fixture=new OriginRuntimeFixture(name);var actual=await new ScriptedOriginRuntimeCases().ExecuteAsync(fixture);
        OriginRuntimeCases.AssertContract(fixture,OriginRuntimeCases.Json(actual));
    }
    [Theory,MemberData(nameof(OriginRuntimeCases.Controls),MemberType=typeof(OriginRuntimeCases))]
    public async Task EachRootAdmissionNonceLoadedMvidProofHashWriteModeAndEffectControlRejects(string name,int memberIndex,string label)
    {
        var fixture=new OriginRuntimeFixture(name);var actual=OriginRuntimeCases.Json(await new ScriptedOriginRuntimeCases().ExecuteAsync(fixture));
        OriginRuntimeCases.AssertContract(fixture,actual);var members=ReviewOfficialTransportCases.Leaves(actual).ToArray();Assert.Equal(label,members[memberIndex].Label);
        ReviewOfficialTransportCases.Mutate(actual,members[memberIndex].Path);
        Assert.NotNull(Record.Exception(()=>OriginRuntimeCases.AssertContract(fixture,actual)));
    }
}
