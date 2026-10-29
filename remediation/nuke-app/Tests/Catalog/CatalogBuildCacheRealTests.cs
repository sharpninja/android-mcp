using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed class NativeCatalogBuildCacheProvider:ICatalogBuildCacheCaseProvider
{
    public CatalogBuildCacheReply Build(CatalogBuildCall call)
    {
        using var native=new NativeCatalogBuildProvider();var result=native.Build(call);var root=native.EvidenceRoot;
        var source=File.ReadAllLines(Path.Combine(root,"out/source_details.jsonl")).Where(line=>line.Length!=0)
            .Select(line=>JsonNode.Parse(JsonNode.Parse(line)!["document_json"]!.GetValue<string>())!.AsObject()).Single(source=>source["path"]!.GetValue<string>()=="api.txt");
        var audit=source["x-native-cache-audit"]!.AsObject();var cache=audit["historical_cache"]!;
        var canonical=new CanonicalJson();using var db=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=Path.Combine(root,"out/catalog.sqlite"),Mode=SqliteOpenMode.ReadOnly,Pooling=false}.ToString());db.Open();
        using var count=db.CreateCommand();count.CommandText="SELECT count(*) FROM binding_declarations";var declarations=(int)(long)count.ExecuteScalar()!;
        return new(result,File.ReadAllBytes(Path.Combine(root,"cache.jsonl.gz")).AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(CatalogBuildCacheCases.CacheText)),
            audit["disposition"]!.GetValue<string>(),cache["parser_sha256"]!.GetValue<string>(),cache["records"]!.GetValue<int>(),
            Encoding.UTF8.GetString(canonical.Serialize(JsonSerializer.SerializeToElement(audit["historical_context"]))),declarations,
            audit["records_consumed"]!.GetValue<bool>(),audit["historical_identity_claimed"]!.GetValue<bool>() || source["x-native-producer"]!["historical_python_identity"]!.GetValue<bool>());
    }
}
[Trait("Gate","Real")]
public sealed class CatalogBuildCacheRealTests
{
    [Theory,MemberData(nameof(CatalogBuildCacheCases.All),MemberType=typeof(CatalogBuildCacheCases))]
    public void NativeRawExtractionUsesSameCacheAuditAssertions(CatalogBuildCacheCase test)=>CatalogBuildCacheCases.AssertCase(test,new NativeCatalogBuildCacheProvider());
}
