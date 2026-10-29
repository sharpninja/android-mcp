using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources.Constraints;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources.Constraints;

[Trait("Gate","Real")]
public sealed class SourceConstraintRealTests
{
    [Theory,MemberData(nameof(SourceConstraintCases.Cases),MemberType=typeof(SourceConstraintCases))]
    public Task SameOriginalAndBoundaryAssertionsBindExactPublicCallableApis(ConstraintCase test)=>SourceConstraintCases.AssertCase(test,Probe);
    private static Task<JsonNode?> Probe(ConstraintCase test)
    {
        var equality=new LegacyJsonValueEquality();var aidl=new AidlSourceConstraints(equality);var hidl=new HidlSourceConstraints(equality);var e=test.Input;
        JsonNode result=test.Api switch{
            "stable"=>JsonValue.Create(aidl.StableModule(e["module"]!.AsObject())),
            "aidl"=>aidl.ExactConstraint(e["source"]!.AsObject(),e["declaration"]!.AsObject(),e["audit"]!.AsObject(),e["published"]!.GetValue<string>(),e["module"]!.AsObject(),e["linkage"]!.AsObject()),
            _=>hidl.ExactConstraint(e["source"]!.AsObject(),e["record"]!.AsObject(),e["facts"]!.AsArray())};
        return Task.FromResult<JsonNode?>(result);
    }
}
