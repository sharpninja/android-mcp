using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Real")]
[Trait("Requirement", "TEST-BUILD-004")]
public sealed class SourcesRuntimeRealTests
{
    private static SourceRuntimeRig Rig()
    {
        var java = new RecordingSourceJavaLoader(); var permission = new RecordingPermissionReference();
        return new(new SourcesRuntimeFactory(java, permission), java, permission);
    }
    [Theory, MemberData(nameof(SourcesRuntimeContractCases.Cases), MemberType = typeof(SourcesRuntimeContractCases))]
    public void SameCompositionCasesUseNativeFactory(string profile, bool java) => SourcesRuntimeContractCases.AssertComposition(profile, java, Rig());
    [Theory, InlineData(false), InlineData(true)]
    public void SameLifecycleCasesUseNativeFactory(bool java) => SourcesRuntimeContractCases.AssertLifecycle(java, Rig());
    [Fact] public void SameNullProfileCaseUsesNativeFactory() => SourcesRuntimeContractCases.AssertNullProfile(Rig());
    [Theory, InlineData("format"), InlineData("io"), InlineData("dll"), InlineData("cancel")]
    public void SameAdmissionFailuresUseNativeFactory(string kind)
    {
        Exception failure = kind switch
        { "format" => new FormatException("manifest invalid"), "io" => new IOException("asset missing"), "dll" => new DllNotFoundException("exports absent"), _ => new OperationCanceledException("cancelled") };
        SourcesRuntimeContractCases.AssertAdmissionFailure(Rig(), failure);
    }
    [Theory, InlineData("aidl"), InlineData("unexpected"), InlineData("")]
    public void SameRejectedJavaKindCasesUseNativeFactory(string kind) => SourcesRuntimeContractCases.AssertWrongJavaKind(Rig(), kind);
    [Fact] public void SameMissingLoaderCaseUsesDefaultNativeFactory() => SourcesRuntimePreflightContractCases.AssertMissingJavaLoader(new SourcesRuntimeFactory());
}
