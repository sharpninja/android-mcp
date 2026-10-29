using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Catalog;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using AndroidMcp.BuildTool.Sources.Java;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed class NativeScopedProjection:IScopedProjectionProvider
{
    public ScopedProjectionReply Project(ScopedProjectionInput input)
    {
        var fixture=ScopedProjectionCases.Fixture(input);var canonical=new CanonicalJson();var contracts=new CatalogContracts(canonical);
        var facts=new SourceTypeFacts(new JvmSourceLinker(),canonical);
        var rows=fixture.Rows.Select(row=>new CatalogDeclaration(row.SourceId,row.Document["provenance"]![0]!["line"]!.GetValue<int>(),row.Document)).ToArray();
        var projection=new CatalogProjection(fixture.Sources,rows,facts,facts,contracts,canonical);
        var shapes=new List<ProjectedShape>();var digests=new List<string>();
        foreach(var (source,document) in fixture.Types)
        {
            var projected=projection.Project(document,source);var shape=projected["extensions"]!["declared_type_shape"]!.AsObject();
            var fields=shape["fields"]!.AsArray();var traits=shape["hidl_traits"] as JsonObject;
            shapes.Add(new(fields.Select(value=>value!["name"]!.GetValue<string>()).ToArray(),fields.Select(value=>value!["type"]!["text"]!.GetValue<string>()).ToArray(),
                fields.Select(value=>value!["constant"]?.GetValue<string>()).ToArray(),fields.Select(value=>value!["declaration_order"]?.GetValue<int>()).ToArray(),
                shape["field_order_status"]!.GetValue<string>(),traits?["declaration_kind"]?.GetValue<string>(),traits?["storage_type_raw"]?.GetValue<string>(),traits?["typedef_target_raw"]?.GetValue<string>()));
            digests.Add(contracts.Digest(JsonSerializer.SerializeToElement(projected)));
        }
        return new(shapes.ToArray(),digests.ToArray());
    }
}
[Trait("Gate","Real")]
public sealed class ScopedProjectionRealTests
{
    [Theory,MemberData(nameof(ScopedProjectionCases.All),MemberType=typeof(ScopedProjectionCases))]
    public void NativeProjectionUsesSameCasesAndAssertions(ScopedProjectionCase test)=>ScopedProjectionCases.AssertCase(test,new NativeScopedProjection());
}
