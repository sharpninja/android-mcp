using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

[Trait("Gate","Real"),Trait("Requirement","FR-BUILD-005"),Trait("Requirement","TEST-BUILD-005")]
public sealed class DocumentationWrapperPropertiesRealTests
{
    [Theory,MemberData(nameof(WrapperPropertiesCases.Cases),MemberType=typeof(WrapperPropertiesCases))]
    public void SameOriginalPropertyAndReadOnlyCasesBindNativeValidator(WrapperPropertiesCase test)=>WrapperPropertiesCases.AssertContract(test,new DocumentationWrapperPropertiesValidator());
    [Fact]
    public async Task ActualCurrentWrapperIsBoundToCurrentNativeRendererProfile()
    {
        var root=Path.Combine(DocumentationFixture.Current.Workspace,"remediation/design-c4");
        var source=new FileArtifactStore(root,bytes=>ContentHash.Sha256(bytes.Span));
        var profile=new DocumentationRendererProfileReader().Read(new("toolchain-lock.json",await source.ReadAsync("toolchain-lock.json"),"application/json"),
            new("renderer-lock.json",await source.ReadAsync("renderer-lock.json"),"application/json"));
        var wrapper=new Artifact(WrapperPropertiesCases.Path,await source.ReadAsync(WrapperPropertiesCases.Path),"application/octet-stream");
        Assert.Equal(WrapperPropertiesCases.Expected,System.Text.Encoding.UTF8.GetString(wrapper.Bytes.Span));
        new DocumentationWrapperPropertiesValidator().Validate(profile,wrapper);
    }
}
