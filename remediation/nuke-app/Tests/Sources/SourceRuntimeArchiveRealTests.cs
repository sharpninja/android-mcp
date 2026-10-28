using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Real")]
[Trait("Requirement", "TEST-BUILD-004")]
public sealed class SourceRuntimeArchiveRealTests
{
    [Theory, MemberData(nameof(SourceRuntimeArchiveContractCases.Cases), MemberType = typeof(SourceRuntimeArchiveContractCases))]
    public void SameBinaryConcernRegistrationCacheFlagsAndOwnershipAssertionsUseNativeFactory(string profile, bool java, bool partial) =>
        SourceRuntimeArchiveContractCases.AssertContract(profile, java, partial, (loader, archive) => new SourcesRuntimeFactory(loader, annotationArchive: archive));
}
