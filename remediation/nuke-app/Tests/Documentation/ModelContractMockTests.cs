using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public static class ModelContractAssertions
{
    private static string Snapshot(ModelContractInput input)=>JsonSerializer.Serialize(new
    {
        Code=DocumentationFixture.Hash(input.CodeMarkdown.Bytes.Span),Dsl=DocumentationFixture.Hash(input.WorkspaceDsl.Bytes.Span),Design=DocumentationFixture.Hash(input.DesignMarkdown.Bytes.Span),
        Model=input.Model.Blueprint.ToJsonString(),Module=input.ModuleContract?.ToJsonString(),input.DeclaredClasses,
        Copies=input.ContractCopies.Select(copy=>new{copy.File,copy.ExpectedSha256,copy.Snapshot.RelativePath,copy.Snapshot.MediaType,Snapshot=DocumentationFixture.Hash(copy.Snapshot.Bytes.Span),Canonical=copy.Canonical is {} canonical?DocumentationFixture.Hash(canonical.Bytes.Span):null})
    });
    public static void AssertContract(ModelContractCase test,ModelContractInput input,IModelContractValidator provider)
    {
        var before=Snapshot(input);ModelContractValidationResult? result=null;var error=Record.Exception(()=>result=provider.Validate(input));
        Assert.Equal(before,Snapshot(input));
        if(test.Errors.Count>0)
        {
            var typed=Assert.IsType<ModelContractException>(error);Assert.Equal("MODEL_CONTRACT",typed.Code);Assert.Equal(test.Errors,typed.Errors);Assert.Equal(string.Join("\n",test.Errors),typed.Message);Assert.Null(result);return;
        }
        Assert.Null(error);Assert.NotNull(result);Assert.True(result.Validated);Assert.Equal(ModelContractCases.Scope,result.Scope);Assert.Equal(11,result.Screens);Assert.Equal(76,result.States);
    }
}
public sealed class ScriptedModelContractValidator(ModelContractCase test,ModelContractInput expected,string mutation=""):IModelContractValidator
{
    public ModelContractValidationResult Validate(ModelContractInput input)
    {
        Assert.Same(expected,input);
        if(mutation=="source-mutation")input.Model.Blueprint["unexpectedMutation"]=true;
        if(mutation=="outcome")return test.Errors.Count==0?throw new ModelContractException(["Wrong failure"]):new(true,ModelContractCases.Scope,11,76);
        if(test.Errors.Count>0)
        {
            var errors=test.Errors.ToArray();
            if(mutation=="error-omitted")errors=errors.Skip(1).ToArray();if(mutation=="error-extra")errors=errors.Append("Wrong extra error").ToArray();
            if(mutation=="error-content")errors[0]="Wrong error";if(mutation=="error-order")Array.Reverse(errors);
            throw new ModelContractException(errors);
        }
        return new(mutation!="validated",mutation=="scope"?"Wrong scope":ModelContractCases.Scope,mutation=="screens"?12:11,mutation=="states"?77:76);
    }
}
[Trait("Gate","Mock")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class ModelContractMockTests
{
    [Theory,MemberData(nameof(ModelContractCases.Data),MemberType=typeof(ModelContractCases))]
    public void CorrectMocksPreserveEveryOriginalValidatorClause(ModelContractCase test)
    {var input=test.Input();ModelContractAssertions.AssertContract(test,input,new ScriptedModelContractValidator(test,input));}
    [Theory,MemberData(nameof(ModelContractCases.Data),MemberType=typeof(ModelContractCases))]
    public void EveryWrongClauseOutcomeIsRejected(ModelContractCase test)
    {var input=test.Input();Assert.NotNull(Record.Exception(()=>ModelContractAssertions.AssertContract(test,input,new ScriptedModelContractValidator(test,input,"outcome"))));}
    [Theory]
    [InlineData("validated")][InlineData("scope")][InlineData("screens")][InlineData("states")][InlineData("source-mutation")]
    public void EachSuccessFieldAndSourceMutationHasItsOwnControl(string mutation)
    {var test=ModelContractCases.Create().First();var input=test.Input();Assert.NotNull(Record.Exception(()=>ModelContractAssertions.AssertContract(test,input,new ScriptedModelContractValidator(test,input,mutation))));}
    [Theory]
    [InlineData("error-omitted")][InlineData("error-extra")][InlineData("error-content")][InlineData("error-order")][InlineData("source-mutation")]
    public void EachErrorListFieldAndSourceMutationHasItsOwnControl(string mutation)
    {var test=ModelContractCases.Create().First(test=>test.Errors.Count>1);var input=test.Input();Assert.NotNull(Record.Exception(()=>ModelContractAssertions.AssertContract(test,input,new ScriptedModelContractValidator(test,input,mutation))));}
    [Fact]
    public void OriginalFullEntityDenominatorsArePreserved()
    {
        var cases=ModelContractCases.Create().ToArray();Assert.Equal(cases.Length,cases.Select(test=>test.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(528,cases.Count(test=>test.Id.StartsWith("CONTROL-REQUIRED-",StringComparison.Ordinal)));Assert.Equal(88,cases.Count(test=>test.Id.StartsWith("CONTROL-PLACEHOLDER-",StringComparison.Ordinal)));
        Assert.Equal(18,cases.Count(test=>test.Id.StartsWith("CONTROL-FALSY-",StringComparison.Ordinal)));
        Assert.Equal(107,cases.Count(test=>test.Id.StartsWith("STORY-ANCHOR-",StringComparison.Ordinal)));Assert.Equal(22,cases.Count(test=>test.Id.StartsWith("STATE-TEMPLATE-",StringComparison.Ordinal)));
        Assert.Equal(96,cases.Count(test=>test.Id.StartsWith("C4-MISSING-",StringComparison.Ordinal)));Assert.Equal(96,cases.Count(test=>test.Id.StartsWith("C4-UNKNOWN-",StringComparison.Ordinal)));
        Assert.Equal(6,cases.Count(test=>test.Id.StartsWith("COPY-",StringComparison.Ordinal)));Assert.Equal(7,cases.Count(test=>test.Id.StartsWith("CODE-MECHANISM-",StringComparison.Ordinal)));
        Assert.Equal(8,cases.Count(test=>test.Id.StartsWith("DYNAMIC-TOKEN-",StringComparison.Ordinal)));Assert.Equal(6,cases.Count(test=>test.Id.StartsWith("DYNAMIC-ORDER-",StringComparison.Ordinal)));
        Assert.Equal(2,cases.Count(test=>test.Id.StartsWith("API-SEALED-STAGE-",StringComparison.Ordinal)));Assert.Equal(2,cases.Count(test=>test.Id.StartsWith("API-BEARER-",StringComparison.Ordinal)));
    }
}
