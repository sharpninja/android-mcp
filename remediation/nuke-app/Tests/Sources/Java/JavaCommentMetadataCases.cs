using System.Text;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using static AndroidMcp.BuildTool.Tests.Sources.Java.JavaContractCases;

namespace AndroidMcp.BuildTool.Tests.Sources.Java;

// java_source.mask_comments replaces every comment scalar except LF with one
// space. Values below are transcribed from that retained original operation.
public static class JavaCommentMetadataCases
{
    private static JavaCase Create(string id,string comment,string masked)
    {
        var sourceRaw="@A(value={1,"+comment+"2}, text=\"😀\")";
        var arguments="value={1,"+masked+"2}, text=\"😀\"";
        var raw="@A("+arguments+")";
        var annotation=Object(("name","A"),("arguments",arguments),("source_arguments",arguments),
            ("source_spelling","A"),("source_raw",sourceRaw),("raw",raw),
            ("name_resolution","package-candidate; unresolved wildcard imports retained"),
            ("start",0),("end",sourceRaw.EnumerateRunes().Count()));
        return new(id,"class C { "+sourceRaw+" int x; }",
            [Object(("kind","class"),("canonical","C"),("name","C")),
             Object(("kind","field"),("canonical","C#x"),("name","x"),("return_type","int"),
                 ("annotations",Array(annotation)),("all_annotations",Array(annotation)))],
            Object(("package",""),("imports",new JsonObject()),("wildcard_imports",new JsonArray()),("parse_errors",0)));
    }
    public static readonly IReadOnlyList<JavaCase> All=
    [
        Create("JAVA-COMMENT-METADATA-ASCII","/*x*/",new string(' ',5)),
        Create("JAVA-COMMENT-METADATA-SUPPLEMENTARY","/*😀*/",new string(' ',5)),
        Create("JAVA-COMMENT-METADATA-CRLF","/*\r\nx*/","   \n   "),
        Create("JAVA-COMMENT-METADATA-LINE-CRLF","//😀\r\n","    \n")
    ];
    public static IEnumerable<object[]> Cases=>All.Select(test=>new object[]{test.Id});
    public static JavaCase Find(string id)=>All.Single(test=>test.Id==id);
    public static void AssertContract(JavaCase test,ISourceExtractor provider)=>JavaContractCases.AssertContract(test,provider);
}
