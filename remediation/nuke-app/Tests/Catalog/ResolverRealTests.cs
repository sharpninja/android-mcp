using System.Text.Json;
using AndroidMcp.BuildTool.Catalog;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed class NativeResolverProvider:IResolverCaseProvider
{
    private static string SchemaPath()
    {
        for(var directory=new DirectoryInfo(AppContext.BaseDirectory);directory is not null;directory=directory.Parent)
        {
            var path=Path.Combine(directory.FullName,"remediation","catalog","contracts","DeviceView.schema.json");
            if(File.Exists(path))
            {
                Assert.Equal("706d34871610f7e97ed0a00edd63bfb8961d39747a705835ed0b942ff27f807f",ContentHash.Sha256(File.ReadAllBytes(path)));
                return path;
            }
        }
        throw new FileNotFoundException("Pinned DeviceView.schema.json fixture was not found");
    }
    private static JsonElement Input(System.Text.Json.Nodes.JsonNode value)
    {
        using var bytes=new MemoryStream();using(var writer=new Utf8JsonWriter(bytes)) value.WriteTo(writer);
        using var document=JsonDocument.Parse(bytes.ToArray());return document.RootElement.Clone();
    }
    private static string? Optional(JsonElement value,string name)=>value.GetProperty(name).ValueKind==JsonValueKind.Null ? null : value.GetProperty(name).GetString();
    public ResolverReply Resolve(ResolverInput input)
    {
        var service=new DeviceResolver(new CanonicalJson(),SchemaPath());
        var result=service.Resolve(input.Inventory.Select(Input).ToArray(),Input(input.Facts),
            new(input.RegistryId,input.CatalogGeneration,input.OverlayGeneration,input.Epoch,input.Generation));
        var rows=result.GetProperty("entries").EnumerateArray().Select(row=>new ResolverRow(
            row.GetProperty("entry_id").GetString()!,row.GetProperty("entity_kind").GetString()!,row.GetProperty("state").GetString()!,
            row.GetProperty("reason_codes").EnumerateArray().Select(x=>x.GetString()!).ToArray(),Optional(row,"contract_hash"),Optional(row,"schema_hash"),Optional(row,"graph_digest"),
            row.GetProperty("dependencies").EnumerateArray().Select(x=>new ResolverDependency(x.GetProperty("entry_id").GetString()!,x.GetProperty("contract_hash").GetString()!)).ToArray(),
            row.GetProperty("catalog_state").GetString()!,row.GetProperty("catalog_revision").GetInt64(),Optional(row,"catalog_document_sha256"))).ToArray();
        return new(rows,result.GetProperty("registry_id").GetString()!,result.GetProperty("catalog_generation").GetInt64(),result.GetProperty("overlay_generation").GetInt64(),
            result.GetProperty("capability_epoch").GetInt64(),result.GetProperty("view_generation").GetInt64(),result.GetProperty("inventory_count").GetInt32());
    }
}
[Trait("Gate","Real")]
public sealed class ResolverRealTests
{
    [Theory,MemberData(nameof(ResolverCases.All),MemberType=typeof(ResolverCases))]
    public void NativeResolverUsesSameCasesAndAssertions(ResolverCase test)=>ResolverCases.AssertCase(test,new NativeResolverProvider());
}
