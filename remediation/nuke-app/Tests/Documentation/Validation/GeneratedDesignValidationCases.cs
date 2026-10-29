using System.Text;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using AndroidMcp.BuildTool.Documentation.Validation;
using AndroidMcp.BuildTool.Tests.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation.Validation;

public sealed record GeneratedDesignValidationCase(string Id,GeneratedDesignValidationInput Input,GeneratedDesignValidationResult Expected);

// Authored fixtures and surgical mutations, not a second validator. The full
// pinned corpus is added through the shared admitted source fixture authority.
public static class GeneratedDesignValidationCases
{
    public const string ManifestPath="docs/checksums.sha256",HtmlPath="docs/storyboards.html",DeckPath="storyboards/storyboard.md";
    public const string Vector="<svg xmlns=\"http://www.w3.org/2000/svg\"><text>Vector</text></svg>";
    public const string ScreenPath="docs/screens/SCREEN.svg";
    public static readonly string[] SvgPaths=["docs/diagrams/server-core-classes.svg","docs/diagrams/structurizr-Context.svg","docs/screens/SCREEN.svg","docs/screens/STORY.svg","docs/screens/states/SCREEN.READY.svg","docs/screens/steps/STORY.01.svg"];
    private const string BlueprintJson="""
        {"schema_version":"2.0.0","screens":[{"id":"SCREEN","state_specs":{"SCREEN.READY":{}}}],"storyboards":[{"id":"STORY","steps":[{"step_id":"STORY.01"}]}]}
        """;
    public static Artifact Text(string path,string value,string media="text/plain")=>new(path,Encoding.UTF8.GetBytes(value),media);
    public static string Utf8(Artifact value)=>Encoding.UTF8.GetString(value.Bytes.Span);
    public static GeneratedDesignValidationInput Minimal()
    {
        var blueprint=Text("docs/ui-blueprint.md","# Blueprint\n```json ui-blueprint-v2\n"+BlueprintJson+"\n```\n","text/markdown");
        var model=new DocumentationModelReader().Read(blueprint);
        var manifest=(JsonObject)model.Blueprint.DeepClone();manifest["state_svg_files"]=new JsonArray("screens/states/SCREEN.READY.svg","screens/steps/STORY.01.svg");
        var ui=Text("docs/ui-asset-manifest.json",manifest.ToJsonString(),"application/json");
        var dsl=Text("architecture/workspace.dsl","systemContext bridge Context \"context\" {\n}\nimage bridge.app Code {\n image ../docs/diagrams/server-core-classes.svg\n}\n");
        var artifacts=new List<Artifact>{Text("README.md","[Guide](docs/guide.md)\n","text/markdown"),blueprint,
            Text("docs/guide.md","# Guide\n[Screen](screens/SCREEN.svg#title)\n","text/markdown"),
            Text("architecture/code-diagrams.md","![Class](../docs/diagrams/server-core-classes.svg)\n","text/markdown"),
            Text(DeckPath,"# Story\n![Screen](../docs/screens/steps/STORY.01.svg)\n","text/markdown"),
            Text(HtmlPath,"<html><head><style>.x{color:black}</style></head><body>"+Image()+"</body></html>","text/html"),ui,dsl};
        artifacts.AddRange(SvgPaths.Select(path=>Text(path,Vector,"image/svg+xml")));
        artifacts.Add(Text("ui-svg/SCREEN.svg",Vector,"image/svg+xml"));artifacts.Add(Text("ui-svg/STORY.svg",Vector,"image/svg+xml"));
        return new(artifacts.ToArray(),model,dsl,ui,DocumentationValidationPhase.BeforeManifest);
    }
    public static string Image()=>"<img src=\"data:image/svg+xml;base64,"+Convert.ToBase64String(Encoding.UTF8.GetBytes(Vector))+"\" />";
    public static GeneratedDesignValidationResult Clean(DocumentationValidationPhase phase=DocumentationValidationPhase.BeforeManifest)=>new(phase,new(0,4,6,2),SvgPaths.ToArray(),SvgPaths.ToArray(),[],[]);
    private static GeneratedDesignValidationInput Change(GeneratedDesignValidationInput input,string path,Func<Artifact,Artifact> change)=>input with{Artifacts=input.Artifacts.Select(a=>a.RelativePath==path?change(a):a).ToArray()};
    private static GeneratedDesignValidationInput ChangeText(GeneratedDesignValidationInput input,string path,Func<string,string> change)=>Change(input,path,a=>a with{Bytes=Encoding.UTF8.GetBytes(change(Utf8(a)))});
    private static GeneratedDesignValidationInput Append(GeneratedDesignValidationInput input,Artifact artifact)=>input with{Artifacts=input.Artifacts.Append(artifact).ToArray()};
    private static GeneratedDesignValidationInput Remove(GeneratedDesignValidationInput input,string path)=>input with{Artifacts=input.Artifacts.Where(a=>a.RelativePath!=path).ToArray()};
    private static GeneratedDesignValidationResult Errors(GeneratedDesignValidationResult result,params DesignIssue[] issues)=>result with{Issues=issues};
    private static DesignIssue Issue(string code,string path,string subject)=>new(code,path,subject);
    private static GeneratedDesignValidationResult InputError(GeneratedDesignValidationInput input,string code,string path,string subject)=>new(input.Phase,new(0,0,0,0),[],[],[Issue(code,path,subject)],[]);
    private static string Hash(Artifact artifact)=>ContentHash.Sha256(artifact.Bytes.Span);
    private static GeneratedDesignValidationInput Checksum(GeneratedDesignValidationInput input,string content)=>Append(input with{Phase=DocumentationValidationPhase.AfterManifest},Text(ManifestPath,content));
    private static GeneratedDesignValidationInput Ui(GeneratedDesignValidationInput input,Action<JsonObject> change)
    {
        var value=JsonNode.Parse(input.UiManifest.Bytes.Span)!.AsObject();change(value);var artifact=input.UiManifest with{Bytes=Encoding.UTF8.GetBytes(value.ToJsonString())};
        return Change(input,artifact.RelativePath,_=>artifact) with{UiManifest=artifact};
    }
    public static IReadOnlyList<GeneratedDesignValidationCase> Create()
    {
        var cases=new List<GeneratedDesignValidationCase>();
        void Add(string id,GeneratedDesignValidationInput input,GeneratedDesignValidationResult expected)=>cases.Add(new(id,input,expected));
        var clean=Clean();
        Add("DV-POSITIVE-MINIMAL",Minimal(),clean);
        var reversed=Minimal();Add("DV-POSITIVE-INPUT-ORDER",reversed with{Artifacts=reversed.Artifacts.Reverse().ToArray()},clean);
        foreach(var path in new[]{"../outside.md","/outside.md","docs\\outside.md"})
        {var input=Append(Minimal(),Text(path,"input"));Add("DV-INPUT-PATH-"+cases.Count,input,InputError(input,"DOCS_INPUT_PATH",path,"unsafe"));}
        foreach(var different in new[]{false,true})
        {var input=Minimal();var repeated=input.Artifacts[0];input=Append(input,different?repeated with{Bytes="different"u8.ToArray()}:repeated);Add("DV-INPUT-DUPLICATE-"+different,input,InputError(input,"DOCS_INPUT_DUPLICATE",repeated.RelativePath,"duplicate"));}
        foreach(var member in new[]{"dsl","manifest"})
        {
            var input=Minimal();var artifact=member=="dsl"?input.WorkspaceDsl:input.UiManifest;
            var missing=Remove(input,artifact.RelativePath);Add("DV-INPUT-MISSING-"+member,missing,InputError(missing,"DOCS_INPUT_MEMBER",artifact.RelativePath,"missing"));
            var changed=Change(input,artifact.RelativePath,a=>a with{Bytes="different"u8.ToArray()});Add("DV-INPUT-SUBSTITUTED-"+member,changed,InputError(changed,"DOCS_INPUT_MEMBER",artifact.RelativePath,"different"));
        }
        var invalidPhase=Minimal() with{Phase=(DocumentationValidationPhase)99};Add("DV-INPUT-PHASE",invalidPhase,InputError(invalidPhase,"DOCS_PHASE","","unsupported"));
        foreach(var text in new[]{"{","{\"state_svg_files\":[],\"state_svg_files\":[]}","[]"})
        {
            var input=Minimal();var ui=input.UiManifest with{Bytes=Encoding.UTF8.GetBytes(text)};input=Change(input,ui.RelativePath,_=>ui) with{UiManifest=ui};
            Add("DV-MODEL-JSON-"+cases.Count,input,Errors(clean with{ExpectedSvgPaths=[]},Issue("DOCS_UI_MANIFEST",ui.RelativePath,"strict-json")));
        }
        {
            var input=Minimal();var ui=input.UiManifest with{Bytes=new byte[]{0xff,0xfe}};input=Change(input,ui.RelativePath,_=>ui) with{UiManifest=ui};
            Add("DV-MODEL-UTF8",input,Errors(clean with{ExpectedSvgPaths=[]},Issue("DOCS_UI_MANIFEST",ui.RelativePath,"strict-json")));
        }
        foreach(var id in new[]{"JSON-25-escaped-duplicate","JSON-27-overflow","JSON-28-unpaired-surrogate"})
        {
            var shared=CanonicalJsonCases.Values.Single(value=>value.Id==id);var raw=shared.RawBytes??Encoding.UTF8.GetBytes(shared.Raw);
            using var bytes=new MemoryStream();bytes.Write("{\"extra\":"u8);bytes.Write(raw);bytes.Write(",\"state_svg_files\":[]}"u8);
            var input=Minimal();var ui=input.UiManifest with{Bytes=bytes.ToArray()};input=Change(input,ui.RelativePath,_=>ui) with{UiManifest=ui};
            Add("DV-MODEL-HIDDEN-"+id,input,Errors(clean with{ExpectedSvgPaths=[]},Issue("DOCS_UI_MANIFEST",ui.RelativePath,"strict-json")));
        }
        foreach(var mutation in new[]{"identity","variant-missing","variant-extra","variant-duplicate","variant-unsafe"})
        {
            var input=Ui(Minimal(),json=>{switch(mutation){case "identity":json["screens"]![0]!["id"]="OTHER";break;case "variant-missing":json["state_svg_files"]!.AsArray().RemoveAt(0);break;case "variant-extra":json["state_svg_files"]!.AsArray().Add("screens/states/EXTRA.svg");break;case "variant-duplicate":json["state_svg_files"]!.AsArray().Add("screens/states/SCREEN.READY.svg");break;case "variant-unsafe":json["state_svg_files"]![0]="../outside.svg";break;}});
            Add("DV-MODEL-"+mutation,input,Errors(clean with{ExpectedSvgPaths=[]},Issue("DOCS_UI_MANIFEST",input.UiManifest.RelativePath,mutation=="identity"?"model-identity":"model-variants")));
        }
        {
            var input=Minimal();var dsl=input.WorkspaceDsl with{Bytes=Encoding.UTF8.GetBytes(Utf8(input.WorkspaceDsl)+"systemContext bridge Context \"duplicate\" {\n}\n")};input=Change(input,dsl.RelativePath,_=>dsl) with{WorkspaceDsl=dsl};
            Add("DV-MODEL-DUPLICATE-VIEW",input,Errors(clean,Issue("DOCS_VIEW_DUPLICATE",dsl.RelativePath,"Context")));
        }
        foreach(var path in new[]{SvgPaths[0],ScreenPath,"docs/screens/states/SCREEN.READY.svg"})
        {
            var input=Remove(Minimal(),path);var issues=new List<DesignIssue>{Issue("DOCS_SVG_MISSING",path,"expected")};
            if(path==SvgPaths[0])issues.Add(Issue("DOCS_LINK_MISSING","architecture/code-diagrams.md","../docs/diagrams/server-core-classes.svg"));
            if(path==ScreenPath){issues.Add(Issue("DOCS_LINK_MISSING","docs/guide.md","screens/SCREEN.svg#title"));issues.Add(Issue("DOCS_COPY_MISSING","ui-svg/SCREEN.svg",ScreenPath));}
            Add("DV-SET-MISSING-"+cases.Count,input,clean with{Counters=clean.Counters with{RenderedSvgCount=5},ActualSvgPaths=SvgPaths.Where(p=>p!=path).ToArray(),Issues=issues});
        }
        foreach(var path in new[]{"docs/diagrams/unexpected.svg","docs/screens/EXTRA.svg","docs/screens/states/EXTRA.svg"})
        {var input=Append(Minimal(),Text(path,Vector,"image/svg+xml"));Add("DV-SET-EXTRA-"+cases.Count,input,Errors(clean with{Counters=clean.Counters with{RenderedSvgCount=7},ActualSvgPaths=SvgPaths.Append(path).Order(StringComparer.Ordinal).ToArray()},Issue("DOCS_SVG_UNEXPECTED",path,"unexpected")));}
        Add("DV-SET-UPPERCASE-OUTSIDE-GLOB",Append(Minimal(),Text("docs/ignored.SVG","opaque")),clean);
        foreach(var fixture in new[]{
            ("MALFORMED","<svg","DOCS_SVG_XML","xml"),
            ("DTD","<!DOCTYPE svg SYSTEM \"file:///unadmitted\">"+Vector,"DOCS_SVG_XML","xml"),
            ("ROOT","<root xmlns=\"http://www.w3.org/2000/svg\"><text>Vector</text></root>","DOCS_SVG_ROOT","svg"),
            ("NAMESPACE","<svg><text>Vector</text></svg>","DOCS_SVG_NAMESPACE","http://www.w3.org/2000/svg"),
            ("TEXT",Vector.Replace("<text>Vector</text>","",StringComparison.Ordinal),"DOCS_SVG_TEXT","text"),
            ("RASTER",Vector.Replace("</svg>","<image href=\"data:image/png;base64,AA==\"/></svg>",StringComparison.Ordinal),"DOCS_SVG_RASTER","image"),
            ("RASTER-CASE",Vector.Replace("</svg>","<IMAGE/></svg>",StringComparison.Ordinal),"DOCS_SVG_RASTER","image"),
            ("SCRIPT",Vector.Replace("</svg>","<script/></svg>",StringComparison.Ordinal),"DOCS_SVG_SCRIPT","script"),
            ("SCRIPT-CASE",Vector.Replace("</svg>","<SCRIPT/></svg>",StringComparison.Ordinal),"DOCS_SVG_SCRIPT","script"),
            ("SRC",Vector.Replace("</svg>","<!--SRC=[retained]--></svg>",StringComparison.Ordinal),"DOCS_SVG_METADATA","SRC")})
        {
            const string path="docs/diagrams/server-core-classes.svg";var input=ChangeText(Minimal(),path,_=>fixture.Item2);var result=Errors(clean,Issue(fixture.Item3,path,fixture.Item4));
            if(fixture.Item3=="DOCS_SVG_XML")result=result with{ParseDiagnostics=[new(path,1,1,"fixture parse diagnostic")]};
            Add("DV-XML-"+fixture.Item1,input,result);
        }
        Add("DV-XML-SRC-CASE-BOUNDARY",ChangeText(Minimal(),SvgPaths[0],value=>value.Replace("</svg>","<!--src=[allowed-by-original-case-rule]--></svg>",StringComparison.Ordinal)),clean);
        Add("DV-XML-RAW-SCRIPT-IN-COMMENT",ChangeText(Minimal(),SvgPaths[0],value=>value.Replace("</svg>","<!--<script/>--></svg>",StringComparison.Ordinal)),Errors(clean,Issue("DOCS_SVG_SCRIPT",SvgPaths[0],"script")));
        foreach(var fixture in new[]{
            ("FILE","screens/SCREEN.svg#title",(string?)null,(string?)null),
            ("FRAGMENT","#section",null,null),
            ("ANGLE","<../README.md#section>",null,null),
            ("URI","https://example.test/a",null,null),
            ("DATA","data:image/svg+xml;base64,AA==",null,null),
            ("MISSING","missing.md","DOCS_LINK_MISSING","missing.md"),
            ("DIRECTORY","../docs/","DOCS_LINK_MISSING","../docs/"),
            ("COLON","mailto:owner@example.test","DOCS_LINK_INVALID","unsafe-path"),
            ("ESCAPE","../../outside.md","DOCS_LINK_INVALID","namespace-escape")})
        {
            var input=Append(Minimal(),Text("docs/link-case.md","[Link]("+fixture.Item2+")\n","text/markdown"));
            var ignored=fixture.Item1 is "FRAGMENT" or "URI" or "DATA";var result=clean with{Counters=clean.Counters with{MarkdownLinksChecked=ignored?4:5}};
            if(fixture.Item3 is {} code)result=Errors(result,Issue(code,"docs/link-case.md",fixture.Item4!));Add("DV-LINK-"+fixture.Item1,input,result);
        }
        foreach(var name in new[]{"A%20B.md","target.md?raw=1","A B.md"})
        {var input=Append(Append(Minimal(),Text("docs/"+name,"target")),Text("docs/link-case.md","[Link]("+name+")\n","text/markdown"));Add("DV-LINK-LITERAL-"+cases.Count,input,clean with{Counters=clean.Counters with{MarkdownLinksChecked=5}});}
        Add("DV-LINK-OUTSIDE-SELECTED-SCOPE",Append(Minimal(),Text("notes/unrelated.md","[bad](missing.md)\n```mermaid\n","text/markdown")),clean);
        Add("DV-LINK-DATA-EXTENSION",Append(Minimal(),Text("docs/historical.md.fixture","[bad](missing.md)\n```mermaid\n")),clean);
        Add("DV-LINK-CURRENT-MERMAID",Append(Minimal(),Text("docs/source.md","```MeRmAiD\n","text/markdown")),Errors(clean,Issue("DOCS_MERMAID","docs/source.md","current-source")));
        foreach(var path in new[]{"README.md","architecture/code-diagrams.md",DeckPath})
        {var input=Remove(Minimal(),path);Add("DV-LINK-REQUIRED-"+cases.Count,input,Errors(clean with{Counters=clean.Counters with{MarkdownLinksChecked=3}},Issue("DOCS_REQUIRED_SOURCE",path,"missing")));}
        Add("DV-COPY-MISSING",Remove(Minimal(),"ui-svg/SCREEN.svg"),Errors(clean with{Counters=clean.Counters with{EditableUiCopies=1}},Issue("DOCS_COPY_MISSING","ui-svg/SCREEN.svg",ScreenPath)));
        Add("DV-COPY-DIFFERENT",ChangeText(Minimal(),"ui-svg/SCREEN.svg",value=>value+" "),Errors(clean,Issue("DOCS_COPY_DIFFERENT","ui-svg/SCREEN.svg",ScreenPath)));
        Add("DV-COPY-UNEXPECTED",Append(Minimal(),Text("ui-svg/EXTRA.svg",Vector,"image/svg+xml")),Errors(clean with{Counters=clean.Counters with{EditableUiCopies=3}},Issue("DOCS_COPY_UNEXPECTED","ui-svg/EXTRA.svg","unexpected")));
        Add("DV-COPY-NESTED-BOUNDARY",Append(Minimal(),Text("ui-svg/nested/data.svg",Vector,"image/svg+xml")),clean);
        foreach(var tag in new[]{"script","img","link","iframe","video","audio"})foreach(var attribute in new[]{"src","href"})
        {
            const string value="https://example.test/asset";var input=Minimal();
            input=ChangeText(input,HtmlPath,text=>tag=="img"?text.Replace("<img ","<img "+attribute+"=\""+value+"\" ",StringComparison.Ordinal):text.Replace("</body>","<"+tag+" "+attribute+"=\""+value+"\"></"+tag+"></body>",StringComparison.Ordinal));
            // For src on img retain both attributes: original HTMLParser reports
            // duplicate attributes, while the SVG embedding count stays one.
            Add("DV-HTML-TAG-"+tag+"-"+attribute,input,Errors(clean,Issue("DOCS_HTML_GRADLE_EXTERNAL",HtmlPath,"attribute:"+value),Issue("DOCS_HTML_ASSET_EXTERNAL",HtmlPath,tag+":"+attribute+":"+value)));
        }
        foreach(var fixture in new[]{("UPPERCASE","HTTP://example.test/asset",true,false),("ENTITY","&#104;ttps://example.test/asset",false,true),("PROTOCOL","//example.test/asset",true,true),("HTTP-NO-SLASH","http:asset",false,true),("LOCAL","local.js",false,false),("DATA","data:text/plain;base64,AA==",false,false),("FTP","ftp://example.test/asset",false,false)})
        {
            var input=ChangeText(Minimal(),HtmlPath,text=>text.Replace("</body>","<script src=\""+fixture.Item2+"\"></script></body>",StringComparison.Ordinal));var issues=new List<DesignIssue>();
            if(fixture.Item3)issues.Add(Issue("DOCS_HTML_GRADLE_EXTERNAL",HtmlPath,"attribute:"+fixture.Item2));
            if(fixture.Item4)issues.Add(Issue("DOCS_HTML_ASSET_EXTERNAL",HtmlPath,"script:src:"+(fixture.Item1=="ENTITY"?"https://example.test/asset":fixture.Item2)));
            Add("DV-HTML-VALUE-"+fixture.Item1,input,clean with{Issues=issues});
        }
        foreach(var fixture in new[]{("COMMENT","<!-- <script src=\"https://example.test/asset\"></script> -->"),("RAW-SCRIPT","<script>const text='<link href=\"https://example.test/asset\">';</script>"),("UNRELATED-TAG","<a href=\"https://example.test/asset\">link</a>")})
        {var input=ChangeText(Minimal(),HtmlPath,text=>text.Replace("</body>",fixture.Item2+"</body>",StringComparison.Ordinal));Add("DV-HTML-"+fixture.Item1,input,Errors(clean,Issue("DOCS_HTML_GRADLE_EXTERNAL",HtmlPath,"attribute:https://example.test/asset")));}
        Add("DV-HTML-QUOTED-GREATER",ChangeText(Minimal(),HtmlPath,text=>text.Replace("</body>","<script title=\"quoted > value\" src=\"https://example.test/asset\"></script></body>",StringComparison.Ordinal)),Errors(clean,Issue("DOCS_HTML_GRADLE_EXTERNAL",HtmlPath,"attribute:https://example.test/asset"),Issue("DOCS_HTML_ASSET_EXTERNAL",HtmlPath,"script:src:https://example.test/asset")));
        Add("DV-HTML-DECODE-ONCE",ChangeText(Minimal(),HtmlPath,text=>text.Replace("</body>","<script src=\"&amp;#104;ttps://example.test/asset\"></script></body>",StringComparison.Ordinal)),clean);
        Add("DV-HTML-SPACE-EQUALS",ChangeText(Minimal(),HtmlPath,text=>text.Replace("</body>","<script src = \"https://example.test/asset\"></script></body>",StringComparison.Ordinal)),Errors(clean,Issue("DOCS_HTML_ASSET_EXTERNAL",HtmlPath,"script:src:https://example.test/asset")));
        Add("DV-HTML-UNQUOTED",ChangeText(Minimal(),HtmlPath,text=>text.Replace("</body>","<script src=https://example.test/asset></script></body>",StringComparison.Ordinal)),Errors(clean,Issue("DOCS_HTML_ASSET_EXTERNAL",HtmlPath,"script:src:https://example.test/asset")));
        Add("DV-HTML-UPPERCASE-TAG-ATTRIBUTE",ChangeText(Minimal(),HtmlPath,text=>text.Replace("</body>","<SCRIPT SRC=\"https://example.test/asset\"></SCRIPT></body>",StringComparison.Ordinal)),Errors(clean,Issue("DOCS_HTML_GRADLE_EXTERNAL",HtmlPath,"attribute:https://example.test/asset"),Issue("DOCS_HTML_ASSET_EXTERNAL",HtmlPath,"script:src:https://example.test/asset")));
        Add("DV-HTML-DUPLICATE-ATTRIBUTES",ChangeText(Minimal(),HtmlPath,text=>text.Replace("</body>","<script src=\"https://example.test/asset\" src=\"https://example.test/asset\"></script></body>",StringComparison.Ordinal)),Errors(clean,Issue("DOCS_HTML_GRADLE_EXTERNAL",HtmlPath,"attribute:https://example.test/asset"),Issue("DOCS_HTML_GRADLE_EXTERNAL",HtmlPath,"attribute:https://example.test/asset"),Issue("DOCS_HTML_ASSET_EXTERNAL",HtmlPath,"script:src:https://example.test/asset"),Issue("DOCS_HTML_ASSET_EXTERNAL",HtmlPath,"script:src:https://example.test/asset")));
        Add("DV-HTML-CSS",ChangeText(Minimal(),HtmlPath,text=>text.Replace("</style>",".x{background:url(https://example.test/asset)}</style>",StringComparison.Ordinal)),Errors(clean,Issue("DOCS_HTML_GRADLE_EXTERNAL",HtmlPath,"css:https://example.test/asset")));
        foreach(var fixture in new[]{("PROTOCOL-SINGLE","url( '//example.test/asset')","//example.test/asset"),("UPPERCASE-DOUBLE","URL(\"HTTP://example.test/asset\")","HTTP://example.test/asset"),("WHITESPACE","url(   https://example.test/asset)","https://example.test/asset")})
        {var input=ChangeText(Minimal(),HtmlPath,text=>text.Replace("</style>",".x{background:"+fixture.Item2+"}</style>",StringComparison.Ordinal));Add("DV-HTML-CSS-"+fixture.Item1,input,Errors(clean,Issue("DOCS_HTML_GRADLE_EXTERNAL",HtmlPath,"css:"+fixture.Item3)));}
        Add("DV-HTML-MISSING",Remove(Minimal(),HtmlPath),Errors(clean,Issue("DOCS_REQUIRED_HTML",HtmlPath,"missing")));
        Add("DV-EMBED-MISSING-IMAGE",ChangeText(Minimal(),HtmlPath,text=>text.Replace(Image(),"",StringComparison.Ordinal)),Errors(clean,Issue("DOCS_EMBED_IMAGE_COUNT",HtmlPath,"expected=1;actual=0"),Issue("DOCS_EMBED_DATA_COUNT",HtmlPath,"expected=1;actual=0")));
        Add("DV-EMBED-EXTRA-IMAGE",ChangeText(Minimal(),HtmlPath,text=>text.Replace("</body>",Image()+"</body>",StringComparison.Ordinal)),Errors(clean,Issue("DOCS_EMBED_IMAGE_COUNT",HtmlPath,"expected=1;actual=2"),Issue("DOCS_EMBED_DATA_COUNT",HtmlPath,"expected=1;actual=2")));
        Add("DV-EMBED-RESIDUAL-MARKDOWN",ChangeText(Minimal(),HtmlPath,text=>text.Replace("</body>","![Screen]</body>",StringComparison.Ordinal)),Errors(clean,Issue("DOCS_EMBED_MARKDOWN",HtmlPath,"![Screen]")));
        Add("DV-EMBED-NONSVG-DATA",ChangeText(Minimal(),HtmlPath,text=>text.Replace("data:image/svg+xml;base64,","data:image/png;base64,",StringComparison.Ordinal)),Errors(clean,Issue("DOCS_EMBED_DATA_COUNT",HtmlPath,"expected=1;actual=0")));
        Add("DV-EMBED-SINGLE-QUOTE",ChangeText(Minimal(),HtmlPath,text=>text.Replace(Image(),Image().Replace("\"","'",StringComparison.Ordinal),StringComparison.Ordinal)),Errors(clean,Issue("DOCS_EMBED_DATA_COUNT",HtmlPath,"expected=1;actual=0")));
        Add("DV-EMBED-UPPERCASE-IMG",ChangeText(Minimal(),HtmlPath,text=>text.Replace("<img ","<IMG ",StringComparison.Ordinal)),Errors(clean,Issue("DOCS_EMBED_IMAGE_COUNT",HtmlPath,"expected=1;actual=0")));
        {
            var input=ChangeText(Minimal(),DeckPath,text=>text+"![Screen](../docs/screens/steps/STORY.01.svg)\n");input=ChangeText(input,HtmlPath,text=>text.Replace("</body>",Image()+"</body>",StringComparison.Ordinal));Add("DV-EMBED-DUPLICATE-OCCURRENCE",input,clean with{Counters=clean.Counters with{MarkdownLinksChecked=5}});
        }
        {
            var input=Append(Minimal(),Text(ManifestPath,"not inspected"));Add("DV-MANIFEST-BEFORE-PRESENT",input,Errors(clean,Issue("DOCS_PHASE_CHECKSUM",ManifestPath,"BeforeManifest")));
        }
        Add("DV-MANIFEST-AFTER-MISSING",Minimal() with{Phase=DocumentationValidationPhase.AfterManifest},Errors(Clean(DocumentationValidationPhase.AfterManifest),Issue("DOCS_CHECKSUM_REQUIRED",ManifestPath,"missing")));
        Add("DV-MANIFEST-EMPTY-NO-COVERAGE-CLAIM",Checksum(Minimal(),""),Clean(DocumentationValidationPhase.AfterManifest));
        foreach(var ending in new[]{"","\n","\r\n"})
        {var input=Minimal();var target=input.Artifacts.Single(a=>a.RelativePath=="README.md");input=Checksum(input,Hash(target)+"  README.md"+ending);Add("DV-MANIFEST-LINE-ENDING-"+cases.Count,input,Clean(DocumentationValidationPhase.AfterManifest) with{Counters=clean.Counters with{ChecksumsChecked=1}});}
        foreach(var fixture in new[]{("SHORT","abc  README.md"),("UPPERCASE",new string('A',64)+"  README.md"),("SEPARATOR",new string('0',64)+" README.md"),("BLANK","\n")})
        {var input=Checksum(Minimal(),fixture.Item2);Add("DV-MANIFEST-MALFORMED-"+fixture.Item1,input,Errors(Clean(DocumentationValidationPhase.AfterManifest) with{Counters=clean.Counters with{ChecksumsChecked=1}},Issue("DOCS_CHECKSUM_LINE",ManifestPath,"1")));}
        foreach(var fixture in new[]{("UNSAFE","../outside","DOCS_CHECKSUM_PATH"),("SELF",ManifestPath,"DOCS_CHECKSUM_SELF"),("MISSING","absent.txt","DOCS_CHECKSUM_MISSING"),("CHANGED","README.md","DOCS_CHECKSUM_HASH")})
        {var input=Checksum(Minimal(),new string('0',64)+"  "+fixture.Item2+"\n");Add("DV-MANIFEST-"+fixture.Item1,input,Errors(Clean(DocumentationValidationPhase.AfterManifest) with{Counters=clean.Counters with{ChecksumsChecked=1}},Issue(fixture.Item3,ManifestPath,fixture.Item2)));}
        {
            var input=Minimal();var target=input.Artifacts[0];var line=Hash(target)+"  "+target.RelativePath+"\n";input=Checksum(input,line+line);Add("DV-MANIFEST-DUPLICATE",input,Errors(Clean(DocumentationValidationPhase.AfterManifest) with{Counters=clean.Counters with{ChecksumsChecked=2}},Issue("DOCS_CHECKSUM_DUPLICATE",ManifestPath,target.RelativePath)));
        }
        {
            var input=Minimal();var targets=new[]{input.Artifacts.Single(a=>a.RelativePath=="docs/guide.md"),input.Artifacts.Single(a=>a.RelativePath=="README.md")};
            var lines=targets.Select(target=>Hash(target)+"  "+target.RelativePath+"\n").ToArray();
            Add("DV-MANIFEST-REORDERED-PARTIAL",Checksum(input,string.Concat(lines)),Clean(DocumentationValidationPhase.AfterManifest) with{Counters=clean.Counters with{ChecksumsChecked=2}});
            Add("DV-MANIFEST-INTERIOR-BLANK",Checksum(input,lines[0]+"\n"+lines[1]),Errors(Clean(DocumentationValidationPhase.AfterManifest) with{Counters=clean.Counters with{ChecksumsChecked=3}},Issue("DOCS_CHECKSUM_LINE",ManifestPath,"2")));
        }
        {
            var input=Append(Minimal(),Text("docs/space 雪.txt","content"));var target=input.Artifacts.Last();input=Checksum(input,Hash(target)+"  "+target.RelativePath+"\n");Add("DV-MANIFEST-SPACE-UNICODE",input,Clean(DocumentationValidationPhase.AfterManifest) with{Counters=clean.Counters with{ChecksumsChecked=1}});
        }
        {
            var input=Minimal();var checksum=DocumentationPackageCases.Checksums(new(input.Artifacts,[],ReadOnlyMemory<byte>.Empty));input=Append(input with{Phase=DocumentationValidationPhase.AfterManifest},checksum);Add("DV-MANIFEST-PACKAGE-FORMAT",input,Clean(DocumentationValidationPhase.AfterManifest) with{Counters=clean.Counters with{ChecksumsChecked=16}});
        }
        {
            var input=ChangeText(Minimal(),SvgPaths[0],_=>"<svg");input=ChangeText(input,ScreenPath,_=>"<svg");input=ChangeText(input,"ui-svg/SCREEN.svg",_=>"<svg");
            var result=Errors(clean,Issue("DOCS_SVG_XML",SvgPaths[0],"xml"),Issue("DOCS_SVG_XML",ScreenPath,"xml")) with{ParseDiagnostics=[new(SvgPaths[0],1,1,"first diagnostic"),new(ScreenPath,1,1,"second diagnostic")]};
            Add("DV-ORDER-MULTIPLE-PARSE",input,result);Add("DV-ORDER-MULTIPLE-PARSE-REVERSED",input with{Artifacts=input.Artifacts.Reverse().ToArray()},result);
        }
        cases.AddRange(GeneratedDesignValidationCorpusCases.Create());return cases;
    }
    private static readonly Lazy<IReadOnlyList<GeneratedDesignValidationCase>> Templates=new(Create);
    public static IEnumerable<object[]> Cases=>Templates.Value.Select(test=>new object[]{test.Id});
    public static GeneratedDesignValidationCase Get(string id)
    {
        var test=Templates.Value.Single(test=>test.Id==id);var source=test.Input;var blueprint=(JsonObject)source.Model.Blueprint.DeepClone();
        var screens=blueprint["screens"]!.AsArray().Select(node=>node!.AsObject()).ToArray();var stories=blueprint["storyboards"]!.AsArray().Select(node=>node!.AsObject()).ToArray();
        var model=new DocumentationModel(blueprint,screens,stories,screens.ToDictionary(screen=>screen["id"]!.GetValue<string>(),StringComparer.Ordinal));
        return test with{Input=source with{Artifacts=source.Artifacts.Select(value=>value with{Bytes=value.Bytes.ToArray()}).ToArray(),Model=model,
            WorkspaceDsl=source.WorkspaceDsl with{Bytes=source.WorkspaceDsl.Bytes.ToArray()},UiManifest=source.UiManifest with{Bytes=source.UiManifest.Bytes.ToArray()}}};
    }
    public static void AssertContract(GeneratedDesignValidationCase test,IGeneratedDesignValidator provider)
    {
        var input=test.Input;var artifacts=input.Artifacts.Select(a=>(a.RelativePath,a.MediaType,Bytes:a.Bytes.ToArray())).ToArray();var model=input.Model.Blueprint.ToJsonString();
        var screens=input.Model.Screens.ToArray();var stories=input.Model.Storyboards.ToArray();var index=input.Model.ScreensById.ToArray();var dsl=input.WorkspaceDsl.Bytes.ToArray();var ui=input.UiManifest.Bytes.ToArray();
        var actual=provider.Validate(input);Assert.Equal("generated-design",actual.Scope);Assert.Equal(test.Expected.Phase,actual.Phase);Assert.Equal(test.Expected.Counters,actual.Counters);
        Assert.Equal(test.Expected.ExpectedSvgPaths,actual.ExpectedSvgPaths);Assert.Equal(test.Expected.ActualSvgPaths,actual.ActualSvgPaths);Assert.Equal(test.Expected.Issues,actual.Issues);Assert.Equal(test.Expected.IsValid,actual.IsValid);
        Assert.Equal(test.Expected.ParseDiagnostics.Count,actual.ParseDiagnostics.Count);
        for(var i=0;i<actual.ParseDiagnostics.Count;i++){Assert.Equal(test.Expected.ParseDiagnostics[i].ArtifactPath,actual.ParseDiagnostics[i].ArtifactPath);Assert.True(actual.ParseDiagnostics[i].Line>0);Assert.True(actual.ParseDiagnostics[i].Column>0);Assert.False(string.IsNullOrWhiteSpace(actual.ParseDiagnostics[i].Message));}
        Assert.Equal(model,input.Model.Blueprint.ToJsonString());Assert.Equal(screens,input.Model.Screens);Assert.Equal(stories,input.Model.Storyboards);Assert.Equal(index,input.Model.ScreensById.ToArray());Assert.Equal(dsl,input.WorkspaceDsl.Bytes.ToArray());Assert.Equal(ui,input.UiManifest.Bytes.ToArray());
        Assert.Equal(artifacts.Length,input.Artifacts.Count);for(var i=0;i<artifacts.Length;i++){Assert.Equal(artifacts[i].RelativePath,input.Artifacts[i].RelativePath);Assert.Equal(artifacts[i].MediaType,input.Artifacts[i].MediaType);Assert.Equal(artifacts[i].Bytes,input.Artifacts[i].Bytes.ToArray());}
    }
}
