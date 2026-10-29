using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

[Trait("Gate","Real"),Trait("Requirement","TEST-BUILD-005")]
public sealed class ArtifactReferenceRealTests
{
    [Theory,MemberData(nameof(ArtifactReferenceCases.Cases),MemberType=typeof(ArtifactReferenceCases))]
    public void IdenticalLegacyProfileAndConfinedNamespaceCasesBindPureResolver(string id)
    {
        var calls=new List<string>();var decoder=new SourceHtmlDecoder();
        var resolver=new ArtifactReferenceResolver(value=>{calls.Add(value);return decoder.Decode(value);});
        ArtifactReferenceCases.AssertContract(ArtifactReferenceCases.Get(id),new(resolver,calls));
    }
}
