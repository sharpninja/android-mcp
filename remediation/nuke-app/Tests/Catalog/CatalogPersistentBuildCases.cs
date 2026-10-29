using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed record CatalogPersistentBuildCall(string Variant);
public sealed record CatalogPersistentBuildReply(string Outcome,int Entries,int ReopenedStageEntries,bool StageExists,bool StageClosedAtFinalCopy,
    bool StageRetained,bool StageBytesMatchFinal,int StreamWrites,int BufferedArtifactWrites,int LargestWrite,bool FinalMarker,bool FailureReceipt,int RetainedPartialFiles);
public sealed record CatalogPersistentBuildCase(string Id,CatalogPersistentBuildCall Call,CatalogPersistentBuildReply Expected);
public interface ICatalogPersistentBuildCaseProvider { CatalogPersistentBuildReply Build(CatalogPersistentBuildCall call); }
public static class CatalogPersistentBuildCases
{
    // Reuse the original BuildTests.fixture source bytes from CatalogBuildCases.
    // Positive replies are contract literals, not output from the staged build.
    public static IEnumerable<object[]> All=>new[]
    {
        new CatalogPersistentBuildCase("PERSIST-01-file-backed-stage-and-nineteen-streamed-artifacts",new("base"),new("ok",10,10,true,true,true,true,19,0,65536,true,false,0)),
        new CatalogPersistentBuildCase("PERSIST-02-duplicate-occurrences-remain-streamed",new("duplicate-source"),new("ok",10,10,true,true,true,true,19,0,65536,true,false,0)),
        new CatalogPersistentBuildCase("PERSIST-03-stream-export-failure-retains-stage-and-partial",new("fail-inventory-stream"),new("export-failure",10,10,true,false,true,false,14,1,65536,false,true,1)),
        new CatalogPersistentBuildCase("PERSIST-04-populated-output-has-no-stage-or-writes",new("nonempty-output"),new("nonempty-output",0,0,false,false,false,false,0,0,0,false,false,0))
    }.Select(test=>new object[]{test});
    public static void AssertCase(CatalogPersistentBuildCase test,ICatalogPersistentBuildCaseProvider provider)
    {
        var value=provider.Build(test.Call);var expected=test.Expected;
        Assert.Equal(expected.Outcome,value.Outcome);Assert.Equal(expected.Entries,value.Entries);Assert.Equal(expected.ReopenedStageEntries,value.ReopenedStageEntries);
        Assert.Equal(expected.StageExists,value.StageExists);Assert.Equal(expected.StageClosedAtFinalCopy,value.StageClosedAtFinalCopy);Assert.Equal(expected.StageRetained,value.StageRetained);
        Assert.Equal(expected.StageBytesMatchFinal,value.StageBytesMatchFinal);Assert.Equal(expected.StreamWrites,value.StreamWrites);Assert.Equal(expected.BufferedArtifactWrites,value.BufferedArtifactWrites);
        // The transport must make bounded writes. The literal is an upper bound,
        // not an implementation-derived exact buffer size.
        Assert.InRange(value.LargestWrite,expected.LargestWrite==0 ? 0 : 1,expected.LargestWrite);
        Assert.Equal(expected.FinalMarker,value.FinalMarker);Assert.Equal(expected.FailureReceipt,value.FailureReceipt);Assert.Equal(expected.RetainedPartialFiles,value.RetainedPartialFiles);
    }
}
public sealed class ScriptedCatalogPersistentBuild(CatalogPersistentBuildCase test,int wrong=-1):ICatalogPersistentBuildCaseProvider
{
    public CatalogPersistentBuildReply Build(CatalogPersistentBuildCall call)
    {
        Assert.Same(test.Call,call);var value=test.Expected;
        return wrong switch
        {
            0=>value with{Outcome="wrong"},1=>value with{Entries=value.Entries+1},2=>value with{ReopenedStageEntries=value.ReopenedStageEntries+1},
            3=>value with{StageExists=!value.StageExists},4=>value with{StageClosedAtFinalCopy=!value.StageClosedAtFinalCopy},5=>value with{StageRetained=!value.StageRetained},
            6=>value with{StageBytesMatchFinal=!value.StageBytesMatchFinal},7=>value with{StreamWrites=value.StreamWrites+1},8=>value with{BufferedArtifactWrites=value.BufferedArtifactWrites+1},
            9=>value with{LargestWrite=value.LargestWrite+1},10=>value with{FinalMarker=!value.FinalMarker},11=>value with{FailureReceipt=!value.FailureReceipt},12=>value with{RetainedPartialFiles=value.RetainedPartialFiles+1},_=>value
        };
    }
}
[Trait("Gate","Mock")]
public sealed class CatalogPersistentBuildMockTests
{
    [Theory,MemberData(nameof(CatalogPersistentBuildCases.All),MemberType=typeof(CatalogPersistentBuildCases))]
    public void CorrectRetainedStageAndStreamObservationsPass(CatalogPersistentBuildCase test)=>CatalogPersistentBuildCases.AssertCase(test,new ScriptedCatalogPersistentBuild(test));
    [Theory,MemberData(nameof(CatalogPersistentBuildCases.All),MemberType=typeof(CatalogPersistentBuildCases))]
    public void EveryPersistenceCopyAndBoundedWriteObservationMutationRejected(CatalogPersistentBuildCase test)
    {for(var wrong=0;wrong<13;wrong++) Assert.NotNull(Record.Exception(()=>CatalogPersistentBuildCases.AssertCase(test,new ScriptedCatalogPersistentBuild(test,wrong))));}
}
