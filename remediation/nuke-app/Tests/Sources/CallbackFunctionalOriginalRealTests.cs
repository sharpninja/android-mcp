using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate","Real")]
[Trait("Requirement","TEST-BUILD-004")]
public sealed class CallbackFunctionalOriginalRealTests
{
    [Theory,MemberData(nameof(CallbackFunctionalOriginalCases.Cases),MemberType=typeof(CallbackFunctionalOriginalCases))]
    public void ExistingNativeProducerUsesExactOriginalFunctionInputsAndAssertions(string id)
    {
        var test=CallbackFunctionalOriginalCases.Get(id);var producer=new SourceFactProducer(new CanonicalJson());
        CallbackFunctionalOriginalCases.AssertCase(test,CallbackFunctionalOriginalReplay.Native(test,producer,producer));
    }
}
