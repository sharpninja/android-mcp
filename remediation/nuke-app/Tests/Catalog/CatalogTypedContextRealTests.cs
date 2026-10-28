using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Catalog;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed class NativeCatalogTypedContextProvider:ICatalogTypedContextCaseProvider
{
    public Task<CatalogTypedContextReply> BuildAsync(CatalogTypedContextCall call)
    {
        using var provider=new NativeCatalogBuildProvider
        {
            InventorySetup=(inventory,_,_)=>
            {
                if(call.DegradedBridge)inventory["x-native-catalog-input"]=new JsonObject{["contract"]="source-inventory-bridge-v1",["requires_typed_context"]=true};
            }
        };
        var actual=provider.Build(new("typed-context"));
        var effects=Directory.Exists(Path.Combine(provider.EvidenceRoot,"out")) || Directory.Exists(Path.Combine(provider.EvidenceRoot,"private-stage"));
        return Task.FromResult(new CatalogTypedContextReply(actual.Outcome,actual.FinalDatabase,effects,
            actual.OriginalInputsRetained,actual.Sources,actual.Outcome=="catalog-typed-context-required",actual.OriginalInputsRetained));
    }
}
[Trait("Gate","Real")]
[Trait("Requirement","TEST-BUILD-004")]
[Trait("Requirement","TEST-BUILD-007")]
public sealed class CatalogTypedContextRealTests
{
    [Theory,MemberData(nameof(CatalogTypedContextCases.All),MemberType=typeof(CatalogTypedContextCases))]
    public Task NativeBuildPreservesSameLegacyAndDowngradeAssertions(CatalogTypedContextCase test)=>
        CatalogTypedContextCases.AssertCase(test,new NativeCatalogTypedContextProvider());
}
