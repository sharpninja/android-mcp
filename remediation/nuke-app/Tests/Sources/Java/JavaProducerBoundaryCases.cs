using System.Text;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using Xunit;
using static AndroidMcp.BuildTool.Tests.Sources.Java.JavaContractCases;

namespace AndroidMcp.BuildTool.Tests.Sources.Java;

// FR-BUILD-004 / TR-BUILD-TOOL-004 / TEST-BUILD-004.
// Expected boundaries come from retained java_source.py, not visitor output.
public sealed record JavaProducerBoundaryCase(string Id,string Text,JsonObject[] Rows,JsonObject Metadata,SourceIssue[] Issues)
{
    public SourceDocument Input()=>new(Id+".java",Encoding.UTF8.GetBytes(Text),new("fixture","pinned","java","37.2"));
    public ExtractionResult Correct()=>new(Rows.Select(row=>(JsonObject)row.DeepClone()).ToArray(),Issues,
        Issues.Length==0?StructuralParseStatus.Clean:StructuralParseStatus.Malformed,(JsonObject)Metadata.DeepClone());
}

public static class JavaProducerBoundaryCases
{
    private static JsonObject Metadata(JsonObject? excluded=null,JsonArray? issues=null)=>Object(("package",""),("imports",new JsonObject()),
        ("wildcard_imports",new JsonArray()),("excluded_implementation_nodes",excluded??new()),("parse_errors",issues?.Count??0),("issues",issues??new()));
    private static JsonObject TypeRow(string owner,string name)=>Object(("kind","class"),("canonical",owner),("owner",owner),("package",""),("name",name),
        ("extras",Object(("owner_binary_name",owner.Replace('.','$')),("declaration_kind","class_declaration"))));
    private static string ScalarText(int length)=>string.Concat(Enumerable.Repeat("😀",length/2))+new string('x',length-length/2);
    private static JavaProducerBoundaryCase Field(int length,bool signature)
    {
        var initializer="\""+ScalarText(signature?length-14:length-2)+"\"";
        var declaration="String p = "+initializer+";";
        Assert.Equal(length,(signature?declaration:initializer).EnumerateRunes().Count());
        var text="class C { "+declaration+" }";const int start=10;
        var valueStart=start+11;var valueEnd=valueStart+Encoding.UTF8.GetByteCount(initializer);
        var extra=Object(("constant_raw",initializer.EnumerateRunes().Count()<=4096?initializer:null),("initializer_source_range",new JsonArray(valueStart,valueEnd)),("enum_constant",false),
            ("source_byte_range",new JsonArray(start,start+Encoding.UTF8.GetByteCount(declaration))));
        if(signature&&length>16384)extra["raw_signature_is_excerpt"]=true;
        var row=Object(("kind","field"),("canonical","C#p"),("owner","C"),("name","p"),("return_type","String"),
            ("raw",signature&&length>16384?"String p [initializer retained at source byte range]":declaration),("extras",extra));
        return new("JAVA-BOUNDARY-"+(signature?"signature-":"constant-")+length,text,[TypeRow("C","C"),row],Metadata(),[]);
    }
    private static JavaProducerBoundaryCase Depth(int count)
    {
        var builder=new StringBuilder();var starts=new List<int>();
        for(var index=0;index<count;index++){starts.Add(builder.Length);builder.Append("class N").Append(index).Append(" { ");}
        builder.Append("int leaf;");builder.Append('}',count);var text=builder.ToString();var rows=new List<JsonObject>();var owner="";
        for(var index=0;index<Math.Min(count,129);index++){owner+=(index==0?"":".")+"N"+index;rows.Add(TypeRow(owner,"N"+index));}
        if(count==129)
        {
            rows.Add(Object(("kind","field"),("canonical",owner+"#leaf"),("owner",owner),("name","leaf"),("return_type","int")));
            return new("JAVA-BOUNDARY-depth128",text,rows.ToArray(),Metadata(),[]);
        }
        var start=starts[129];var end=text.Length-129;var range=new JsonArray(start,end);const string reason="Nested type depth exceeds128";
        var detail=Object(("reason",reason),("line",1),("byte_range",range));
        rows.Add(Object(("kind","unsupported"),("canonical","unparsed-java:JAVA-BOUNDARY-depth129.java:["+start+", "+end+"]:"+ContentHash.Sha256([])),
            ("owner",null),("name","unparsed"),("raw",""),("line",1),("extras",Object(("parse_error",reason),("source_byte_range",range)))));
        return new("JAVA-BOUNDARY-depth129",text,rows.ToArray(),Metadata(issues:Array(detail)),[new("java-ast-error",reason,start,end)]);
    }
    private static JavaProducerBoundaryCase Exclusions()=>new("JAVA-BOUNDARY-exclusions",
        "class C { static {} {} void f(){ class Local {} } Object x=new Object(){}; }",
        [TypeRow("C","C"),Object(("kind","method"),("canonical","C#f()"),("name","f")),Object(("kind","field"),("canonical","C#x"),("name","x"))],
        Metadata(Object(("static_initializer",1),("block",1))),[]);
    private static JavaProducerBoundaryCase Provenance()
    {
        const string annotation="@RequiresPermission(value=Permissions.CAMERA,conditional=ENABLED)";
        var item=Object(("name","android.annotation.RequiresPermission"),("arguments","value=x.Permissions.CAMERA,conditional=ENABLED"),
            ("source_arguments","value=Permissions.CAMERA,conditional=ENABLED"),("source_spelling","RequiresPermission"),("source_raw",annotation),
            ("raw",annotation),("start",0),("end",annotation.Length),("name_resolution","known-annotation-wildcard"));
        var row=TypeRow("C","C");row["annotations"]=Array(item);row["all_annotations"]=Array(item);
        var metadata=Metadata();metadata["imports"]=Object(("Permissions","x.Permissions"),("ENABLED","x.Flags.ENABLED"));
        metadata["wildcard_imports"]=Array(JsonValue.Create("android.annotation"));
        return new("JAVA-BOUNDARY-provenance","import x.Permissions; import static x.Flags.ENABLED; import android.annotation.*; "+annotation+" class C {}",[row],metadata,[]);
    }
    public static readonly IReadOnlyList<JavaProducerBoundaryCase> All=[Field(4096,false),Field(4097,false),Field(16384,true),Field(16385,true),Depth(129),Depth(130),Exclusions(),Provenance()];
    public static IEnumerable<object[]> Cases=>All.Select(test=>new object[]{test.Id});
    public static JavaProducerBoundaryCase Find(string id)=>All.Single(test=>test.Id==id);
    public static void AssertContract(JavaProducerBoundaryCase test,ISourceExtractor provider)
    {
        Assert.Equal("java-source",provider.Kind);var input=test.Input();var before=input.Bytes.ToArray();var result=provider.Extract(input);
        Assert.Equal(before,input.Bytes.ToArray());Assert.Equal(test.Issues.Length==0?StructuralParseStatus.Clean:StructuralParseStatus.Malformed,result.StructuralStatus);
        Assert.Equal(test.Issues,result.Issues);Assert.Equal(test.Rows.Length,result.Records.Count);
        for(var index=0;index<test.Rows.Length;index++)SourceExtractionContractCases.AssertSubset(test.Rows[index],result.Records[index]);
        SourceExtractionContractCases.AssertSubset(test.Metadata,result.Metadata);
        foreach(var key in new[]{"imports","wildcard_imports","excluded_implementation_nodes","issues"})
            Assert.True(JsonNode.DeepEquals(test.Metadata[key],result.Metadata![key]),"Exact metadata field "+key);
        if(test.Id is "JAVA-BOUNDARY-constant-4096" or "JAVA-BOUNDARY-constant-4097" or "JAVA-BOUNDARY-signature-16384")
            Assert.False(result.Records[1]["extras"]!.AsObject().ContainsKey("raw_signature_is_excerpt"));
    }
    public static IEnumerable<JsonNode> MissingExpectedLeaves(JsonNode expected)
    {
        foreach(var path in LeafPaths(expected,[]))
        {
            var wrong=expected.DeepClone();JsonNode parent=wrong;
            foreach(var part in path.SkipLast(1))parent=parent is JsonArray array?array[int.Parse(part)]!:parent[part]!;
            if(parent is JsonArray list)list.RemoveAt(int.Parse(path[^1]));else parent.AsObject().Remove(path[^1]);
            yield return wrong;
        }
    }
    private static IEnumerable<string[]> LeafPaths(JsonNode? node,string[] path)
    {
        if(node is JsonObject obj&&obj.Count>0)
        {foreach(var field in obj)foreach(var leaf in LeafPaths(field.Value,[..path,field.Key]))yield return leaf;}
        else if(node is JsonArray array&&array.Count>0)
        {for(var index=0;index<array.Count;index++)foreach(var leaf in LeafPaths(array[index],[..path,index.ToString()]))yield return leaf;}
        else yield return path;
    }
}
