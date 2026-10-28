using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review.Staging;

public sealed class SdkNamedTargetLookupNativeTests
{
    [Theory,MemberData(nameof(SdkNamedTargetCases.Data),MemberType=typeof(SdkNamedTargetCases))]
    public void Same_three_named_dictionary_cases_bind_to_native_target_provenance_guard(string name)
    {
        var fixture=new SdkNamedTargetFixture(name);
        var actual=SdkNamedTargetCases.Execute(fixture,NativeTestGateSdkSessionFactory.VerifyNamedReferenceTarget);
        SdkNamedTargetCases.AssertContract(fixture,JsonSerializer.SerializeToNode(actual)!);
    }
}
