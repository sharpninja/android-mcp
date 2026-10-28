using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources.Java;

public static class JavaOriginalLiteralMocks
{
    public static JavaOriginalLiteralRig Create(JavaOriginalLiteralCase test,string? mutation=null)
    {
        if(test.ContextId is not null)return new(null,new JavaScriptedContext((sources,reports)=>
        {
            var context=(JsonObject)reports[0]!.DeepClone();context.Remove("cache_path");context.Remove("cache_sha256");context.Remove("parser_sha256");
            context["excluded_implementation_nodes"]=null;
            context["static_import_resolution"]="Static/type imports not separately classified; original source retained. No unresolved wildcard/type-variable identity inferred.";
            if(mutation=="extra-import")context["imports"]!["Other"]="extra.Type";
            sources[0]!["java_file_context"]=context;
        }));
        var fixture=JavaContractCases.Get(test.AstId!);
        return new(new ScriptedSourceExtractor("java-source",source=>
        {
            Assert.Equal(fixture.Input().RelativePath,source.RelativePath);Assert.Equal(fixture.Input().Bytes.ToArray(),source.Bytes.ToArray());Assert.Equal(fixture.Input().Context,source.Context);
            var result=fixture.Correct();result.Metadata!["parse_errors"]=fixture.HasErrors?1:0;
            if(fixture.Id=="JAVA-01")
            {
                // Authored owners of the exact eleven original records. This
                // adds expected data to scripted facts, not an owner algorithm.
                string?[] owners=[null,"a.Outer","a.Outer","a.Outer","a.Outer","a.Outer","a.Outer","a.Outer.Inner","a.Outer.Inner","a.Outer.A","a.Outer.A"];
                Assert.Equal(owners.Length,result.Records.Count);for(var i=0;i<owners.Length;i++)result.Records[i]["owner"]=owners[i];
            }
            switch(mutation)
            {
                case "remove-inner-owners":foreach(var row in result.Records.Where(row=>row["owner"]?.GetValue<string>()=="a.Outer.Inner"))row["owner"]="wrong";break;
                case "wrong-P-owner":case "wrong-Q-owner":Field(result,mutation=="wrong-P-owner"?"P":"Q")["owner"]="wrong";break;
                case "nested-field-owner":Field(result,"value")["owner"]="a.Outer";break;
                case "count-one":result.Metadata!["parse_errors"]=1;break;
                case "count-zero":result.Metadata!["parse_errors"]=0;break;
                case "count-negative":result.Metadata!["parse_errors"]=-1;break;
                case "count-absent":result.Metadata!.Remove("parse_errors");break;
                case "count-string":result.Metadata!["parse_errors"]="wrong";break;
                case "enum-a":case "enum-b":Field(result,mutation=="enum-a"?"a":"b")["extras"]!["enum_constant"]=true;break;
            }
            return result;
        }),null);
    }
    private static JsonObject Field(ExtractionResult result,string name)=>result.Records.Single(row=>row["kind"]?.GetValue<string>()=="field"&&row["name"]?.GetValue<string>()==name);
}

[Trait("Gate","Mock"),Trait("Requirement","TEST-BUILD-004")]
public sealed class JavaOriginalLiteralAssertionMockTests
{
    public static IEnumerable<object[]> Cases=>JavaOriginalLiteralAssertionCases.Cases;
    [Theory,MemberData(nameof(Cases))]
    public void SixOriginalLiteralAssertionsPassCorrectExistingFixtureMocks(string id)
    {var test=JavaOriginalLiteralAssertionCases.Get(id);JavaOriginalLiteralAssertionCases.AssertContract(test,JavaOriginalLiteralMocks.Create(test));}
    [Theory,MemberData(nameof(Cases))]
    public void PreviouslyUndetectedMutationsPassOldSubsetButFailExactOriginalAssertion(string id)
    {
        var test=JavaOriginalLiteralAssertionCases.Get(id);
        foreach(var control in test.Controls)
        {
            JavaOriginalLiteralAssertionCases.AssertExistingContract(test,JavaOriginalLiteralMocks.Create(test,control));
            Assert.NotNull(Record.Exception(()=>JavaOriginalLiteralAssertionCases.AssertContract(test,JavaOriginalLiteralMocks.Create(test,control))));
        }
    }
}
