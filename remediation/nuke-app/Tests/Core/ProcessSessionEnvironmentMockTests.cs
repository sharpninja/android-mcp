using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

[CollectionDefinition("OwnedProcessEnvironment",DisableParallelization=true)]
public sealed class OwnedProcessEnvironmentCollection;

[Collection("OwnedProcessEnvironment"),Trait("Gate","Mock"),Trait("Requirement","TEST-BUILD-003")]
public sealed class ProcessSessionEnvironmentMockTests
{
    [Theory,InlineData(false),InlineData(true)]
    public async Task InheritAndExplicitClearBothApplyPublicOverrideLast(bool clear)
    {
        foreach(var mutation in new string?[]{null,"inherit","override"})
        {
            var root=Path.Combine(Path.GetTempPath(),"androidmcp-environment-mock",Guid.NewGuid().ToString("N"));var request=ProcessRunnerRealTests.Request(root,0,"environment",[ProcessSessionEnvironmentCases.Name]);
            var error=await Record.ExceptionAsync(()=>ProcessSessionEnvironmentCases.AssertContract(clear,new ScriptedEnvironmentFactory(mutation),request));if(mutation is null)Assert.Null(error);else Assert.NotNull(error);
        }
    }
}
