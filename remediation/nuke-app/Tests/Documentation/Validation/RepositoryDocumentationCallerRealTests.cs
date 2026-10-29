using AndroidMcp.BuildTool.Documentation.Validation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation.Validation;

[Trait("Gate","Real"),Trait("Requirement","FR-BUILD-005"),Trait("Requirement","TEST-BUILD-005")]
public sealed class RepositoryDocumentationCallerRealTests
{
    [Theory,MemberData(nameof(RepositoryCallerCases.Cases),MemberType=typeof(RepositoryCallerCases))]
    public Task SameMainPrintExitAndExceptionCasesBindNativeCaller(string scenario)
    {var e=new RepositoryCallerEnvironment(scenario);return RepositoryCallerCases.AssertContract(e,new RepositoryDocumentationCaller(e.Checker).RunAsync);}
}
