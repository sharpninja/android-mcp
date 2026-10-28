using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources.Java;
using AndroidMcp.BuildTool.Tests.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources.Java;

[Trait("Gate","Real"),Trait("Requirement","TEST-BUILD-004")]
public sealed class JavaRuntimeAdmissionRealTests(JavaNativeFixture fixture):IClassFixture<JavaNativeFixture>
{
    public static IEnumerable<object[]> Cases=>JavaRuntimeAdmissionCases.Cases;
    [Theory,MemberData(nameof(Cases))]
    public void IdenticalAdmissionAndLifetimeCasesBindToLocalNativeLoader(string id)
    {
        var relative=fixture.SourceLock["native_artifact"]!["relative_path"]!.GetValue<string>();
        var native=ArtifactPaths.Resolve(Path.GetDirectoryName(fixture.ManifestPath)!,relative);var material=File.ReadAllBytes(native);
        Assert.Equal(fixture.Parser.ArtifactSha256,ContentHash.Sha256(material));
        var test=JavaRuntimeAdmissionCases.Get(id);var root=ProcessSessionFixturePaths.NewRoot("java-runtime-admission-native");
        var input=JavaRuntimeAdmissionCases.Create(test,material,root);
        JavaRuntimeAdmissionCases.AssertContract(test,new JavaSourceRuntimeLoader(),input);
    }
}
