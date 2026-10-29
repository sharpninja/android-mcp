using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using Xunit;
using static AndroidMcp.BuildTool.Tests.Sources.Java.JavaContractCases;

namespace AndroidMcp.BuildTool.Tests.Sources.Java;

// Producer clauses, independently transcribed from both pinned java_source.py
// bodies. Existing original test-method proof did not exercise these clauses.
public static class JavaProducerDeclarationCases
{
    private static JsonObject TypeRow(string kind,string owner,string name,string binary,string? parentKind,string visibility)=>
        Object(("kind",kind),("canonical",owner),("name",name),("extras",Object(("owner_binary_name",binary),("owner_kind",parentKind),("effective_visibility",visibility))));
    private static JsonObject Package()=>Object(("kind","package"),("canonical","a"),("name","a"));
    private static JsonObject Deprecated()=>Object(("name","java.lang.Deprecated"),("arguments",""),("name_resolution","java-lang"),("source_spelling","Deprecated"));
    private static JsonObject Component(string name,string type,bool annotated)=>Object(("kind","field"),("canonical","a.R#"+name),("name",name),("return_type",type),("annotations",new JsonArray()),
        ("extras",Object(("owner_kind","class"),("record_component",true),("record_component_annotations",annotated?Array(Deprecated()):new JsonArray()),
            ("annotation_scope","component; target propagation unresolved"),("effective_visibility","unknown; source record component metadata"))));
    private static JsonObject Parameter(string name,string type,bool annotated)=>Object(("name",name),("type",type),("direction",null),("annotations",annotated?Array(Deprecated()):new JsonArray()));
    public static readonly IReadOnlyList<JavaCase> All=[
        new("JAVA-PRODUCER-record","package a; record R(@Deprecated int x, String name) { R {} }",
            [Package(),TypeRow("class","a.R","R","a/R",null,"package"),Component("x","int",true),Component("name","String",false),
             Object(("kind","constructor"),("canonical","a.R#<init>(int,String)"),("name","R"),("return_type",null),
                 ("parameters",Array(Parameter("x","int",true),Parameter("name","String",false))),
                 ("extras",Object(("owner_kind","class"),("source_declared_constructor",true))))],Get("JAVA-10").Metadata),
        Nested("interface","I","interface","JAVA-PRODUCER-interface"),
        Nested("@interface","A","annotation","JAVA-PRODUCER-annotation"),
        Nested("class","C","class","JAVA-PRODUCER-class")
    ];
    private static JavaCase Nested(string spelling,string name,string kind,string id)
    {
        var owner="a."+name;var nestedVisibility=kind is "interface" or "annotation"?"public":"package";
        return new(id,"package a; "+spelling+" "+name+" { class Child {} interface Port {} enum Choice { VALUE } @interface Mark {} }",
            [Package(),TypeRow(kind,owner,name,"a/"+name,null,"package"),
             TypeRow("class",owner+".Child","Child","a/"+name+"$Child",kind,nestedVisibility),
             TypeRow("interface",owner+".Port","Port","a/"+name+"$Port",kind,nestedVisibility),
             TypeRow("enum",owner+".Choice","Choice","a/"+name+"$Choice",kind,nestedVisibility),
             Object(("kind","field"),("canonical",owner+".Choice#VALUE"),("name","VALUE"),("return_type",owner+".Choice"),
                 ("extras",Object(("owner_kind","enum"),("enum_constant",true),("effective_visibility","public")))),
             TypeRow("annotation",owner+".Mark","Mark","a/"+name+"$Mark",kind,nestedVisibility)],Get("JAVA-10").Metadata);
    }
    public static IEnumerable<object[]> Cases=>All.Select(test=>new object[]{test.Id});
    public static JavaCase Find(string id)=>All.Single(test=>test.Id==id);
    public static void AssertContract(JavaCase test,ISourceExtractor provider)=>JavaContractCases.AssertContract(test,provider);
}
