using System.Text;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed class NativeCatalogOriginalTamperProvider:ICatalogOriginalTamperCaseProvider
{
    public CatalogOriginalTamperReply Build(CatalogBuildCall call)
    {
        string? declared=null;byte[]? before=null;
        using var provider=new NativeCatalogBuildProvider
        {
            InventorySetup=(inventory,root,originals)=>
            {
                declared=inventory["sources"]![0]!["sha256"]!.GetValue<string>();before=File.ReadAllBytes(Path.Combine(root,"api.txt"));
                var retained=Path.Combine(root,"api.before-tamper.txt");File.WriteAllBytes(retained,before);Assert.True(File.ReadAllBytes(retained).AsSpan().SequenceEqual(before));originals.Add("api.before-tamper.txt",before);
                var tampered=Encoding.UTF8.GetBytes("tampered");File.WriteAllBytes(Path.Combine(root,"api.txt"),tampered);originals["api.txt"]=tampered;
                // The source metadata digest remains pinned to the preimage.
                Assert.Equal(ContentHash.Sha256(before),declared);
            }
        };
        var result=provider.Build(call);var current=JsonNode.Parse(File.ReadAllText(Path.Combine(provider.EvidenceRoot,"inventory.json")))!;
        return new(result,File.ReadAllText(Path.Combine(provider.EvidenceRoot,"api.txt")),current["sources"]![0]!["sha256"]!.GetValue<string>()==declared,
            before is not null && File.ReadAllBytes(Path.Combine(provider.EvidenceRoot,"api.before-tamper.txt")).AsSpan().SequenceEqual(before));
    }
}
[Trait("Gate","Real")]
[Trait("Requirement","TEST-BUILD-004")]
public sealed class CatalogOriginalTamperRealTests
{
    [Fact] public void NativeExactOriginalRawTamperUsesSameAssertions()=>CatalogOriginalTamperCases.AssertCase(new NativeCatalogOriginalTamperProvider());
}
