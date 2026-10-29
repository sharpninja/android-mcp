using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review.Staging;

// One real existing RNG capability, recording only its public challenge nonce.
// All process, store and SDK collaborators remain deterministic fixtures.
public sealed class RecordingSdkChildNonce:ITestGateNonceSource
{
    public string? Value { get; private set; }
    public SdkChildFixture Fixture { get; private set; }=null!;
    public SdkChildDependencies Io { get; private set; }=null!;
    public string CreateNonce()
    {
        Assert.Null(Value);Value=new CryptographicTestGateNonceSource().CreateNonce();
        Fixture=new("native-source-graph",Value);Io=new(Fixture);return Value;
    }
}
public sealed class SdkChildNonceIo(RecordingSdkChildNonce nonce):IPinnedArtifactReader,ICreateNewArtifactStore,IProcessRunner,IProcessCaptureReader
{
    public Task<ReadOnlyMemory<byte>> ReadAsync(StoredArtifact pin,CancellationToken cancellationToken=default)=>nonce.Io.ReadAsync(pin,cancellationToken);
    public Task<ReadOnlyMemory<byte>> ReadAsync(string path,CancellationToken cancellationToken=default)=>nonce.Io.ReadAsync(path,cancellationToken);
    public Task<StoredArtifact> WriteNewAsync(Artifact artifact,CancellationToken cancellationToken=default)=>nonce.Io.WriteNewAsync(artifact,cancellationToken);
    public Task<StoredArtifact> WriteAsync(Artifact artifact,CancellationToken cancellationToken=default)=>nonce.Io.WriteAsync(artifact,cancellationToken);
    public Task<ProcessResult> RunAsync(ProcessRequest request,CancellationToken cancellationToken=default)=>nonce.Io.RunAsync(request,cancellationToken);
    public Task<ReadOnlyMemory<byte>> ReadAsync(ProcessResult result,bool standardError,CancellationToken cancellationToken=default)=>nonce.Io.ReadAsync(result,standardError,cancellationToken);
}
public sealed class ScriptedSdkChildNonceEvaluator(RecordingSdkChildNonce nonce):ITestGateSdkEvaluator
{
    public async Task<TestGateSdkEvaluation> EvaluateAsync(TestGateSdkEvaluationRequest request,CancellationToken cancellationToken=default)
    {
        nonce.CreateNonce();
        Assert.Equal(64,nonce.Value!.Length);Assert.Matches("^[0-9a-f]{64}$",nonce.Value);
        Assert.Equal(new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(request)),
            new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(nonce.Fixture.Native.Evaluation)));
        return (await new ScriptedSdkChildCases().ExecuteAsync(nonce.Fixture,nonce.Io)).Evaluation!;
    }
}
public static class SdkChildNonceCases
{
    public static async Task VerifyActualSharedSourceAsync(Func<RecordingSdkChildNonce,ITestGateSdkEvaluator> create)
    {
        var nonce=new RecordingSdkChildNonce();var evaluator=create(nonce);
        var request=new SdkChildFixture("native-source-graph").Native.Evaluation;
        var result=await evaluator.EvaluateAsync(request);
        Assert.NotNull(nonce.Value);Assert.Matches("^[0-9a-f]{64}$",nonce.Value);
        SdkChildCases.AssertContract(nonce.Fixture,JsonSerializer.SerializeToNode(nonce.Io.Observe("OK",result))!);
        Assert.Contains("sdk-evaluation-"+nonce.Value+"/evaluation.json",nonce.Fixture.Request.EvaluationRelativePath,StringComparison.Ordinal);
    }
    public static void RejectTruncatedTransport()
    {
        var nonce=new CryptographicTestGateNonceSource().CreateNonce();Assert.Equal(64,nonce.Length);
        var fixture=new SdkChildFixture("native-source-graph",nonce);
        var actual=JsonSerializer.SerializeToNode(fixture.Expected)!;
        var path=actual["Processes"]![0]!["ReceiptDirectory"]!.GetValue<string>();
        actual["Processes"]![0]!["ReceiptDirectory"]=path.Replace(nonce,nonce[..32],StringComparison.Ordinal);
        Assert.ThrowsAny<Exception>(()=>SdkChildCases.AssertContract(fixture,actual));
    }
}
public sealed class TestGateSdkChildNonceMockTests
{
    [Fact]public Task Actual_shared_64_character_nonce_survives_the_existing_evaluator_interface()
        =>SdkChildNonceCases.VerifyActualSharedSourceAsync(nonce=>new ScriptedSdkChildNonceEvaluator(nonce));
    [Fact]public void A_truncated_nonce_in_the_process_path_is_rejected()=>SdkChildNonceCases.RejectTruncatedTransport();
}
