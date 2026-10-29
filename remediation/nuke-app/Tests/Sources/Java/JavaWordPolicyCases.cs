using System.Text;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using static AndroidMcp.BuildTool.Tests.Sources.Java.JavaContractCases;

namespace AndroidMcp.BuildTool.Tests.Sources.Java;

// Retained parsers.annotations and java_source import substitutions use Python
// Unicode alphanumeric+underscore word policy, not .NET's combining-mark set.
public static class JavaWordPolicyCases
{
    private static JsonObject Annotation(string spelling,int start)=>Object(("name",spelling),("arguments",""),("source_arguments",""),
        ("source_spelling",spelling),("source_raw","@"+spelling),("raw","@"+spelling),
        ("name_resolution","package-candidate; unresolved wildcard imports retained"),("start",start),("end",start+spelling.EnumerateRunes().Count()+1));
    private static JsonObject Root()=>Object(("kind","class"),("canonical","C"),("name","C"));
    private static JsonObject Field(string name,string type)=>Object(("kind","field"),("canonical","C#"+name),("name",name),("return_type",type));
    private static JsonObject Metadata(bool imported=false)=>Object(("package",""),("imports",imported?Object(("A","p.A")):new JsonObject()),("wildcard_imports",new JsonArray()),("parse_errors",0));
    public static readonly IReadOnlyList<JavaCase> All=
    [
        new("JAVA-WORD-ANNOTATION-NAMES","class C { @Ⅳ @A\u0301 @𐐀 int x; }",
            [Root(),Object(("kind","field"),("canonical","C#x"),("name","x"),("return_type","int"),
                ("annotations",Array(Annotation("Ⅳ",0),Annotation("A",3),Annotation("𐐀",7))),("all_annotations",Array(Annotation("Ⅳ",0),Annotation("A",3),Annotation("𐐀",7))))],Metadata()),
        new("JAVA-WORD-TYPE-BOUNDARIES","import p.A; class C { ⅣA first; A\u0301 second; }",
            [Root(),Field("first","ⅣA"),Field("second","p.A\u0301")],Metadata(true)),
        new("JAVA-WORD-ARGUMENT-BOUNDARIES","import p.A; class C { @Ann({ⅣA.VALUE,\"²A.VALUE\",\"𐐀A.VALUE\",\"😀A.VALUE\",A.VALUE}) int x; }",
            [Root(),Object(("kind","field"),("canonical","C#x"),("name","x"),("return_type","int"),
                ("annotations",Array(Object(("name","Ann"),("source_arguments","{ⅣA.VALUE,\"²A.VALUE\",\"𐐀A.VALUE\",\"😀A.VALUE\",A.VALUE}"),("arguments","{ⅣA.VALUE,\"²A.VALUE\",\"𐐀A.VALUE\",\"😀p.A.VALUE\",p.A.VALUE}")))),
                ("all_annotations",Array(Object(("name","Ann"),("source_arguments","{ⅣA.VALUE,\"²A.VALUE\",\"𐐀A.VALUE\",\"😀A.VALUE\",A.VALUE}"),("arguments","{ⅣA.VALUE,\"²A.VALUE\",\"𐐀A.VALUE\",\"😀p.A.VALUE\",p.A.VALUE}")))))],Metadata(true))
    ];
    public static IEnumerable<object[]> Cases=>All.Select(test=>new object[]{test.Id});
    public static JavaCase Find(string id)=>All.Single(test=>test.Id==id);
    public static void AssertContract(JavaCase test,ISourceExtractor provider)=>JavaContractCases.AssertContract(test,provider);
}
