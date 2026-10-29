using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public static class ModuleDeclaredClassCases
{
    public static string[] Expected(ModuleGraphCase test)
    {
        if(test.Id=="MOD-ORIGINAL-FULL-GRAPH")
        {
            var fixture=DocumentationFixture.Current;var data=JsonNode.Parse(File.ReadAllBytes(Path.Combine(fixture.Workspace,"remediation/nuke-planning/docs/fixtures/model-contract-type-index.fixture.json")))!;
            Assert.Equal(DocumentationFixture.CurrentSourceHash("remediation/design-c4/architecture/code-diagrams.md",data["codeSha256"]!.GetValue<string>()),fixture.ExpectedHashes["remediation/design-c4/architecture/code-diagrams.md"]);
            var names=data["declaredClasses"]!.AsArray().Select(n=>n!.GetValue<string>()).ToArray();Assert.Equal(96,names.Length);return names;
        }
        return test.Id switch
        {
            "MOD-VALID-PORT"=>["Repository","CatalogPort"],"MOD-ORIGINAL-CYCLE"=>["DeviceViewResolver","CallbackBroker"],
            "MOD-ORIGINAL-FORBIDDEN"=>["Repository","OwnerAuthenticationService"],"MOD-ORIGINAL-DOMAIN-PORT"=>["LoopbackHost"],
            "MOD-MISSING-OWNER"=>["Unknown"],"MOD-PER-VIEW-ENDPOINT"=>["A"],"MOD-DEPENDENCY-OWNER"=>["A","B"],
            "MOD-UNUSED-CYCLE"=>[],"MOD-EXTERNAL-EDGE"=>["App","Remote"],_=>throw new InvalidOperationException("Unassigned legacy module case")
        };
    }
    public static void AssertContract(ModuleGraphCase test,IModuleGraphValidator provider)
    {
        ModuleGraphContractCases.AssertContract(test,provider);
        var before=test.Input.ModuleContract.ToJsonString();var result=provider.Validate(test.Input);Assert.Equal(before,test.Input.ModuleContract.ToJsonString());
        Assert.NotNull(result.DeclaredClasses);Assert.Equal(Expected(test),result.DeclaredClasses);Assert.Equal(result.ClassCount,result.DeclaredClasses.Count);
        Assert.Equal(result.DeclaredClasses.Count,result.DeclaredClasses.Distinct(StringComparer.Ordinal).Count());
    }
    public static ModuleValidationResult Response(ModuleGraphCase test)=>ModuleGraphContractCases.Correct(test) with{DeclaredClasses=Expected(test)};
}
[Trait("Gate","Mock")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class ModuleDeclaredClassesMockTests
{
    [Theory,MemberData(nameof(ModuleGraphContractCases.Cases),MemberType=typeof(ModuleGraphContractCases))]
    public void CorrectDeclaredClassMetadataMocksPreserveEveryLegacyCase(ModuleGraphCase test)=>ModuleDeclaredClassCases.AssertContract(test,new ScriptedModuleGraphValidator(ModuleDeclaredClassCases.Response(test)));
    [Theory,MemberData(nameof(ModuleGraphContractCases.Cases),MemberType=typeof(ModuleGraphContractCases))]
    public void MissingAuthorityMetadataIsRejectedForEveryLegacyCase(ModuleGraphCase test)=>Assert.NotNull(Record.Exception(()=>ModuleDeclaredClassCases.AssertContract(test,new ScriptedModuleGraphValidator(ModuleDeclaredClassCases.Response(test) with{DeclaredClasses=null}))));
    [Theory]
    [InlineData("missing")][InlineData("extra")][InlineData("wrong")][InlineData("order")][InlineData("duplicate")]
    public void EachDeclaredClassDriftIsRejected(string mutation)
    {
        var test=ModuleGraphContractCases.Create().Single(test=>test.Id=="MOD-ORIGINAL-FULL-GRAPH");var expected=ModuleDeclaredClassCases.Expected(test);
        var wrong=mutation switch{"missing"=>expected.Skip(1).ToArray(),"extra"=>expected.Append("Unmodeled").ToArray(),"wrong"=>new[]{"Wrong"}.Concat(expected.Skip(1)).ToArray(),"order"=>expected.Reverse().ToArray(),_=>expected.Append(expected[0]).ToArray()};
        Assert.NotNull(Record.Exception(()=>ModuleDeclaredClassCases.AssertContract(test,new ScriptedModuleGraphValidator(ModuleDeclaredClassCases.Response(test) with{DeclaredClasses=wrong}))));
    }
    [Fact]
    public void ExistingThreeArgumentConstructionRetainsOptionalMetadataDefault()=>Assert.Null(new ModuleValidationResult(0,[],[]).DeclaredClasses);
}
