using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

[Trait("Gate","Real"),Trait("Requirement","TEST-BUILD-004")]
public sealed class CatalogOriginalAssertionGapRealTests
{
    [Fact]
    public void SameOriginalGroupedIdentitySqlHasExactlyOneBindingAndTwoOccurrences()
    {
        using var provider=new NativeCatalogBuildProvider();
        var observed=provider.Build(CatalogOriginalAssertionGapCases.GroupCase.Call);
        using var connection=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=Path.Combine(provider.EvidenceRoot,"out/catalog.sqlite"),Mode=SqliteOpenMode.ReadOnly,Pooling=false}.ToString());
        connection.Open();using var query=connection.CreateCommand();query.CommandText=CatalogOriginalAssertionGapCases.OriginalGroupedSql;
        using var reader=query.ExecuteReader();var groups=new List<(string,long)>();
        while(reader.Read())groups.Add((reader.GetString(0),reader.GetInt64(1)));
        CatalogOriginalAssertionGapCases.AssertGrouped(observed,groups);
    }
    [Fact]
    public async Task SameOriginalCompleteSdkExtensionObjectsUseTheExistingSingleExtraction()
    {
        JsonArray? captured=null;
        var provider=new NativeCatalogDevicePipelineProvider{SourceIndexExtensionsObserved=value=>captured=value};
        var observed=await provider.RunAsync(CatalogOriginalAssertionGapCases.SdkCase.Call);
        Assert.NotNull(captured);
        await CatalogOriginalAssertionGapCases.AssertExtensions(observed,captured);
    }
}
