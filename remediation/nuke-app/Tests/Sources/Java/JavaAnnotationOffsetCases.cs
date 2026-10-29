using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using static AndroidMcp.BuildTool.Tests.Sources.Java.JavaContractCases;

namespace AndroidMcp.BuildTool.Tests.Sources.Java;

// Source-pinned java_source.mask_comments/qualify_anns: one Python character
// masks to one space. Both seven-character comments place @ at code-point15.
public static class JavaAnnotationOffsetCases
{
    private static JavaCase Create(string id,string comment)
    {
        var annotation=Object(("name","java.lang.Deprecated"),("arguments",""),("source_arguments",""),
            ("source_spelling","Deprecated"),("source_raw","@Deprecated"),("raw","@Deprecated"),
            ("name_resolution","java-lang"),("start",15),("end",26));
        return new(id,"class C { public /* "+comment+" */ @Deprecated int x; }",
            [Object(("kind","class"),("canonical","C"),("name","C")),
             Object(("kind","field"),("canonical","C#x"),("name","x"),("return_type","int"),
                 ("annotations",Array(annotation)),("all_annotations",Array(annotation)))],
            Object(("package",""),("imports",new JsonObject()),("wildcard_imports",new JsonArray()),("parse_errors",0)));
    }
    public static readonly IReadOnlyList<JavaCase> All=[Create("JAVA-ANNOTATION-OFFSET-ASCII","x"),Create("JAVA-ANNOTATION-OFFSET-SUPPLEMENTARY","😀")];
    public static IEnumerable<object[]> Cases=>All.Select(test=>new object[]{test.Id});
    public static JavaCase Find(string id)=>All.Single(test=>test.Id==id);
    public static void AssertContract(JavaCase test,ISourceExtractor provider)=>JavaContractCases.AssertContract(test,provider);
}
