using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources.Constraints;

[Trait("Gate","Mock")]
public sealed class SourceConstraintMockTests
{
    [Theory,MemberData(nameof(SourceConstraintCases.Cases),MemberType=typeof(SourceConstraintCases))]
    public Task ExactOriginalAndBoundaryResponsesPassIndependentMocks(ConstraintCase test)=>SourceConstraintCases.AssertCase(test,value=>SourceConstraintCases.Mock(value));
    [Theory,MemberData(nameof(SourceConstraintCases.Faults),MemberType=typeof(SourceConstraintCases))]
    public async Task EachOutputLeafOrderErrorAndMutationFaultIsRejected(ConstraintCase test,string fault)
        =>Assert.NotNull(await Record.ExceptionAsync(()=>SourceConstraintCases.AssertCase(test,value=>SourceConstraintCases.Mock(value,fault))));
    [Fact]
    public void ExactlyTwelveOriginalMethodsRemainDistinctFromFixtureAndBoundaryCounts()
        =>Assert.Equal(12,SourceConstraintCases.All().Where(test=>test.OriginalMethod is not null).Select(test=>test.Api=="hidl"?"hidl:"+test.OriginalMethod:"aidl:"+test.OriginalMethod).Distinct(StringComparer.Ordinal).Count());
}
