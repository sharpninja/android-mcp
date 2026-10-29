using System.Text.Json.Nodes;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

[Trait("Gate","Mock"),Trait("Requirement","TEST-BUILD-004")]
public sealed class CatalogOriginalAssertionGapMockTests
{
    [Fact]
    public void ExactOriginalGroupedRowAndCountAcceptIndependentMock()=>
        CatalogOriginalAssertionGapCases.AssertGrouped(CatalogOriginalAssertionGapCases.GroupCase.Expected,[("fixture-binding",2L)]);
    [Fact]
    public void GroupCardinalityCountAndEveryExistingBuildFieldAreDiscriminated()
    {
        var test=CatalogOriginalAssertionGapCases.GroupCase;
        foreach(var rows in new (string,long)[][]{[],[("first",1L),("second",1L)],[("first",2L),("second",2L)],[("first",1L)],[("first",3L)]})
            Assert.NotNull(Record.Exception(()=>CatalogOriginalAssertionGapCases.AssertGrouped(test.Expected,rows)));
        for(var wrong=0;wrong<24;wrong++)
        {
            using var provider=new ScriptedCatalogBuild(test,wrong);
            var reply=provider.Build(test.Call);
            Assert.NotNull(Record.Exception(()=>CatalogOriginalAssertionGapCases.AssertGrouped(reply,[("fixture-binding",2L)])));
        }
    }
    [Fact]
    public Task ExactOriginalCompleteExtensionObjectsAcceptIndependentMock()=>
        CatalogOriginalAssertionGapCases.AssertExtensions(CatalogOriginalAssertionGapCases.SdkCase.Expected,CatalogOriginalAssertionGapCases.ExpectedExtensions());
    [Fact]
    public async Task ExtraKeysOrderMissingRowsWrongTypesAndExistingPipelineFactsAreDiscriminated()
    {
        var test=CatalogOriginalAssertionGapCases.SdkCase;
        var expected=CatalogOriginalAssertionGapCases.ExpectedExtensions();
        var wrongArrays=new List<JsonArray>{new(),new((JsonNode)expected[0]!.DeepClone()),new(expected[1]!.DeepClone(),expected[0]!.DeepClone()),new(expected[0]!.DeepClone(),expected[1]!.DeepClone(),expected[1]!.DeepClone())};
        var extra=CatalogOriginalAssertionGapCases.ExpectedExtensions();extra[0]!["unexpected"]=true;wrongArrays.Add(extra);
        var missing=CatalogOriginalAssertionGapCases.ExpectedExtensions();missing[0]!.AsObject().Remove("minimum_version");wrongArrays.Add(missing);
        var typed=CatalogOriginalAssertionGapCases.ExpectedExtensions();typed[0]!["minimum_version"]=4;wrongArrays.Add(typed);
        foreach(var index in new[]{0,1})foreach(var key in new[]{"sdk_id","minimum_version"})
        {var wrong=CatalogOriginalAssertionGapCases.ExpectedExtensions();wrong[index]![key]="wrong";wrongArrays.Add(wrong);}
        foreach(var wrong in wrongArrays)
            Assert.NotNull(await Record.ExceptionAsync(()=>CatalogOriginalAssertionGapCases.AssertExtensions(test.Expected,wrong)));
        foreach(var key in test.Expected.Facts.Keys)
        {
            foreach(var omit in new[]{false,true})
            {
                var reply=await new ScriptedCatalogDevicePipeline(test,key,omit).RunAsync(test.Call);
                Assert.NotNull(await Record.ExceptionAsync(()=>CatalogOriginalAssertionGapCases.AssertExtensions(reply,expected)));
            }
        }
        foreach(var provider in new[]{new ScriptedCatalogDevicePipeline(test,wrongOutcome:true),new ScriptedCatalogDevicePipeline(test,discardInput:true)})
        {var reply=await provider.RunAsync(test.Call);Assert.NotNull(await Record.ExceptionAsync(()=>CatalogOriginalAssertionGapCases.AssertExtensions(reply,expected)));}
    }
}
