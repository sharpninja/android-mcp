using System.Text;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public static class StandaloneNarrowReceiptCases
{
    public static IEnumerable<object[]> Cases=>DesignNarrowStageRecipeCases.Cases
        .Where(row=>new[]{"validateModuleGraph","generateUiAssets","renderStoryboards","UNION-CODE-MODULE","UNION-CODE-UI",
            "verifyToolchain","extractCodeViews","validateModuleGraph-module","generateUiAssets-ui","extractCodeViews-cancel"}
            .Contains(((DesignNarrowRecipeCase)row[0]).Id,StringComparer.Ordinal));

    public static IReadOnlyList<Artifact> ExpectedReceipts(DesignNarrowRecipeEnvironment e)
    {
        var result=new List<Artifact>();
        if(e.Has(DesignStage.ModuleGraph))result.Add(StandaloneReceiptFixtures.Module(e.Original.Model.Modules));
        if(e.Has(DesignStage.UiAssets))result.Add(StandaloneReceiptFixtures.Canonical(LegacyReceiptCases.Original(LegacyReceiptKind.Ui)));
        return result;
    }

    public static Func<DesignArchitectureRequest,DesignStagePlan,ArtifactGenerationRecipe> Scripted(DesignNarrowRecipeEnvironment e,string mutation="")
        =>(request,selection)=>
        {
            var recipe=new ScriptedNarrowStageRecipeFactory(e).Architecture(request,selection);
            return new(recipe.Sources,inputs=>
            {
                var prepared=recipe.Prepare(inputs);
                var paths=mutation=="declared-omit"?prepared.OutputPaths.Where(path=>!path.StartsWith("build/receipts/",StringComparison.Ordinal)).ToArray():prepared.OutputPaths;
                return new(paths,async token=>
                {
                    var outputs=(await prepared.MaterializeAsync(token)).ToArray();
                    var receipt=Array.FindIndex(outputs,artifact=>artifact.RelativePath.StartsWith("build/receipts/",StringComparison.Ordinal));
                    if(receipt>=0)
                    {
                        if(mutation=="omit")return outputs.Where((_,index)=>index!=receipt).ToArray();
                        if(mutation=="path")outputs[receipt]=outputs[receipt] with{RelativePath="validation/wrong.json"};
                        if(mutation=="media")outputs[receipt]=outputs[receipt] with{MediaType="text/plain"};
                        if(mutation=="bytes")outputs[receipt]=outputs[receipt] with{Bytes=Encoding.UTF8.GetBytes("{}\n")};
                    }
                    if(mutation=="extra")outputs=outputs.Append(new Artifact("build/receipts/model-contracts.json",Encoding.UTF8.GetBytes("{}\n"),"application/json")).ToArray();
                    return outputs;
                },prepared.VerifyBeforeCommitAsync);
            });
        };
}

public static class StandaloneReceiptFixtures
{
    public static Artifact Canonical(Artifact fixture)=>fixture with
        {Bytes=Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(new CanonicalJson().Serialize(fixture.Bytes))+"\n")};
    public static Artifact Module(ModuleValidationResult module)=>Canonical(LegacyReceiptCases.Json(LegacyReceiptCases.Original(LegacyReceiptKind.Module),value=>
    {
        value["class_count"]=module.ClassCount;
        value["module_edges"]=new JsonArray(module.ModuleEdges.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)
            .Select(edge=>(JsonNode?)JsonValue.Create(edge)).ToArray());
    }));
}

[Trait("Gate","Mock")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class StandaloneNarrowReceiptMockTests
{
    [Theory,MemberData(nameof(StandaloneNarrowReceiptCases.Cases),MemberType=typeof(StandaloneNarrowReceiptCases))]
    public Task CorrectSameNarrowScopeAndReceiptCasesPass(DesignNarrowRecipeCase test)
    {var e=new DesignNarrowRecipeEnvironment(test,true);return DesignNarrowStageRecipeCases.AssertRecipe(e,StandaloneNarrowReceiptCases.Scripted(e));}

    [Theory]
    [InlineData("validateModuleGraph","declared-omit")][InlineData("validateModuleGraph","omit")]
    [InlineData("validateModuleGraph","path")][InlineData("validateModuleGraph","media")][InlineData("validateModuleGraph","bytes")]
    [InlineData("generateUiAssets","omit")][InlineData("generateUiAssets","path")][InlineData("generateUiAssets","media")]
    [InlineData("generateUiAssets","bytes")][InlineData("generateUiAssets","extra")][InlineData("extractCodeViews","extra")]
    public async Task EachMissingAlteredUnselectedAndUndeclaredReceiptIsRejected(string id,string mutation)
    {
        var test=StandaloneNarrowReceiptCases.Cases.Select(row=>(DesignNarrowRecipeCase)row[0]).Single(test=>test.Id==id);
        var e=new DesignNarrowRecipeEnvironment(test,true);
        Assert.NotNull(await Record.ExceptionAsync(()=>DesignNarrowStageRecipeCases.AssertRecipe(e,StandaloneNarrowReceiptCases.Scripted(e,mutation))));
    }
}
