using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Real")]
[Trait("Requirement", "TEST-BUILD-004")]
public sealed class SourceProducerIdentityRealTests
{
    [Theory, MemberData(nameof(SourceProducerIdentityContractCases.Cases), MemberType = typeof(SourceProducerIdentityContractCases))]
    public void NativeBuilderUsesSameAssemblyRuntimeJavaPinAndLifetimeAssertions(string id) =>
        SourceProducerIdentityContractCases.AssertContract(id, (fixture, rig) =>
            new SourceProducerIdentityBuilder(id == "managed" ? new CountingLoadedAssembly(rig) : rig, rig));
    private sealed class CountingLoadedAssembly(SourceProducerIdentityRig rig) : ISourceProducerAssemblySource
    {
        public SourceProducerAssemblyObservation Capture()
        { rig.Captures++; return new LoadedSourceProducerAssemblySource().Capture(); }
    }
}
