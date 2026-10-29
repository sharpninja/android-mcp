using System.Text;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public static class StandaloneModelReceiptCases
{
    public static IEnumerable<object[]> Cases=>FullArchitectureRecipeCases.Cases.Where(row=>(string)row[0]!="PENDING");
    public static IReadOnlyList<Artifact> ExpectedReceipts(FullArchitectureRecipeEnvironment e)
    {
        var result=new List<Artifact>();
        if(e.Selection.Stages.Contains(DesignStage.ModuleGraph))
            result.Add(StandaloneReceiptFixtures.Module(e.Model.Modules));
        result.Add(LegacyReceiptCases.Json(LegacyReceiptCases.Original(LegacyReceiptKind.Model),value=>
        {value["validated"]=e.Model.Validated.Validated;value["scope"]=e.Model.Validated.Scope;value["screens"]=e.Model.Validated.Screens;value["states"]=e.Model.Validated.States;}));
        result.Add(LegacyReceiptCases.Original(LegacyReceiptKind.Ui));
        return result.Select(StandaloneReceiptFixtures.Canonical).ToArray();
    }
}

public sealed class ScriptedStandaloneModelReceiptFactory(FullArchitectureRecipeEnvironment e,string mutation=""):IDesignFullArchitectureRecipeFactory
{
    public ArtifactGenerationRecipe Architecture(DesignArchitectureRequest request,DesignStagePlan selection,DocumentationModelSourceSnapshot snapshot,IDocumentationSourceCollection sourceCollection)
    {
        var recipe=new ScriptedFullArchitectureRecipeFactory(e).Architecture(request,selection,snapshot,sourceCollection);
        return new(recipe.Sources,inputs=>
        {
            var prepared=recipe.Prepare(inputs);
            var paths=mutation=="declare"?prepared.OutputPaths.Where(path=>!path.StartsWith("build/receipts/",StringComparison.Ordinal)).ToArray():prepared.OutputPaths;
            return new(paths,async token=>
            {
                var outputs=(await prepared.MaterializeAsync(token)).ToArray();
                var index=Array.FindIndex(outputs,artifact=>artifact.RelativePath=="build/receipts/"+mutation);
                if(index>=0)return outputs.Where((_,position)=>position!=index).ToArray();
                var model=Array.FindIndex(outputs,artifact=>artifact.RelativePath=="build/receipts/model-contracts.json");
                if(mutation=="media")outputs[model]=outputs[model] with{MediaType="text/plain"};
                if(mutation=="bytes")outputs[model]=outputs[model] with{Bytes=Encoding.UTF8.GetBytes("{}\n")};
                if(mutation=="extra")outputs=outputs.Append(new Artifact("build/receipts/unselected.json",Encoding.UTF8.GetBytes("{}\n"),"application/json")).ToArray();
                return outputs;
            },prepared.VerifyBeforeCommitAsync);
        });
    }
    public ArtifactGenerationRecipe Architecture(DesignArchitectureRequest request)=>throw new InvalidOperationException("Only captured stage overload belongs to this case");
    public ArtifactGenerationRecipe Package(DesignPackageRequest request)=>throw new InvalidOperationException("Package cases retain their existing provider");
}

[Trait("Gate","Mock")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class StandaloneModelReceiptMockTests
{
    [Theory,MemberData(nameof(StandaloneModelReceiptCases.Cases),MemberType=typeof(StandaloneModelReceiptCases))]
    public Task CorrectSameCapturedModelAndReceiptCasesPass(string scenario)
    {var e=new FullArchitectureRecipeEnvironment(scenario,true);return FullArchitectureRecipeCases.AssertRecipe(e,new ScriptedStandaloneModelReceiptFactory(e));}
    [Theory]
    [InlineData("FULL","module-graph.json")][InlineData("FULL","model-contracts.json")][InlineData("FULL","ui-projection.json")]
    [InlineData("MODEL-ONLY","model-contracts.json")][InlineData("MODEL-ONLY","ui-projection.json")]
    [InlineData("FULL","declare")][InlineData("FULL","media")][InlineData("FULL","bytes")][InlineData("MODEL-ONLY","extra")]
    public async Task EachMissingAlteredUndeclaredAndUnselectedReceiptIsRejected(string scenario,string mutation)
    {var e=new FullArchitectureRecipeEnvironment(scenario,true);Assert.NotNull(await Record.ExceptionAsync(()=>FullArchitectureRecipeCases.AssertRecipe(e,new ScriptedStandaloneModelReceiptFactory(e,mutation))));}
}
