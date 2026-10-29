using System.Text.Json.Nodes;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog.Migration;

public sealed class ScriptedCatalogMigration(CatalogMigrationCase test,string? wrong=null):ICatalogMigrationCaseProvider
{
    public Task<JsonObject> ExecuteAsync(CatalogMigrationCall call)
    {
        Assert.Equal(test.Call,call);var reply=test.Expected.DeepClone().AsObject();
        if(wrong=="$omit")reply.Remove("source_bytes_unchanged");
        else if(wrong=="$extra")reply["unplanned_success"]=true;
        else if(wrong is not null)
        {
            var value=reply[wrong];reply[wrong]=value is JsonValue scalar&&scalar.TryGetValue<bool>(out var flag)?JsonValue.Create(!flag):JsonValue.Create("independently wrong");
        }
        return Task.FromResult(reply);
    }
}
[Trait("Gate","Mock"),Trait("Requirement","TEST-BUILD-004"),Trait("Requirement","TEST-BUILD-007")]
public sealed class CatalogMigrationMockTests
{
    [Theory,MemberData(nameof(CatalogMigrationCases.All),MemberType=typeof(CatalogMigrationCases))]
    public Task IndependentOriginalMigrationFixturesPass(CatalogMigrationCase test)=>CatalogMigrationCases.AssertCase(test,new ScriptedCatalogMigration(test));
    [Theory,MemberData(nameof(CatalogMigrationCases.All),MemberType=typeof(CatalogMigrationCases))]
    public async Task EveryOriginalAssertionErrorAndRetentionEffectRejectsWrongOrMissingMocks(CatalogMigrationCase test)
    {
        foreach(var key in test.Expected.Select(pair=>pair.Key).Concat(new[]{"$omit","$extra"}))
            Assert.NotNull(await Record.ExceptionAsync(()=>CatalogMigrationCases.AssertCase(test,new ScriptedCatalogMigration(test,key))));
    }
}
