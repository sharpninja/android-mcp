using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public sealed record BootstrapCarryObservation(bool Declared,bool SameReceipt,bool Validated,bool Covered,bool Packaged,bool DefaultsStable);
public static class BootstrapCarryCases
{
    public static readonly string[] Names=["NARROW","FULL","VERIFY","MANIFEST","PACKAGE"];
    public static IEnumerable<object[]> Cases=>Names.Select(id=>new object[]{id});
    public static readonly Artifact Receipt=new("toolchain-bootstrap-receipt.json","{\"original\":\"retained receipt\"}\n"u8.ToArray(),"application/json");
    public static BootstrapCarryObservation Correct(string id)=>new(true,true,id is "VERIFY" or "MANIFEST" or "PACKAGE",id is "MANIFEST" or "PACKAGE",id=="PACKAGE",true);
    public static async Task AssertContract(string id,Func<string,Task<BootstrapCarryObservation>> probe)
    {var expected=Correct(id);var actual=await probe(id);Assert.Equal(expected.Declared,actual.Declared);Assert.Equal(expected.SameReceipt,actual.SameReceipt);Assert.Equal(expected.Validated,actual.Validated);Assert.Equal(expected.Covered,actual.Covered);Assert.Equal(expected.Packaged,actual.Packaged);Assert.True(actual.DefaultsStable);}
    public static BootstrapCarryObservation Wrong(string id,string mutation)
    {var x=Correct(id);return mutation switch{"declared"=>x with{Declared=false},"same"=>x with{SameReceipt=false},"validated"=>x with{Validated=!x.Validated},"covered"=>x with{Covered=!x.Covered},"packaged"=>x with{Packaged=!x.Packaged},_=>x with{DefaultsStable=false}};}
    public static IEnumerable<object[]> Faults=>Names.SelectMany(id=>new[]{"declared","same","validated","covered","packaged","defaults"}.Select(m=>new object[]{id,m}));
}
[Trait("Gate","Mock"),Trait("Requirement","FR-BUILD-005"),Trait("Requirement","TEST-BUILD-005")]
public sealed class DocumentationBootstrapCarryMockTests
{
    [Theory,MemberData(nameof(BootstrapCarryCases.Cases),MemberType=typeof(BootstrapCarryCases))]
    public Task OriginalCommittedReceiptCarryMocksPass(string id)=>BootstrapCarryCases.AssertContract(id,x=>Task.FromResult(BootstrapCarryCases.Correct(x)));
    [Theory,MemberData(nameof(BootstrapCarryCases.Faults),MemberType=typeof(BootstrapCarryCases))]
    public async Task EveryReceiptPathIdentityValidationCoverageAndArchiveFaultIsRejected(string id,string mutation)=>Assert.NotNull(await Record.ExceptionAsync(()=>BootstrapCarryCases.AssertContract(id,x=>Task.FromResult(BootstrapCarryCases.Wrong(x,mutation)))));
}
