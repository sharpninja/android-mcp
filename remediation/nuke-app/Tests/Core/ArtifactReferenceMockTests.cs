using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

internal sealed class ScriptedArtifactReferences(ArtifactReferenceCase test,List<string> calls,int wrong=-1):IArtifactReferenceResolver
{
    public ArtifactReferenceResolution Resolve(ArtifactReferenceRequest request)
    {
        Assert.Equal(test.Request,request);
        if(wrong==4){if(request.Profile!=ArtifactReferenceProfile.RepositoryOfflineAsset)calls.Add("invented");}
        else if(request.Profile==ArtifactReferenceProfile.RepositoryOfflineAsset)calls.Add(request.Reference);
        return wrong switch
        {
            0=>test.Expected with{Disposition=test.Expected.Disposition==ArtifactReferenceDisposition.Invalid?ArtifactReferenceDisposition.Local:ArtifactReferenceDisposition.Invalid},
            1=>test.Expected with{RootRelativePath=test.Expected.RootRelativePath is null?"wrong":test.Expected.RootRelativePath+"wrong"},
            2=>test.Expected with{DirectoryAllowed=!test.Expected.DirectoryAllowed},
            3=>test.Expected with{Reason=test.Expected.Reason is null?"wrong":test.Expected.Reason+"wrong"},
            _=>test.Expected
        };
    }
}
[Trait("Gate","Mock")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class ArtifactReferenceMockTests
{
    [Theory,MemberData(nameof(ArtifactReferenceCases.Cases),MemberType=typeof(ArtifactReferenceCases))]
    public void ExactLegacyProfileAndConfinedNamespaceCasesPassScriptedResponses(string id)
    {var test=ArtifactReferenceCases.Get(id);var calls=new List<string>();ArtifactReferenceCases.AssertContract(test,new(new ScriptedArtifactReferences(test,calls),calls));}
    [Theory,MemberData(nameof(ArtifactReferenceCases.Cases),MemberType=typeof(ArtifactReferenceCases))]
    public void EachDispositionPathDirectoryReasonAndDecoderControlIsRejected(string id)
    {var test=ArtifactReferenceCases.Get(id);for(var wrong=0;wrong<5;wrong++){var calls=new List<string>();Assert.NotNull(Record.Exception(()=>ArtifactReferenceCases.AssertContract(test,new(new ScriptedArtifactReferences(test,calls,wrong),calls))));}}
}
