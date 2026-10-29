using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation.Browser;

namespace AndroidMcp.BuildTool.Tests.Documentation.Browser;

internal static partial class BrowserQaCases
{
    internal static string Svg(bool outside=false)=>"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"900\" height=\"720\" viewBox=\"0 0 900 720\"><rect x=\"0\" y=\"0\" width=\"900\" height=\"720\" fill=\"white\"/><g transform=\"translate("+(outside?"1000":"0")+" 0)\"><text x=\"10\" y=\"30\">fixture</text></g><circle cx=\"40\" cy=\"50\" r=\"5\"/></svg>";
    internal static BrowserQaRequest CreateInput(string root,BrowserQaCase test,ToolIdentity? browser=null)
    {
        var request=Request(root) with{Browser=browser??Request(root).Browser,ExpectedArchitectureDiagrams=1};var input=request.ProjectRoot;Directory.CreateDirectory(input);
        foreach(var path in new[]{"docs/screens/states/SCR01.S0.svg","docs/screens/states/SCR01.S1.svg","docs/screens/steps/STB01-1.svg","docs/screens/steps/STB01-2.svg","docs/diagrams/fixture.svg"})
        {var target=Path.Combine(input,path);Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.WriteAllText(target,Svg(test.Failure=="svg"&&path=="docs/screens/states/SCR01.S0.svg"),new UTF8Encoding(false));}
        var sections="<section class=\"active\"><h1>Cover</h1>"+(test.Failure=="overflow"?"<p style=\"position:absolute;left:1500px;top:0;width:300px;white-space:nowrap\">fixture overflow</p>":"")+"</section>";
        for(var i=0;i<2;i++)sections+="<section data-step-id=\"STB01-"+(i+1)+"\"><h1>Step "+(i+1)+"</h1><img alt=\"Screen\" src=\"data:image/svg+xml;base64,"+Convert.ToBase64String(Encoding.UTF8.GetBytes(test.Failure=="image"&&i==0?"not SVG":Svg()))+"\"></section>";
        var html="<!doctype html><meta charset=\"utf-8\"><title>Native QA fixture</title><style>body{margin:0}section{display:none;position:relative;width:1280px;height:720px}section.active{display:block}h1{margin:0;padding:20px}img{width:900px;height:650px}</style><main>"+sections+"</main><script>let active=0;function show(i){active=Math.max(0,Math.min(2,i));document.querySelectorAll('main>section').forEach((s,j)=>s.classList.toggle('active',j===active));}addEventListener('keydown',e=>{if(e.key==='ArrowRight')"+(test.Failure=="navigation"?"{}":"show(active+1)")+";if(e.key==='ArrowLeft')show(active-1);});"+(test.Failure=="fonts"?"Object.defineProperty(document.fonts,'status',{get:()=> 'loading'});":"")+"</script>"+(test.Failure=="external"?"<img alt=\"external fixture\" src=\"https://blocked.example/fixture\">":"");
        File.WriteAllText(Path.Combine(input,"docs/storyboards.html"),html,new UTF8Encoding(false));
        var steps=new JsonArray(CdpCases.Obj(("step_id","STB01-1"),("state_id","SCR01.S0")),CdpCases.Obj(("step_id","STB01-2"),("state_id","SCR01.S1")));
        var manifest=CdpCases.Obj(("storyboards",new JsonArray(CdpCases.Obj(("id","STB01"),("steps",steps)))),("state_svg_files",new JsonArray("screens/states/SCR01.S0.svg","screens/states/SCR01.S1.svg")));
        File.WriteAllBytes(Path.Combine(input,"docs/ui-asset-manifest.json"),JsonSerializer.SerializeToUtf8Bytes(manifest));return request;
    }
}
