using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review.Staging;

public sealed class NativeTestGateFactsNativeTests
{
    [Theory,MemberData(nameof(NativeFactsCases.Data),MemberType=typeof(NativeFactsCases))]
    public async Task SameThirtyCasesBindToActualCollectorAndBclPeParsing(string name)
    {
        var fixture=new NativeFactsFixture(name);var io=new NativeFactsDependencies(fixture);
        var facts=new SdkEvaluatedTestGateNativeFacts(fixture.Configuration,io,io,io,io,io,io);
        TestGateSourceSnapshot? snapshot=null;TestGateBinaryIdentity? host=null;Guid? loaded=null;
        TestGateTestsOutput? tests=null;TestGateBinaryIdentity? executed=null;IReadOnlyDictionary<string,string>? environment=null;
        var code="OK";
        try
        {
            if(fixture.Operation=="source")snapshot=await facts.CaptureSourceAsync(fixture.Request,TestGateSnapshotPoint.BeforeBuild);
            else if(fixture.Operation=="host"){loaded=facts.ReadRunningHostModuleId();host=await facts.ReadBuiltHostAsync(fixture.Request);}
            else
            {
                tests=await facts.ReadBuiltTestsAsync(fixture.Request);
                if(fixture.Operation=="origin")
                {
                    environment=await facts.PrepareTestHostEnvironmentAsync(fixture.Request);
                    if(fixture.Name=="origin-replay")await facts.PrepareTestHostEnvironmentAsync(fixture.Request);
                    else executed=await facts.ReadExecutedTestsAsync(fixture.Request,new(0,"unused-stdout","unused-stderr",DateTimeOffset.UnixEpoch,DateTimeOffset.UnixEpoch,false));
                }
            }
        }
        catch(TestGateFactsException error){code=error.Code;}
        var actual=io.Observe(code,snapshot,host,loaded,tests,executed,environment);
        NativeFactsCases.AssertContract(fixture,NativeFactsCases.Json(actual));
    }
}
