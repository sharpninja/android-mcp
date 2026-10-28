using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

[Trait("Gate","Real")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class ArtifactReceiptVerifierRealTests
{
    [Theory,MemberData(nameof(ArtifactReceiptCases.Data),MemberType=typeof(ArtifactReceiptCases))]
    public void SameMetadataCasesUseNativeHash(ArtifactReceiptCase test)=>ArtifactReceiptCases.AssertCase(test,new ArtifactReceiptVerifier(new NativeContentHash()),null);
    [Theory,MemberData(nameof(ArtifactReceiptCases.Data),MemberType=typeof(ArtifactReceiptCases))]
    public void SameMetadataCasesPreserveHashInteractions(ArtifactReceiptCase test)
    {var hashes=new RecordingReceiptHash();ArtifactReceiptCases.AssertCase(test,new ArtifactReceiptVerifier(hashes),hashes);}
}
