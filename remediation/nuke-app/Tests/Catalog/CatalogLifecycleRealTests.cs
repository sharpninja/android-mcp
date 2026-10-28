using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Catalog;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed class NativeLifecycleProvider:ILifecycleCaseProvider
{
    private readonly CanonicalJson canonical=new();
    public string Project(JsonObject entry,JsonObject source)=>Encoding.UTF8.GetString(canonical.Serialize(new CatalogLifecycle().Project(JsonSerializer.SerializeToElement(entry),JsonSerializer.SerializeToElement(source))));
}
[Trait("Gate","Real")]
public sealed class CatalogLifecycleRealTests
{
    [Theory,MemberData(nameof(CatalogLifecycleCases.All),MemberType=typeof(CatalogLifecycleCases))]
    public void NativeLifecycleUsesSameCasesAndAssertions(LifecycleCase test)=>CatalogLifecycleCases.AssertCase(test,new NativeLifecycleProvider());
}
