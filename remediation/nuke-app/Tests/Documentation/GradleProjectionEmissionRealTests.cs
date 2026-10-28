using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

[Trait("Gate","Real")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class GradleProjectionEmissionRealTests
{
    [Theory,MemberData(nameof(GradleProjectionParameterCases.Cases),MemberType=typeof(GradleProjectionParameterCases))]
    public void SameNoInputParameterCasesBindRootRegistry(GradleProjectionParameterCase test)=>
        GradleProjectionParameterCases.AssertPlan(test,new OperationPlanner(new OperationRegistry()));

    [Theory,MemberData(nameof(GradleProjectionRecipeCases.Cases),MemberType=typeof(GradleProjectionRecipeCases))]
    public Task SameDeferredProjectionAndCancellationCasesBindNativeRecipe(bool cancelled)
    {
        var environment=new GradleProjectionRecipeEnvironment(cancelled);
        var projection=new NativeDesignGradleProjection(new ObservedCanonical(environment),new ArtifactReceiptVerifier(new NativeContentHash()));
        return GradleProjectionRecipeCases.AssertRecipe(environment,()=>projection.CreateRecipe(environment.Input.Stages,environment.Input.Operation));
    }

    private sealed class ObservedCanonical(GradleProjectionRecipeEnvironment environment):ICanonicalJson
    {
        private readonly ICanonicalJson authority=new CanonicalJson();
        public byte[] Serialize(JsonElement value){environment.Trace.Add("projection");return authority.Serialize(value);}
        public byte[] Serialize(ReadOnlyMemory<byte> value)=>throw new InvalidOperationException("Existing descriptor projection supplies its typed JSON value");
        public string Hash(JsonElement value)=>throw new InvalidOperationException("Recipe must not invent an extra projection hash");
        public string Hash(ReadOnlyMemory<byte> value)=>throw new InvalidOperationException("Recipe must not invent an extra projection hash");
    }
}
