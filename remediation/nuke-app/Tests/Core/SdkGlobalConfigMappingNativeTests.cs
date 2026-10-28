using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review.Staging;

public sealed class SdkGlobalConfigMappingNativeTests
{
    [Fact]
    public Task Same_SDK_candidate_and_actual_editor_config_case_binds_to_native_projection()
        =>SdkGlobalConfigCases.AssertMappingAndManifestAsync((fixture,io)=>
            new NativeTestGateSdkEvaluator(fixture.Source.Configuration,io,io));
}
