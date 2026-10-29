using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources.Java;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources.Java;

public sealed record JavaOriginalLiteralCase(string Id,string OriginalFunction,string? AstId,string? ContextId,string[] Controls);
public sealed record JavaOriginalLiteralRig(ISourceExtractor? Ast,IJavaFileContext? Context);

// Six previously indirect literal assertions only. Existing input bytes,
// source/context fixtures and the shared subset comparer remain unchanged.
public static class JavaOriginalLiteralAssertionCases
{
    public static readonly IReadOnlyList<JavaOriginalLiteralCase> All=[
        new("ORIG-JAVA-OWNER","test_nested_overloads_fields_and_annotations","JAVA-01",null,["remove-inner-owners"]),
        new("ORIG-JAVA-FIELD-OWNERS","test_nested_overloads_fields_and_annotations","JAVA-01",null,["wrong-P-owner","wrong-Q-owner","nested-field-owner"]),
        new("ORIG-JAVA-PARSE-ZERO","test_nested_overloads_fields_and_annotations","JAVA-01",null,["count-one","count-absent","count-string"]),
        new("ORIG-JAVA-PARSE-POSITIVE","test_parse_error_retained","JAVA-04",null,["count-zero","count-negative","count-absent","count-string"]),
        new("ORIG-JAVA-ENUM-SET","test_enum_and_multiple_array_declarators","JAVA-05",null,["enum-a","enum-b"]),
        new("ORIG-CONTEXT-IMPORTS-EXACT","test_imports_retained_in_runtime_source_details",null,"CTX-imports",["extra-import"])
    ];
    public static IEnumerable<object[]> Cases=>All.Select(test=>new object[]{test.Id});
    public static JavaOriginalLiteralCase Get(string id)=>All.Single(test=>test.Id==id);
    public static void AssertExistingContract(JavaOriginalLiteralCase test,JavaOriginalLiteralRig rig)
    {
        if(test.AstId is {} ast)JavaContractCases.AssertContract(JavaContractCases.Get(ast),rig.Ast!);
        else JavaContextCases.AssertContract(JavaContextCases.Get(test.ContextId!),rig.Context!);
    }
    public static void AssertContract(JavaOriginalLiteralCase test,JavaOriginalLiteralRig rig)
    {
        AssertExistingContract(test,rig);
        if(test.ContextId is {} context)
        {
            var input=JavaContextCases.Get(context);var sources=input.Sources();var reports=input.Reports();var before=reports.ToJsonString();
            rig.Context!.Attach(sources,reports);
            Assert.True(JsonNode.DeepEquals(JavaContractCases.Object(("List","java.util.List")),sources[0]!["java_file_context"]!["imports"]));
            Assert.True(JsonNode.DeepEquals(JavaContractCases.Array(JsonValue.Create("other")),sources[0]!["java_file_context"]!["wildcard_imports"]));
            Assert.Equal(before,reports.ToJsonString());return;
        }
        var source=JavaContractCases.Get(test.AstId!).Input();var original=source.Bytes.ToArray();var result=rig.Ast!.Extract(source);
        Assert.Equal(original,source.Bytes.ToArray());
        switch(test.Id)
        {
            case "ORIG-JAVA-OWNER":Assert.Contains("a.Outer.Inner",result.Records.Select(row=>row["owner"]?.GetValue<string>()));break;
            case "ORIG-JAVA-FIELD-OWNERS":
                var names=result.Records.Where(row=>row["kind"]?.GetValue<string>()=="field"&&row["owner"]?.GetValue<string>()=="a.Outer").Select(row=>row["name"]!.GetValue<string>()).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);
                Assert.Equal(new[]{"P","Q"},names);break;
            case "ORIG-JAVA-PARSE-ZERO":Assert.Equal(0,result.Metadata!["parse_errors"]!.GetValue<int>());break;
            case "ORIG-JAVA-PARSE-POSITIVE":Assert.True(result.Metadata!["parse_errors"]!.GetValue<int>()>0);break;
            case "ORIG-JAVA-ENUM-SET":
                var constants=result.Records.Where(row=>row["extras"]?["enum_constant"]?.GetValue<bool>()==true).Select(row=>row["name"]!.GetValue<string>()).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);
                Assert.Equal(new[]{"BLUE","RED"},constants);break;
            default:throw new InvalidOperationException("Unknown literal assertion "+test.Id);
        }
    }
}
