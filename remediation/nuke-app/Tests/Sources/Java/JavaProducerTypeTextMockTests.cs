using System.Text.Json.Nodes;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources.Java;

[Trait("Gate","Mock"),Trait("Requirement","TEST-BUILD-004")]
public sealed class JavaProducerTypeTextMockTests
{
    [Fact]
    public void ExplicitJavaLangSubpackageImportPassesOriginalTypeTextClause()
    {
        var test=JavaProducerTypeTextCases.Case;
        JavaContractCases.AssertContract(test,new ScriptedSourceExtractor("java-source",_=>test.Correct()));
    }
    [Fact]
    public void SkippedReturnParameterOrCanonicalImportReplacementCannotPass()
    {
        var test=JavaProducerTypeTextCases.Case;
        foreach(var field in new[]{"return_type","parameter_type","canonical"})
        {
            var rows=test.Correct().Records.Select(row=>(JsonObject)row.DeepClone()).ToArray();var method=rows[2];
            if(field=="parameter_type")method["parameters"]![0]!["type"]="Retention";
            else method[field]=field=="canonical"?"a.C#f(Retention)":"Retention";
            Assert.NotNull(Record.Exception(()=>JavaContractCases.AssertContract(test,new ScriptedSourceExtractor("java-source",_=>test.Correct() with{Records=rows}))));
        }
    }
}
