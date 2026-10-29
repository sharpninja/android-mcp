using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review;

public sealed record PrepareCallerObservation(string Status, int Reads, int ReadinessCalls,
    int PrerequisiteCalls, int Writes, string Head, string ParentRequest, int ProviderCalls);
public sealed record PrepareCallerCase(string Name, PrepareCallerObservation Expected);
public static class ReviewPrepareCallerCases
{
    public static IReadOnlyList<PrepareCallerCase> All { get; } = [
        new("ready", new("prepared",5,1,1,7,"head","req-parent",0)),
        new("draft", new("PR_NOT_READY",5,1,0,0,"","",0)),
        new("wrong-head", new("HEAD_MISMATCH",5,1,0,0,"","",0)),
        new("duplicate-snapshot-property",new("INVALID_PREPARE_INPUT",2,0,0,0,"","",0)),
        new("unknown-snapshot-property",new("INVALID_PREPARE_INPUT",2,0,0,0,"","",0)),
        new("null-inventory",new("INVALID_PREPARE_INPUT",2,0,0,0,"","",0)),
        new("missing-snapshot-field",new("INVALID_PREPARE_INPUT",2,0,0,0,"","",0)),
        new("private-snapshot-payload",new("PRIVATE_PAYLOAD",2,0,0,0,"","",0)),
        new("private-control-path",new("PRIVATE_PAYLOAD",1,0,0,0,"","",0)),
        new("missing-gh-identity",new("INVALID_PREPARE_INPUT",4,0,0,0,"","",0)),
        new("invalid-git-pin",new("INVALID_PREPARE_INPUT",3,0,0,0,"","",0)),
        new("wrong-workspace",new("PREPARE_WORKSPACE_MISMATCH",5,0,0,0,"","",0)),
        new("unsafe-receipt-pin",new("INVALID_PREPARE_INPUT",5,0,0,0,"","",0)),
        new("missing-prerequisite-pin",new("INVALID_PREPARE_INPUT",5,0,0,0,"","",0)),
        new("invalid-input-path",new("INVALID_PREPARE_INPUT",0,0,0,0,"","",0)),
        new("changed-snapshot-hash",new("ARTIFACT_PIN_HASH",2,0,0,0,"","",0)),
        new("changed-config-length",new("ARTIFACT_PIN_LENGTH",5,0,0,0,"","",0)),
        new("trusted-prerequisite-failure",new("PREREQUISITE_UNAVAILABLE",5,1,1,0,"","",0))
    ];
    public static IEnumerable<object[]> Data => All.Select(x=>new object[]{x.Name});
    public static PrepareCallerCase Find(string name)=>All.Single(x=>x.Name==name);
    public static void AssertContract(PrepareCallerCase item,PrepareCallerObservation actual)=>Assert.Equal(item.Expected,actual);
}
public sealed class ReviewPrepareCallerMockTests
{
    [Theory,MemberData(nameof(ReviewPrepareCallerCases.Data),MemberType=typeof(ReviewPrepareCallerCases))]
    [Trait("Gate","Mock")]
    public void CorrectResponseAndEveryWrongFieldControl(string name)
    {
        var item=ReviewPrepareCallerCases.Find(name);
        var responseFromBoundary=ScriptedPrepareBoundary(name);
        ReviewPrepareCallerCases.AssertContract(item,responseFromBoundary);
        PrepareCallerObservation[] wrong=[
            item.Expected with{Status="wrong"},item.Expected with{Reads=item.Expected.Reads+1},
            item.Expected with{ReadinessCalls=item.Expected.ReadinessCalls+1},
            item.Expected with{PrerequisiteCalls=item.Expected.PrerequisiteCalls+1},
            item.Expected with{Writes=item.Expected.Writes+1},item.Expected with{Head="wrong"},
            item.Expected with{ParentRequest="wrong"},item.Expected with{ProviderCalls=1}];
        foreach(var response in wrong) Assert.ThrowsAny<Exception>(()=>ReviewPrepareCallerCases.AssertContract(item,response));
    }
    private static PrepareCallerObservation ScriptedPrepareBoundary(string name)=>name switch
    {
        "ready"=>new("prepared",5,1,1,7,"head","req-parent",0),
        "draft"=>new("PR_NOT_READY",5,1,0,0,"","",0),
        "wrong-head"=>new("HEAD_MISMATCH",5,1,0,0,"","",0),
        "wrong-workspace"=>new("PREPARE_WORKSPACE_MISMATCH",5,0,0,0,"","",0),
        "invalid-input-path"=>new("INVALID_PREPARE_INPUT",0,0,0,0,"","",0),
        "missing-gh-identity"=>new("INVALID_PREPARE_INPUT",4,0,0,0,"","",0),
        "invalid-git-pin"=>new("INVALID_PREPARE_INPUT",3,0,0,0,"","",0),
        "unsafe-receipt-pin" or "missing-prerequisite-pin"=>new("INVALID_PREPARE_INPUT",5,0,0,0,"","",0),
        "private-snapshot-payload"=>new("PRIVATE_PAYLOAD",2,0,0,0,"","",0),
        "private-control-path"=>new("PRIVATE_PAYLOAD",1,0,0,0,"","",0),
        "changed-snapshot-hash"=>new("ARTIFACT_PIN_HASH",2,0,0,0,"","",0),
        "changed-config-length"=>new("ARTIFACT_PIN_LENGTH",5,0,0,0,"","",0),
        "trusted-prerequisite-failure"=>new("PREREQUISITE_UNAVAILABLE",5,1,1,0,"","",0),
        _=>new("INVALID_PREPARE_INPUT",2,0,0,0,"","",0)
    };
}
