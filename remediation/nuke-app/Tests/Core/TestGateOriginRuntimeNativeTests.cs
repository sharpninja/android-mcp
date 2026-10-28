using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review.Staging;

public sealed class TestGateOriginRuntimeNativeTests
{
    [Theory,MemberData(nameof(OriginRuntimeCases.Data),MemberType=typeof(OriginRuntimeCases))]
    public async Task SameThirteenCasesBindToNativeRuntimeWriterAndBclPeMetadata(string name)
    {
        var fixture=new OriginRuntimeFixture(name);var io=new OriginRuntimeDependencies(fixture);
        var service=new NativeTestGateOriginRuntimeWriter(io,io,io,io);var result=await service.WriteIfRequestedAsync(fixture.Request);
        var actual=new OriginRuntimeObservation(fixture.Request,result,io.Reads,io.Files,io.Writes,io.Loaded,io.Order);
        OriginRuntimeCases.AssertContract(fixture,OriginRuntimeCases.Json(actual));
    }
}
