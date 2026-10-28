using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review.Staging;

public sealed class TestGateSdkChildNonceNativeTests
{
    [Fact]
    public Task Actual_shared_64_character_nonce_survives_the_existing_native_evaluator_interface()
        =>SdkChildNonceCases.VerifyActualSharedSourceAsync(nonce=>
        {
            var fixture=new SdkChildFixture("native-source-graph");var io=new SdkChildNonceIo(nonce);
            var configuration=new TestGateNativeFactsConfiguration(fixture.Native.Evaluation.Sdk,
                fixture.Native.ApprovedReadRoots,fixture.Native.Evaluation.Configuration,fixture.Native.Evaluation.TargetFramework);
            return new NativeTestGateSdkChildEvaluator(configuration,fixture.Request.Monolith,
                fixture.Files["bin/App.dll"].LongLength,fixture.Request.RuntimeFiles,io,io,io,io,nonce);
        });
    [Fact]
    public void Same_truncated_process_nonce_control_remains_rejected()=>SdkChildNonceCases.RejectTruncatedTransport();
}
