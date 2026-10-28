using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Documentation;

/// <summary>Pure in-memory projections. File writes and operation planning belong to the shared host.</summary>
public sealed class DocumentationProjector : IDocumentationProjector
{
    private const string NavigationBegin = "<!-- GENERATED UI NAVIGATION BEGIN -->";
    private const string NavigationEnd = "<!-- GENERATED UI NAVIGATION END -->";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public DocumentationProjection Project(DocumentationInput input)
    {
        var source = Encoding.UTF8.GetString(input.BlueprintMarkdown.Bytes.Span);
        var blocks = Regex.Matches(source,"```json ui-blueprint-v2\\r?\\n([\\s\\S]*?)\\r?\\n```");
        if (blocks.Count != 1) throw new InvalidDataException("Exactly one canonical ui-blueprint-v2 block is required");
        var blueprint = JsonNode.Parse(blocks[0].Groups[1].Value)!.AsObject();
        if (Value(blueprint,"schema_version") != "2.0.0") throw new InvalidDataException("Unsupported UI blueprint schema");
        var screens = blueprint["screens"]!.AsArray().Select(n=>n!.AsObject()).ToArray();
        var stories = blueprint["storyboards"]!.AsArray().Select(n=>n!.AsObject()).ToArray();
        var screenIndex = screens.ToDictionary(s=>Value(s,"id"),StringComparer.Ordinal);
        var output = new Dictionary<string,Artifact>(StringComparer.Ordinal);
        var variantFiles = new List<string>();
        void Write(string path,string text,string mediaType)
        {
            if (!text.EndsWith('\n')) text += "\n";
            if (!output.TryAdd(path,new(path,Encoding.UTF8.GetBytes(text),mediaType)))
                throw new InvalidDataException("Duplicate projected output: " + path);
        }
        void WriteOverview(string id,string svg)
        {
            Write("docs/screens/"+id+".svg",svg,"image/svg+xml");
            Write("ui-svg/"+id+".svg",svg,"image/svg+xml");
        }
        foreach (var screen in screens)
        {
            var id=Value(screen,"id");
            foreach(var pair in screen["state_specs"]!.AsObject())
            {
                ValidateControlPartition(screen,pair.Key,pair.Value!.AsObject());
                var path="screens/states/"+pair.Key+".svg";
                Write("docs/"+path,RenderState(screen,pair.Key,null),"image/svg+xml");
                variantFiles.Add(path);
            }
            WriteOverview(id,RenderOverview(screen));
        }
        var reference=new StringBuilder("# Kotlin Compose screen definitions and branching storyboards\n\nCanonical behavior/state source: [UI blueprint](ui-blueprint.md). SVG exports retain vector text. These are design specifications; no Android UI exists yet. State-specific panels reflect exact enabled controls and health.\n\n");
        foreach(var screen in screens)
        {
            var id=Value(screen,"id");
            reference.Append("## ").Append(id).Append(" - ").Append(Value(screen,"title")).Append("\n\n").Append(Value(screen,"requirements")).Append("\n\n![Screen definition](screens/").Append(id).Append(".svg)\n\n");
            foreach(var pair in screen["control_specs"]!.AsObject())
            {
                var control=pair.Value!.AsObject();
                reference.Append("- **").Append(pair.Key).Append(' ').Append(screen["controls"]![pair.Key]!.GetValue<string>()).Append("** (").Append(Value(control,"kind")).Append("): ").Append(Value(control,"behavior")).Append(" Fields: ").Append(Value(control,"fields")).Append(". Failure: ").Append(Value(control,"error")).Append(" Cancel: ").Append(Value(control,"cancel")).Append('\n');
            }
            var phone=screen["phone_layout"]!.AsObject();
            reference.Append("\nPhone: ").Append(Value(phone,"content")).Append(' ').Append(Value(phone,"navigation")).Append(" Landscape: ").Append(Value(phone,"landscape")).Append("\n\n");
            foreach(var pair in screen["state_specs"]!.AsObject())
            {
                var state=pair.Value!.AsObject();
                reference.Append("- [").Append(pair.Key).Append("](screens/states/").Append(pair.Key).Append(".svg) **").Append(Value(state,"label")).Append("**. Effect: ").Append(Value(state,"effect")).Append(" Recovery: ").Append(Value(state,"recovery")).Append(" Disabled: ").Append(string.Join(", ",Strings(state["disabled_controls"]))).Append(".\n");
            }
        }
        var deck=new StringBuilder("---\nmarp: true\ntheme: default\npaginate: true\nsize: 16:9\n---\n\n# Kotlin Compose UI storyboards\n\nExplicit actor, state, action and branch IDs. Alternatives are labeled, not automatic retries. External events are observation anchors. Exact owner authorization and self-ADB authority stay separate.\n");
        var deckSteps=new List<(JsonObject Step,string Svg)>();
        foreach(var story in stories)
        {
            var id=Value(story,"id");var steps=story["steps"]!.AsArray().Select(n=>n!.AsObject()).ToArray();
            var composite=new VectorCanvas();
            composite.Text(id+" - "+Value(story,"title"),24,42,110,27);
            reference.Append("\n## ").Append(id).Append(" - ").Append(Value(story,"title")).Append("\n\n![Storyboard panels](screens/").Append(id).Append(".svg)\n\n").Append(Value(story,"boundary")).Append("\n\n");
            for(var i=0;i<steps.Length;i++)
            {
                var step=steps[i];var stepId=Value(step,"step_id");
                if(!screenIndex.TryGetValue(Value(step,"screen_id"),out var screen))throw new InvalidDataException("Unknown step screen: "+stepId);
                var stateId=Value(step,"state_id");var controlId=Value(step,"control_id");
                if(screen["state_specs"]![stateId] is not JsonObject state||screen["controls"]![controlId] is null)throw new InvalidDataException("Unknown step anchor: "+stepId);
                if(Value(step,"interaction")=="control"&&!Strings(state["enabled_controls"]).Contains(controlId,StringComparer.Ordinal))throw new InvalidDataException("Disabled storyboard control: "+stepId);
                var svg=RenderState(screen,stateId,controlId);if(!svg.EndsWith('\n'))svg+="\n";
                var focus="screens/steps/"+stepId+".svg";
                Write("docs/"+focus,svg,"image/svg+xml");variantFiles.Add(focus);deckSteps.Add((step,svg));
                var x=24+(i%3)*548;var y=75+(i/3)*680;
                var group=new XElement(VectorCanvas.Ns+"g",new XAttribute("data-step-id",stepId));
                group.Add(VectorCanvas.Rect(x,y,528,650,"white","#9bb2c9"));
                composite.Text(stepId+" · "+Value(step,"branch"),x+12,y+27,44,18,parent:group);
                composite.Text(Value(step,"actor")+": "+Value(step,"purpose"),x+12,y+77,62,14,parent:group);
                var nested=XElement.Parse(svg);var originalHeight=(string)nested.Attribute("height")!;
                nested.SetAttributeValue("x",x+12);nested.SetAttributeValue("y",y+130);nested.SetAttributeValue("width",504);nested.SetAttributeValue("height",480);nested.SetAttributeValue("viewBox","0 0 900 "+originalHeight);group.Add(nested);
                composite.Text("Success → "+Value(step,"next_success")+" · error → "+Value(step,"next_error"),x+12,y+628,64,12,parent:group);composite.Body.Add(group);
                reference.Append("### ").Append(stepId).Append(" - ").Append(Value(step,"branch")).Append("\n\n").Append(Value(step,"actor")).Append("; ").Append(stateId).Append("; ").Append(controlId).Append(' ').Append(Value(step,"action")).Append(". ").Append(Value(step,"purpose")).Append("\n\nSuccess: ").Append(Value(step,"result")).Append("\n\nError: ").Append(Value(step,"error")).Append("\n\nCancel: ").Append(Value(step,"cancel")).Append("\n\nTransitions: success ").Append(Value(step,"next_success")).Append("; error ").Append(Value(step,"next_error")).Append("; cancel ").Append(Value(step,"next_cancel")).Append(".\n\n");
                deck.Append("\n---\n\n# ").Append(stepId).Append(" · ").Append(Value(step,"purpose")).Append("\n\n![Screen](../docs/").Append(focus).Append(")\n\n**").Append(Value(step,"actor")).Append("** · `").Append(stateId).Append("`\n\n**Branch:** ").Append(Value(step,"branch")).Append("\n\n").Append(Value(step,"interaction")=="external_event"?"Observation: ":"Action: ").Append(controlId).Append(' ').Append(Value(step,"action")).Append("\n\n**Success:** ").Append(Value(step,"result")).Append("\n\n**Error:** ").Append(Value(step,"error")).Append("\n\n**Cancel:** ").Append(Value(step,"cancel")).Append("\n\nNext: ").Append(Value(step,"next_success")).Append("; error: ").Append(Value(step,"next_error")).Append("; cancel: ").Append(Value(step,"next_cancel")).Append(".\n");
            }
            WriteOverview(id,composite.Render(1680,100+((steps.Length+2)/3)*680,id+" ordered state panels with branch labels"));
        }
        var design=Encoding.UTF8.GetString(input.DesignMarkdown.Bytes.Span);
        var regionPattern=Regex.Escape(NavigationBegin)+"[\\s\\S]*?"+Regex.Escape(NavigationEnd);
        if(Regex.Matches(design,regionPattern).Count!=1)throw new InvalidDataException("Exactly one generated UI navigation region required");
        var navigation=new StringBuilder(NavigationBegin+"\n<!-- Generated by the native C# documentation projector from docs/ui-blueprint.md. -->\n\n| Screen ID | Definition / all current controls |\n| --- | --- |\n");
        foreach(var screen in screens)navigation.Append("| [").Append(Value(screen,"id")).Append("](screens/").Append(Value(screen,"id")).Append(".svg) ").Append(EscapeTable(Value(screen,"title"))).Append(" | ").Append(string.Join("; ",screen["controls"]!.AsObject().Select(p=>EscapeTable(p.Key+" "+p.Value!.GetValue<string>())))).Append(" |\n");
        navigation.Append("\n| Storyboard ID | Actor-aware flow |\n| --- | --- |\n");
        foreach(var story in stories)navigation.Append("| [").Append(Value(story,"id")).Append("](screens/").Append(Value(story,"id")).Append(".svg) | ").Append(EscapeTable(Value(story,"title"))).Append(" |\n");
        navigation.Append("\n").Append(NavigationEnd);
        Write("docs/Android MCP Bridge Design.md",Regex.Replace(design,regionPattern,_=>navigation.ToString()),"text/markdown");
        var manifest=(JsonObject)blueprint.DeepClone();manifest["state_svg_files"]=new JsonArray(variantFiles.Order(StringComparer.Ordinal).Select(p=>(JsonNode?)JsonValue.Create(p)).ToArray());
        Write("docs/ui-asset-manifest.json",manifest.ToJsonString(JsonOptions),"application/json");
        Write("docs/ui-screens-and-storyboards.md",reference.ToString(),"text/markdown");
        Write("storyboards/storyboard.md",deck.ToString(),"text/markdown");
        Write("docs/storyboards.html",RenderHtml(deckSteps),"text/html");
        return new(output.Values.OrderBy(a=>a.RelativePath,StringComparer.Ordinal).ToArray());
    }

    private static string Value(JsonObject node,string key)=>node[key]?.GetValue<string>()??"";
    private static string[] Strings(JsonNode? node)=>node?.AsArray().Select(n=>n!.GetValue<string>()).ToArray()??[];
    private static string EscapeTable(string value)=>value.Replace("|","\\|",StringComparison.Ordinal);

    private static void ValidateControlPartition(JsonObject screen,string stateId,JsonObject state)
    {
        var controls=screen["controls"]!.AsObject().Select(p=>p.Key).Order(StringComparer.Ordinal).ToArray();
        var enabled=Strings(state["enabled_controls"]);var disabled=Strings(state["disabled_controls"]);
        if(enabled.Intersect(disabled,StringComparer.Ordinal).Any()||!enabled.Concat(disabled).Order(StringComparer.Ordinal).SequenceEqual(controls))throw new InvalidDataException("Incomplete state control partition: "+stateId);
    }

    // Health has one derivation shared by overview/state/step projection. All
    // state-specific values are canonical data; there are no state-ID exceptions.
    private static JsonObject Health(JsonObject screen,string stateId)
    {
        var health=(JsonObject)screen["health_example"]!.DeepClone();
        health["pause_reason"]=null;
        var suffix=stateId.LastIndexOf(".S",StringComparison.Ordinal);
        if(suffix<0||!int.TryParse(stateId.AsSpan(suffix+2),NumberStyles.None,CultureInfo.InvariantCulture,out var ordinal))throw new InvalidDataException("Invalid state revision identity: "+stateId);
        health["revision"]=checked(health["revision"]!.GetValue<int>()+ordinal);
        if(screen["state_specs"]![stateId]!["health"] is JsonObject overrides)
            foreach(var pair in overrides)health[pair.Key]=pair.Value?.DeepClone();
        return health;
    }

    private static string HealthLabel(JsonObject health)=>"Service "+Value(health,"service")+(health["pause_reason"]is null?"":" / "+Value(health,"pause_reason"))+" (desired "+Value(health,"desired_state")+") · MCP "+Value(health,"mcp")+" · ADB "+Value(health,"adb")+" · view "+Value(health,"device_view")+" · catalog "+Value(health,"catalog")+" · ARC "+Value(health,"arc_listener")+" · "+health["revision"]!.GetValue<int>().ToString(CultureInfo.InvariantCulture);

    private static string RenderState(JsonObject screen,string stateId,string? focus)
    {
        var id=Value(screen,"id");var state=screen["state_specs"]![stateId]!.AsObject();var health=Health(screen,stateId);var canvas=new VectorCanvas();
        var y=canvas.Text(id+" - "+Value(screen,"title"),26,40,58,27);y=canvas.Text(stateId+" · "+Value(state,"label"),26,y+9,80,20);
        var status=new XElement(VectorCanvas.Ns+"g",new XAttribute("data-status-id",id+".STATUS"),new XAttribute("data-health-snapshot",health.ToJsonString()));
        var statusHeight=VectorCanvas.Wrap(HealthLabel(health),104).Count*19;
        status.Add(VectorCanvas.Rect(26,y+3,848,statusHeight+20,Value(health,"service")=="Error"?"#ffe6e4":"#e3f1ec"));canvas.Text(HealthLabel(health),38,y+25,104,14,parent:status);canvas.Body.Add(status);y+=statusHeight+45;
        if(id=="SCR09")y=canvas.Text("Protected local owner surface · no remote approve/navigation · global dispatch pause only while a challenge remains pending",26,y,95,17,"#9a3323");
        foreach(var field in Strings(state["preview_fields"]??state["fields"]))y=canvas.Text(field,26,y,100,15);
        y+=14;
        var disabled=Strings(state["disabled_controls"]);
        if(focus is not null)
        {
            var control=screen["control_specs"]![focus]!.AsObject();var off=disabled.Contains(focus,StringComparer.Ordinal);
            y=canvas.Text(focus+" · "+screen["controls"]![focus]!.GetValue<string>(),26,y,78,21);
            y=canvas.Text(off?"Disabled local control in this state; external event is a status observation only":"Enabled subject to exact local/backend authority gates",26,y,98,16,off?"#7a4b36":"#285a46");
            y=canvas.Text(Value(control,"fields"),26,y+4,98,16);y=canvas.Text(Value(control,"behavior"),26,y+5,98,17);
        }
        else
        {
            var controls=screen["controls"]!.AsObject().ToArray();
            for(var i=0;i<controls.Length;i+=2)
            {
                var rowHeight=0;
                for(var j=i;j<Math.Min(i+2,controls.Length);j++)
                {
                    var (cid,label)=controls[j];var off=disabled.Contains(cid,StringComparer.Ordinal);var x=26+(j%2)*432;
                    var lines=VectorCanvas.Wrap(cid+" · "+label!.GetValue<string>(),39);var height=Math.Max(67,lines.Count*21+31);rowHeight=Math.Max(rowHeight,height);
                    var group=new XElement(VectorCanvas.Ns+"g",new XAttribute("data-control-id",cid),new XAttribute("data-enabled",off?"false":"true"));group.Add(VectorCanvas.Rect(x,y,416,height,off?"#e1e5e9":"#ffffff",off?"#a9b0b7":"#a2bad1"));
                    canvas.Text(cid+" · "+label!.GetValue<string>(),x+10,y+23,39,17,off?"#65707b":"#203950",group);
                    canvas.Text(off?"Disabled in this state":Value(screen["control_specs"]![cid]!.AsObject(),"kind"),x+10,y+height-8,100,12,"#586878",group);canvas.Body.Add(group);
                }
                y+=rowHeight+9;
            }
        }
        y=canvas.Text("Effect: "+Value(state,"effect"),26,y+15,99,17);y=canvas.Text("Recovery: "+Value(state,"recovery"),26,y+8,99,17);
        y=canvas.Text(Value(screen["phone_layout"]!.AsObject(),"navigation"),26,y+12,108,14);y=canvas.Text("Design only · local control/state contract; no Android implementation or authority granted",26,y+10,108,13);
        return canvas.Render(900,y+20,stateId+" "+Value(state,"label"),new Dictionary<string,string>{{"data-screen-state",stateId}}.Concat(focus is null?[]:new[]{new KeyValuePair<string,string>("data-focus-control",focus)}));
    }

    private static string RenderOverview(JsonObject screen)
    {
        var id=Value(screen,"id");var canvas=new VectorCanvas();var y=canvas.Text(id+" - "+Value(screen,"title"),26,44,108,29);
        y=canvas.Text("Kotlin Compose screen definition · "+Value(screen,"requirements")+" · blueprint2.0 design only",26,y+8,125,17);
        var health=screen["health_example"]!.AsObject();var status=new XElement(VectorCanvas.Ns+"g",new XAttribute("data-status-id",id+".STATUS"));var statusHeight=VectorCanvas.Wrap(HealthLabel(health),160).Count*21;
        status.Add(VectorCanvas.Rect(26,y+3,1628,statusHeight+20,Value(health,"service")=="Error"?"#ffe6e4":"#e3f1ec"));canvas.Text(HealthLabel(health),38,y+25,160,16,parent:status);canvas.Body.Add(status);y+=statusHeight+58;
        var phone=screen["phone_layout"]!.AsObject();y=canvas.Text("Phone390dp: "+Value(phone,"content")+" "+Value(phone,"navigation")+" Landscape: "+Value(phone,"landscape"),26,y,156,17);
        foreach(var pair in screen["controls"]!.AsObject())
        {
            var spec=screen["control_specs"]![pair.Key]!.AsObject();var start=y+10;var group=new XElement(VectorCanvas.Ns+"g",new XAttribute("data-control-id",pair.Key));
            var yy=canvas.Text(pair.Key+" · "+pair.Value!.GetValue<string>(),40,start+25,126,21,parent:group);yy=canvas.Text("Fields: "+Value(spec,"fields"),40,yy+4,151,17,parent:group);yy=canvas.Text(Value(spec,"behavior"),40,yy+2,151,17,parent:group);yy=canvas.Text("Failure: "+Value(spec,"error"),40,yy+2,151,16,parent:group);
            group.AddFirst(VectorCanvas.Rect(26,start,1628,yy-start+8,"#ffffff","#bdccdb"));canvas.Body.Add(group);y=yy+24;
        }
        y=canvas.Text("State definitions - exact enabled/disabled controls, effects and recovery",26,y+15,120,23);
        foreach(var pair in screen["state_specs"]!.AsObject())
        {
            var spec=pair.Value!.AsObject();var group=new XElement(VectorCanvas.Ns+"g",new XAttribute("data-state-id",pair.Key));var yy=canvas.Text(pair.Key+" · "+Value(spec,"label"),40,y+30,130,20,parent:group);var disabled=Strings(spec["disabled_controls"]);
            yy=canvas.Text("Disabled: "+(disabled.Length==0?"none; owner/backend exact command checks remain":string.Join(", ",disabled)),40,yy+4,150,16,parent:group);yy=canvas.Text("Effect: "+Value(spec,"effect"),40,yy+2,150,17,parent:group);yy=canvas.Text("Recovery: "+Value(spec,"recovery"),40,yy+2,150,17,parent:group);
            group.AddFirst(VectorCanvas.Rect(26,y+4,1628,yy-y+6,"#fff4e6","#dac6a6"));canvas.Body.Add(group);y=yy+22;
        }
        return canvas.Render(1680,y+20,id+" full screen/state definition");
    }

    private static string RenderHtml(IReadOnlyList<(JsonObject Step,string Svg)> steps)
    {
        static string H(string text)=>WebUtility.HtmlEncode(text);
        var html=new StringBuilder("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>Kotlin Compose UI storyboards</title><style>html,body{margin:0;background:#20354a;color:#20354a;font-family:'DejaVu Sans',sans-serif}section{box-sizing:border-box;position:relative;width:1280px;height:720px;background:white;padding:50px 60% 50px 60px;font-size:18px;line-height:1.25;display:none}section.active{display:block}section.cover{padding:60px}h1{font-size:26px;margin:0 0 .45em}p{margin:.5em 0}img[alt=Screen]{position:absolute;right:24px;top:50px;width:54%;height:620px;object-fit:contain}.controls{position:fixed;bottom:8px;left:12px;color:white;z-index:2}.controls button{margin-right:8px}#counter{margin-left:8px}@media print{html,body{background:white}section{display:block!important;break-after:page;page-break-after:always}.controls{display:none}@page{size:1280px 720px;margin:0}}</style></head><body><main><section class=\"cover active\" id=\"slide-1\" aria-hidden=\"false\"><h1>Kotlin Compose UI storyboards</h1><p>Explicit actor, state, action and branch IDs. Alternatives are labeled, not automatic retries. Each panel depicts the named state. External events are observation anchors, never remote button presses. Exact owner authorization and self-ADB authority stay separate.</p></section>");
        for(var i=0;i<steps.Count;i++)
        {
            var (step,svg)=steps[i];html.Append("<section id=\"slide-").Append(i+2).Append("\" data-step-id=\"").Append(H(Value(step,"step_id"))).Append("\" aria-hidden=\"true\"><h1>").Append(H(Value(step,"step_id")+" · "+Value(step,"purpose"))).Append("</h1><img alt=\"Screen\" src=\"data:image/svg+xml;base64,").Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(svg))).Append("\"><p><strong>").Append(H(Value(step,"actor"))).Append("</strong> · ").Append(H(Value(step,"state_id"))).Append("</p><p><strong>Branch:</strong> ").Append(H(Value(step,"branch"))).Append("</p><p>").Append(Value(step,"interaction")=="external_event"?"Observation: ":"Action: ").Append(H(Value(step,"control_id")+" "+Value(step,"action"))).Append("</p><p><strong>Success:</strong> ").Append(H(Value(step,"result"))).Append("</p><p><strong>Error:</strong> ").Append(H(Value(step,"error"))).Append("</p><p><strong>Cancel:</strong> ").Append(H(Value(step,"cancel"))).Append("</p><p>Next: ").Append(H(Value(step,"next_success"))).Append("; error: ").Append(H(Value(step,"next_error"))).Append("; cancel: ").Append(H(Value(step,"next_cancel"))).Append(".</p></section>");
        }
        html.Append("</main><nav class=\"controls\" aria-label=\"Slide navigation\"><button id=\"previous\" type=\"button\">Previous</button><button id=\"next\" type=\"button\">Next</button><span id=\"counter\" aria-live=\"polite\"></span></nav><script>'use strict';(()=>{const slides=Array.from(document.querySelectorAll('main>section'));let index=0;const previous=document.getElementById('previous'),next=document.getElementById('next'),counter=document.getElementById('counter');function show(n,updateHash=true){index=Math.max(0,Math.min(slides.length-1,n));slides.forEach((slide,i)=>{slide.classList.toggle('active',i===index);slide.setAttribute('aria-hidden',String(i!==index));});previous.disabled=index===0;next.disabled=index===slides.length-1;counter.textContent=`${index+1} / ${slides.length}`;if(updateHash)history.replaceState(null,'',`#${index+1}`);}function fromHash(){const n=Number(location.hash.slice(1));show(Number.isInteger(n)&&n>=1&&n<=slides.length?n-1:0,false);}previous.addEventListener('click',()=>show(index-1));next.addEventListener('click',()=>show(index+1));addEventListener('keydown',event=>{if(event.target instanceof HTMLInputElement||event.target instanceof HTMLTextAreaElement)return;if(['ArrowRight','PageDown',' '].includes(event.key)){event.preventDefault();show(index+1);}else if(['ArrowLeft','PageUp'].includes(event.key)){event.preventDefault();show(index-1);}else if(event.key==='Home'){event.preventDefault();show(0);}else if(event.key==='End'){event.preventDefault();show(slides.length-1);}});addEventListener('hashchange',fromHash);fromHash();})();</script></body></html>");
        return html.ToString();
    }

    private sealed class VectorCanvas
    {
        internal static readonly XNamespace Ns="http://www.w3.org/2000/svg";
        internal XElement Body { get; }=new(Ns+"g",new XAttribute("font-family","DejaVu Sans, sans-serif"));
        internal static IReadOnlyList<string> Wrap(string text,int width)
        {
            var lines=new List<string>();var current="";
            foreach(var word in Regex.Split(text,"\\s+"))
            {
                if(word.Length==0)continue;
                if(current.Length>0&&current.Length+word.Length+1>width){lines.Add(current);current=word;}
                else current+=(current.Length==0?"":" ")+word;
            }
            if(current.Length>0)lines.Add(current);return lines;
        }
        internal int Text(string value,int x,int y,int width=82,int size=18,string color="#203950",XElement? parent=null)
        {
            var lines=Wrap(value,width);for(var i=0;i<lines.Count;i++)(parent??Body).Add(new XElement(Ns+"text",new XAttribute("x",x),new XAttribute("y",y+i*(size+5)),new XAttribute("font-size",size),new XAttribute("fill",color),lines[i]));return y+lines.Count*(size+5);
        }
        internal static XElement Rect(int x,int y,int width,int height,string fill,string? stroke=null)
        {var r=new XElement(Ns+"rect",new XAttribute("x",x),new XAttribute("y",y),new XAttribute("width",width),new XAttribute("height",height),new XAttribute("rx",6),new XAttribute("fill",fill));if(stroke is not null)r.SetAttributeValue("stroke",stroke);return r;}
        internal string Render(int width,int height,string title,IEnumerable<KeyValuePair<string,string>>? attributes=null)
        {
            var root=new XElement(Ns+"svg",new XAttribute("width",width),new XAttribute("height",height),new XAttribute("viewBox",$"0 0 {width} {height}"),new XAttribute("role","img"));if(attributes is not null)foreach(var pair in attributes)root.SetAttributeValue(pair.Key,pair.Value);
            root.Add(new XElement(Ns+"title",title),Rect(0,0,width,height,"#f2f6fb"),Body);
            var text=new StringBuilder();using(var writer=XmlWriter.Create(text,new XmlWriterSettings{OmitXmlDeclaration=true,Indent=false,NewLineHandling=NewLineHandling.None}))root.WriteTo(writer);return text.ToString();
        }
    }
}
