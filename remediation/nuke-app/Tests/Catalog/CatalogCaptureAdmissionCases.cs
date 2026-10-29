using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public static class CatalogCaptureAdmissionCases
{
    public static IEnumerable<object[]> All=>new[]
    {
        new CatalogEffectiveInventoryCase("CAPTURE-ENTRY-SCHEMA-INDEPENDENT",new("invalid-active-schema"),new("Invalid local overlay Entry contract")),
        new CatalogEffectiveInventoryCase("CAPTURE-ROW-KEY-INDEPENDENT",new("overlay-row-key-mismatch"),new("Local overlay canonical identity mismatch"))
    }.Select(test=>new object[]{test});
}
[Trait("Gate","Mock")]
[Trait("Requirement","TEST-BUILD-004")]
[Trait("Requirement","TEST-BUILD-007")]
public sealed class CatalogCaptureAdmissionMockTests
{
    [Theory,MemberData(nameof(CatalogCaptureAdmissionCases.All),MemberType=typeof(CatalogCaptureAdmissionCases))]
    public Task CorrectOriginalActiveAdmissionErrorsAndRollbackResponsesPass(CatalogEffectiveInventoryCase test)=>CatalogEffectiveInventoryCases.AssertCase(test,new ScriptedCatalogEffectiveInventory(test));
    [Theory,MemberData(nameof(CatalogCaptureAdmissionCases.All),MemberType=typeof(CatalogCaptureAdmissionCases))]
    public async Task EveryErrorTransactionInputAndPartialCaptureMutationRejected(CatalogEffectiveInventoryCase test)
    {for(var wrong=0;wrong<17;wrong++)Assert.NotNull(await Record.ExceptionAsync(()=>CatalogEffectiveInventoryCases.AssertCase(test,new ScriptedCatalogEffectiveInventory(test,wrong))));}
}
