using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review.Staging;

public sealed class TestGateResolvedReferenceQueryNativeTests
{
    [Theory,MemberData(nameof(ReferenceQueryCases.Data),MemberType=typeof(ReferenceQueryCases))]
    public async Task SameFifteenCasesBindToActualNativeQueryAdapter(string name)
    {
        var fixture=new ReferenceQueryFixture(name);var io=new ReferenceQueryDependencies(fixture);
        var result=await new NativeTestGateResolvedReferenceQuery(io,io,io).QueryAsync(fixture.Request);
        ReferenceQueryCases.AssertContract(fixture,ReferenceQueryCases.Json(io.Observe(result)));
    }
}
