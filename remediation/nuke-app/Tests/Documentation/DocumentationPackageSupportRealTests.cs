using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

[Trait("Gate","Real"),Trait("Requirement","FR-BUILD-005"),Trait("Requirement","TEST-BUILD-005")]
public sealed class DocumentationPackageSupportRealTests
{
    [Theory,MemberData(nameof(PackageSupportCases.Cases),MemberType=typeof(PackageSupportCases))]
    public Task SameSupportDescriptorAndCompositionCasesBindNative(string scenario)
    {var e=new PackageSupportEnvironment(scenario);return PackageSupportCases.AssertContract(scenario,e,new DocumentationPackageSupport(new ArtifactReceiptVerifier(new NativeContentHash())));}
}
