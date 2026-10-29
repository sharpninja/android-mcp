using System.Text;
using System.Text.Json;
using AndroidMcp.BuildTool.Catalog;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed class NativeContractProvider:IContractCaseProvider
{
    private readonly CanonicalJson canonical=new();
    public ContractReply Execute(ContractCall call)
    {
        var service=new CatalogContracts(canonical);var entry=JsonSerializer.SerializeToElement(call.Entry);
        if(call.Operation=="kind")
        {
            try {return new("ok",Kind:service.EntityKind(entry));}
            catch(ArgumentException) {return new("invalid");}
        }
        var body=service.Project(entry);return new("ok",SchemaHash:service.Digest(entry),Contract:Encoding.UTF8.GetString(canonical.Serialize(body)));
    }
}
[Trait("Gate","Real")]
public sealed class CatalogContractRealTests
{
    [Theory,MemberData(nameof(CatalogContractCases.All),MemberType=typeof(CatalogContractCases))]
    public void NativeContractUsesSameCasesAndAssertions(ContractCase test)=>CatalogContractCases.AssertCase(test,new NativeContractProvider());
}
