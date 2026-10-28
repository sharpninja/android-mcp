using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review.Staging;

public sealed class TestGateSdkChildBindingNativeTests
{
    [Theory,MemberData(nameof(SdkChildBindingCases.Data),MemberType=typeof(SdkChildBindingCases))]
    public async Task Same_seven_cases_bind_to_native_admission_with_controlled_Default_binder(string name)
    {
        var fixture=new SdkChildBindingFixture(name);var io=new SdkChildBindingDependencies(fixture);
        var factory=new NativeTestGateSdkSessionFactory(fixture.Source.SdkRoot,io.Pins);
        var observed=await SdkChildBindingCases.ObserveAsync(fixture,io,
            ()=>factory.OpenChildWithBinderAsync(fixture.Request,fixture.LoadedNames,io.Bind));
        SdkChildBindingCases.AssertContract(fixture,JsonSerializer.SerializeToNode(observed)!);
    }
}
