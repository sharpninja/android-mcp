using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Documentation.Browser;

public sealed class BrowserQaOperation(IProcessSessionFactory sessions,IProcessRunner processes,IClock clock):IBrowserQaOperation
{
    private static readonly CanonicalJson Canonical=new();
    private static JsonNode? Node<T>(T value)=>JsonSerializer.SerializeToNode(value);
    private static string Text(JsonNode value,string key)=>value[key]!.GetValue<string>();
    private static string Relative(string root,string path)=>Path.GetRelativePath(root,path).Replace(Path.DirectorySeparatorChar,'/');
    private static IArtifactStore Store(string root)=>new FileArtifactStore(root,bytes=>ContentHash.Sha256(bytes.Span));
    private static Task<StoredArtifact> JsonAsync(IArtifactStore store,string path,JsonObject value,CancellationToken token)=>store.WriteAsync(new(path,Canonical.Serialize(JsonSerializer.SerializeToElement(value)),"application/json"),token);

    // A hash-pinned native version query, also used by the host test fixture to
    // bind the same cases to the installed tool without guessing its version.
    public async Task<ToolIdentity> ProbeAsync(ToolIdentity tool,string workingDirectory,string outputRoot,CancellationToken cancellationToken=default)
    {
        var request=new ProcessRequest(tool,["--version"],workingDirectory,new Dictionary<string,string>(),Path.Combine(outputRoot,"process","version"),ClearEnvironment:true);
        var result=await processes.RunAsync(request,cancellationToken);var reader=new ProcessCaptureReader(outputRoot);
        var stdout=await reader.ReadAsync(result,false,cancellationToken);var stderr=await reader.ReadAsync(result,true,cancellationToken);
        if(result.Cancelled||result.ExitCode!=0)throw new IOException("Native Chromium version probe failed; captures retained: "+result.StandardErrorPath);
        var version=Encoding.UTF8.GetString(stdout.Span).Trim();if(version.Length==0)throw new IOException("Native Chromium version probe returned no version.");
        var identity=tool with{Version=version};var store=Store(outputRoot);
        await JsonAsync(store,"browser-tool.json",new(){["identity"]=Node(identity),["request"]=Node(request),["result"]=Node(result),["stdout_sha256"]=ContentHash.Sha256(stdout.Span),["stderr_sha256"]=ContentHash.Sha256(stderr.Span)},cancellationToken);
        await store.WriteAsync(new("browser-identity.json",Canonical.Serialize(JsonSerializer.SerializeToElement(identity)),"application/json"),cancellationToken);
        return identity;
    }
    public Task<BrowserQaResult> RunAsync(BrowserQaRequest request,CancellationToken cancellationToken=default)=>request.GenerationSelection is null
        ?RunCoreAsync(request,null,cancellationToken)
        :new BrowserQaGenerationVerifier().AdmitAndRunAsync(request,(admitted,token)=>RunCoreAsync(request,admitted,token),cancellationToken);
    private async Task<BrowserQaResult> RunCoreAsync(BrowserQaRequest request,BrowserQaGenerationAdmission? admitted,CancellationToken cancellationToken)
    {
        var inputRoot=Path.GetFullPath(request.ProjectRoot);var outputRoot=Path.GetFullPath(request.OutputRoot);
        if(request.RendererProfile!="native"||request.ViewportWidth<=0||request.ViewportHeight<=0||request.ExpectedArchitectureDiagrams<0)throw new ArgumentException("Browser QA requires a native renderer, positive viewport dimensions and an explicit nonnegative diagram denominator.");
        var outputRelative=Path.GetRelativePath(inputRoot,outputRoot).Replace(Path.DirectorySeparatorChar,'/');
        if(request.GenerationSelection is null&&(outputRelative=="."||ArtifactPaths.IsRelative(outputRelative)))throw new ArgumentException("Browser QA output must be outside its immutable input tree.");
        if(request.GenerationSelection is null&&Directory.Exists(outputRoot)&&Directory.EnumerateFileSystemEntries(outputRoot).Any())throw new IOException("Browser QA output generation must be fresh.");
        _=ArtifactPaths.Resolve(outputRoot,"browser-qa.json");
        var store=Store(outputRoot);var captures=new List<StoredArtifact>();BrowserSession? browser=null;
        try
        {
            var manifestPath=ArtifactPaths.Resolve(inputRoot,"docs/ui-asset-manifest.json");var htmlPath=ArtifactPaths.Resolve(inputRoot,"docs/storyboards.html");
            var manifestBytes=await File.ReadAllBytesAsync(manifestPath,cancellationToken);_=Canonical.Serialize(manifestBytes);var manifest=JsonNode.Parse(manifestBytes,documentOptions:new JsonDocumentOptions{MaxDepth=int.MaxValue})!.AsObject();
            var steps=manifest["storyboards"]!.AsArray().SelectMany(x=>x!["steps"]!.AsArray()).Select(x=>x!.AsObject()).ToArray();
            var svgFiles=admitted?.SvgFiles.ToArray()??Directory.EnumerateFiles(inputRoot,"*.svg",SearchOption.AllDirectories)
                .Select(path=>new BrowserQaGenerationSvg(inputRoot,Relative(inputRoot,path),"",0,"ui")).OrderBy(file=>file.RelativePath,StringComparer.Ordinal).ToArray();
            foreach(var file in svgFiles)_=file.AbsolutePath;
            foreach(var state in manifest["state_svg_files"]!.AsArray())if(!File.Exists(ArtifactPaths.Resolve(inputRoot,"docs/"+state!.GetValue<string>())))throw new IOException("Declared state SVG is absent: "+state);
            var inputFiles=svgFiles.Select(file=>(Path:file.AbsolutePath,Label:file.RelativePath,Role:file.Role)).Concat(new[]{manifestPath,htmlPath}.Select(path=>(Path:path,Label:Relative(inputRoot,path),Role:"ui"))).OrderBy(file=>file.Label,StringComparer.Ordinal);
            var inputHashes=new JsonArray();foreach(var file in inputFiles){var bytes=await File.ReadAllBytesAsync(file.Path,cancellationToken);var identity=new JsonObject{["path"]=file.Label,["sha256"]=ContentHash.Sha256(bytes),["bytes"]=bytes.LongLength};if(admitted is not null)identity["source_role"]=file.Role;inputHashes.Add(identity);}
            Directory.CreateDirectory(outputRoot);var tool=await ProbeAsync(request.Browser,outputRoot,outputRoot,cancellationToken);
            if(tool.Version!=request.Browser.Version)throw new IOException("Chromium version differs from its supplied content identity.");
            browser=await BrowserSession.OpenAsync(request with{ProjectRoot=inputRoot,OutputRoot=outputRoot},sessions,store,clock,cancellationToken);
            var failures=new HashSet<string>(StringComparer.Ordinal);var report=new JsonObject{["producer"]="androidmcp-native-browser-qa-v1",["scope"]="Offline design only; no Android or device/runtime proof",["at"]=clock.Now.ToString("o"),["inputs"]=inputHashes,["tool"]=Node(tool),["expected_slides"]=steps.Length+1};
            if(admitted is not null){report["generation_scope"]=admitted.Scope;report["generation_inputs"]=Node(admitted.Generations);report["verified_architecture_paths"]=Node(admitted.SvgFiles.Where(file=>file.Role=="architecture").Select(file=>file.RelativePath).ToArray());}
            await browser.NavigateAsync(htmlPath,cancellationToken);var ready=await browser.EvaluateAsync("ready",null,cancellationToken);
            report["font_status"]=ready["font_status"]!.DeepClone();if(Text(ready,"font_status")!="loaded")failures.Add("fonts");
            if(ready["sections"]!.GetValue<int>()!=steps.Length+1)failures.Add("slide-count");
            var sources=ready["sources"]!.AsArray();var embedded=new JsonArray();
            if(sources.Count!=steps.Length)failures.Add("image");
            for(var i=0;i<steps.Length;i++)
            {
                var step=Text(steps[i],"step_id");var source=await File.ReadAllBytesAsync(ArtifactPaths.Resolve(inputRoot,"docs/screens/steps/"+step+".svg"),cancellationToken);
                byte[]? actual=null;string? error=null;
                try{var data=i<sources.Count?sources[i]!.GetValue<string>():"";const string prefix="data:image/svg+xml;base64,";if(!data.StartsWith(prefix,StringComparison.Ordinal))throw new FormatException("Image is not an embedded base64 SVG.");actual=Convert.FromBase64String(data[prefix.Length..]);}
                catch(FormatException invalid){error=invalid.Message;}
                var matches=actual is not null&&actual.AsSpan().SequenceEqual(source);if(!matches)failures.Add("image");
                embedded.Add(new JsonObject{["step"]=step,["state"]=Text(steps[i],"state_id"),["sha256"]=actual is null?null:ContentHash.Sha256(actual),["source_sha256"]=ContentHash.Sha256(source),["matches_source"]=matches,["error"]=error});
            }
            report["embedded"]=embedded;report["loaded"]=ready["loaded"]!.DeepClone();
            if(ready["loaded"]!.AsArray().Any(x=>!x!["valid"]!.GetValue<bool>()))failures.Add("image");
            var first=(await browser.EvaluateAsync("active",null,cancellationToken))["active"]!.GetValue<int>();
            await browser.KeyAsync("ArrowRight",39,cancellationToken);var next=(await browser.EvaluateAsync("active",null,cancellationToken))["active"]!.GetValue<int>();
            await browser.KeyAsync("ArrowLeft",37,cancellationToken);var back=(await browser.EvaluateAsync("active",null,cancellationToken))["active"]!.GetValue<int>();
            report["first"]=first;report["next"]=next;report["back"]=back;if(first!=0||next!=1||back!=0)failures.Add("navigation");
            var shots=new JsonArray();var pngs=new List<byte[]>();
            for(var i=0;i<=steps.Length;i++)
            {
                if(i>0)await browser.KeyAsync("ArrowRight",39,cancellationToken);
                var current=(await browser.EvaluateAsync("active",null,cancellationToken))["active"]!.GetValue<int>();var forced=current!=i;
                if(forced){failures.Add("navigation");await browser.EvaluateAsync("select",new(){["index"]=i},cancellationToken);}
                var geometry=await browser.EvaluateAsync("overflow",null,cancellationToken);if(geometry["overflow"]!.AsArray().Count>0)failures.Add("overflow");
                var png=await browser.ScreenshotAsync(false,cancellationToken);pngs.Add(png);var path="slides/"+i.ToString("D3",CultureInfo.InvariantCulture)+".png";var saved=await SavePngAsync(store,path,png,captures,cancellationToken);
                shots.Add(new JsonObject{["slide"]=i,["step"]=i==0?"cover":Text(steps[i-1],"step_id"),["file"]=path,["sha256"]=saved.Sha256,["forced_capture_selection"]=forced,["overflow"]=geometry["overflow"]!.DeepClone()});
            }
            report["shots"]=shots;var checks=new JsonArray();var diagrams=0;await browser.ViewportAsync(1440,1000,cancellationToken);
            foreach(var file in svgFiles)
            {
                await browser.NavigateAsync(file.AbsolutePath,cancellationToken);var geometry=await browser.EvaluateAsync("svg",null,cancellationToken);geometry["file"]=file.RelativePath;
                geometry["sha256"]=ContentHash.Sha256(await File.ReadAllBytesAsync(file.AbsolutePath,cancellationToken));if(geometry["outside"]!.AsArray().Count>0)failures.Add("svg");
                if(file.RelativePath.StartsWith("docs/diagrams/",StringComparison.Ordinal))
                {diagrams++;var saved=await SavePngAsync(store,"diagrams/"+file.RelativePath["docs/diagrams/".Length..]+".png",await browser.ScreenshotAsync(true,cancellationToken),captures,cancellationToken);geometry["screenshot"]=saved.RelativePath;}
                checks.Add(geometry.DeepClone());
            }
            report["svg_checks"]=checks;report["diagrams"]=diagrams;report["expected_diagrams"]=request.ExpectedArchitectureDiagrams;if(diagrams!=request.ExpectedArchitectureDiagrams)failures.Add("diagram-count");
            var contacts=new JsonArray();
            for(var start=0;start<pngs.Count;start+=12)
            {
                var images=new JsonArray();var indices=new JsonArray();for(var j=start;j<Math.Min(start+12,pngs.Count);j++){images.Add(new JsonObject{["slide"]=j,["png"]=Convert.ToBase64String(pngs[j])});indices.Add(j);}
                var rendered=await browser.EvaluateAsync("contact",new(){["images"]=images},cancellationToken);var path="contacts/contact-"+(start/12).ToString("D2",CultureInfo.InvariantCulture)+".png";
                var saved=await SavePngAsync(store,path,Convert.FromBase64String(Text(rendered,"png")),captures,cancellationToken);contacts.Add(new JsonObject{["file"]=path,["slides"]=indices,["sha256"]=saved.Sha256});
            }
            report["contacts"]=contacts;await browser.CloseAsync(cancellationToken);
            if(admitted is not null)await new BrowserQaGenerationVerifier().VerifyUnchangedAsync(admitted,cancellationToken);
            report["external_requests"]=Node(browser.ExternalRequests);if(browser.ExternalRequests.Count>0)failures.Add("external");
            report["launch"]=new JsonObject{["executable"]=browser.Launch.Tool.ExecutablePath,["sha256"]=browser.Launch.Tool.Sha256,["argv"]=Node(browser.Launch.Arguments),["request"]=Node(browser.Launch)};
            report["shutdown"]=new JsonObject{["graceful_close_requested"]=true,["close_acknowledged"]=browser.CloseAcknowledged,["expected_transport_end"]=browser.ExpectedShutdownTransportEnd,["terminal_transport_code"]=browser.TransportErrorCode,["terminal_transport_error"]=browser.EventsError?.ToString(),["stopped"]=browser.Stopped,["exit_code"]=browser.Exit!.ExitCode,["result"]=Node(browser.Exit)};
            if(browser.Exit.ExitCode!=0||browser.Exit.Cancelled)failures.Add("process");
            report["protocol"]=Node(browser.Protocol);report["captures"]=Node(captures);report["failures"]=Node(failures.Order(StringComparer.Ordinal).ToArray());report["pass"]=failures.Count==0;
            var receipt=await JsonAsync(store,"browser-qa.json",report,cancellationToken);return new(receipt,failures.Count==0,steps.Length+1,sources.Count,svgFiles.Length,captures,failures.Order(StringComparer.Ordinal).ToArray());
        }
        catch(Exception error)
        {
            if(browser is not null)await browser.DisposeAsync();
            await JsonAsync(store,"browser-failure.json",new(){["at"]=clock.Now.ToString("o"),["request"]=Node(request),["exception"]=error.ToString(),["captures"]=Node(captures),["launch"]=Node(browser?.Launch),["process"]=Node(browser?.Exit),["protocol"]=Node(browser?.Protocol)},CancellationToken.None);throw;
        }
        finally{if(browser is not null)await browser.DisposeAsync();}
    }
    private static async Task<StoredArtifact> SavePngAsync(IArtifactStore store,string path,byte[] png,List<StoredArtifact> captures,CancellationToken token)
    {
        if(png.Length<8||!png.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}))throw new IOException("Chromium returned a non-PNG capture.");
        var saved=await store.WriteAsync(new(path,png,"image/png"),token);captures.Add(saved);return saved;
    }
}
