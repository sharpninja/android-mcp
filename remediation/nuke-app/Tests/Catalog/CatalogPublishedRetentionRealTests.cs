using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed class NativeCatalogPublishedRetentionProvider:ICatalogPublishedRetentionProvider
{
    public async Task<CatalogPublishedRetentionReply> ImportAsync(CatalogHidlImportCall call)
    {
        var provider=new NativeCatalogHidlImportProvider();var actual=await provider.ImportAsync(call);
        var report=Assert.IsType<System.Text.Json.Nodes.JsonObject>(provider.RetentionReport);
        var source=Assert.IsType<System.Text.Json.Nodes.JsonObject>(provider.ObservedSource);
        var hidl=call.Variant=="published-retained-hidl";
        return new(actual.Outcome,actual.FinalDatabase,actual.SqlRawText,actual.DecodedRawHex,actual.FlatRawEncoding!,
            hidl?report["declarations"]!.GetValue<int>():null,hidl?report["compiler_validation"]!.GetValue<bool>():null,
            actual.FixtureUrl!,actual.FixtureChannel!,actual.FixtureSourceCount,!source.ContainsKey("source_release_context"),actual.OriginalInputsRetained,actual.QueriedIssueRows??0);
    }
}
[Trait("Gate","Real"),Trait("Requirement","TEST-BUILD-004"),Trait("Requirement","TEST-BUILD-007")]
public sealed class CatalogPublishedRetentionRealTests
{
    [Theory,MemberData(nameof(CatalogPublishedRetentionCases.All),MemberType=typeof(CatalogPublishedRetentionCases))]
    public Task SameFrozenRawRetentionInputsAssertionsAndHelperContext(CatalogPublishedRetentionCase test)=>CatalogPublishedRetentionCases.AssertCase(test,new NativeCatalogPublishedRetentionProvider());
}
