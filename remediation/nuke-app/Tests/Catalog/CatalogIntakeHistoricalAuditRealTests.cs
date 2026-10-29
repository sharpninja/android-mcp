using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed class NativeCatalogIntakeHistoricalAudit:ICatalogIntakeHistoricalAuditProvider
{
    public async Task<CatalogIntakeHistoricalAuditReply> ImportAsync(CatalogSourceIntakeCall call)
    {
        var provider=new NativeCatalogSourceIntakeProvider();await provider.ImportAsync(call);
        return Assert.IsType<CatalogIntakeHistoricalAuditReply>(provider.HistoricalAudit);
    }
}
[Trait("Gate","Real")]
[Trait("Requirement","TEST-BUILD-004")]
[Trait("Requirement","TEST-BUILD-007")]
public sealed class CatalogIntakeHistoricalAuditRealTests
{
    [Fact] public Task NativeTypedIntakeRetainsDeclaredUnverifiedHistoricalAudit()=>CatalogIntakeHistoricalAuditCases.AssertCase(new NativeCatalogIntakeHistoricalAudit());
}
