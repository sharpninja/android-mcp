using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed record CatalogOriginalTamperReply(CatalogBuildReply BuildReply,string ActualRawInput,bool DeclaredOriginalShaRetained,bool PreimageRetained);
public interface ICatalogOriginalTamperCaseProvider { CatalogOriginalTamperReply Build(CatalogBuildCall call); }
public static class CatalogOriginalTamperCases
{
    // Exact test_build.BuildTests.test_hash_failure_does_not_publish changes
    // api.txt to these bytes after recording the original manifest digest.
    public static readonly CatalogBuildCall Call=new("original-raw-source-tampered");
    public static readonly CatalogOriginalTamperReply Expected=new(CatalogBuildCases.Failure("source-sha-mismatch"),"tampered",true,true);
    public static void AssertCase(ICatalogOriginalTamperCaseProvider provider)=>Assert.Equal(Expected,provider.Build(Call));
}
public sealed class ScriptedCatalogOriginalTamper(int wrong=-1):ICatalogOriginalTamperCaseProvider
{
    public CatalogOriginalTamperReply Build(CatalogBuildCall call)
    {
        Assert.Same(CatalogOriginalTamperCases.Call,call);var value=CatalogOriginalTamperCases.Expected;
        if(wrong<24 && wrong>=0)
        {
            using var provider=new ScriptedCatalogBuild(new("original-tamper",call,value.BuildReply),wrong);
            return value with{BuildReply=provider.Build(call)};
        }
        return wrong switch {24=>value with{ActualRawInput="original"},25=>value with{DeclaredOriginalShaRetained=false},26=>value with{PreimageRetained=false},_=>value};
    }
}
[Trait("Gate","Mock")]
[Trait("Requirement","TEST-BUILD-004")]
public sealed class CatalogOriginalTamperMockTests
{
    [Fact] public void ExactOriginalTamperedBytesResponsePasses()=>CatalogOriginalTamperCases.AssertCase(new ScriptedCatalogOriginalTamper());
    [Fact] public void EveryExistingBuildAndTamperInteractionMutationRejected()
    {for(var wrong=0;wrong<27;wrong++) Assert.NotNull(Record.Exception(()=>CatalogOriginalTamperCases.AssertCase(new ScriptedCatalogOriginalTamper(wrong))));}
}
