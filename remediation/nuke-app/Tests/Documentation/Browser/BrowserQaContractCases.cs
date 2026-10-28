using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation.Browser;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation.Browser;

internal sealed record BrowserQaCase(string Id,string? Failure);
internal static partial class BrowserQaCases
{
    internal static readonly byte[] Pixel=Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jLucAAAAASUVORK5CYII=");
    internal static readonly IReadOnlyList<BrowserQaCase> All=[new("QA-good",null),new("QA-overflow","overflow"),new("QA-image","image"),new("QA-svg","svg"),new("QA-external","external"),new("QA-fonts","fonts"),new("QA-navigation","navigation")];
    public static IEnumerable<object[]> Cases=>All.Select(x=>new object[]{x.Id});
    internal static BrowserQaCase Get(string id)=>All.Single(x=>x.Id==id);
    internal static BrowserQaRequest Request(string root)=>new(Path.Combine(root,"input"),Path.Combine(root,"output"),new("/fixture/native-chromium",new string('a',64),"fixture-version"));
    internal static JsonObject Report(BrowserQaCase test)=>CdpCases.Obj(("producer","androidmcp-native-browser-qa-v1"),("scope","Offline design only; no Android or device/runtime proof"),
        ("expected_slides",3),("first",0),("next",test.Failure=="navigation"?0:1),("back",0),("font_status",test.Failure=="fonts"?"loading":"loaded"),
        ("embedded",new JsonArray(CdpCases.Obj(("step","STB01-1"),("state","SCR01.S0"),("sha256",ContentHash.Sha256(Encoding.UTF8.GetBytes(test.Failure=="image"?"not SVG":Svg()))),("matches_source",test.Failure!="image")),CdpCases.Obj(("step","STB01-2"),("state","SCR01.S1"),("sha256",ContentHash.Sha256(Encoding.UTF8.GetBytes(Svg()))),("matches_source",true)))),
        ("loaded",new JsonArray(CdpCases.Obj(("valid",test.Failure!="image"),("width",test.Failure=="image"?0:900),("height",test.Failure=="image"?0:720)),CdpCases.Obj(("valid",true),("width",900),("height",720)))),
        ("shots",new JsonArray(Shot(0,"cover",test.Failure=="overflow"),Shot(1,"STB01-1",false),Shot(2,"STB01-2",false))),
        ("svg_checks",new JsonArray(Svg("docs/diagrams/fixture.svg",false),Svg("docs/screens/states/SCR01.S0.svg",test.Failure=="svg"),Svg("docs/screens/states/SCR01.S1.svg",false),Svg("docs/screens/steps/STB01-1.svg",false),Svg("docs/screens/steps/STB01-2.svg",false))),
        ("contacts",new JsonArray(CdpCases.Obj(("file","contacts/contact-00.png"),("slides",new JsonArray(0,1,2))))),
        ("external_requests",test.Failure=="external"?new JsonArray("https://blocked.example/fixture"):new JsonArray()),("pass",test.Failure is null));
    private static JsonObject Shot(int index,string step,bool overflow)=>CdpCases.Obj(("slide",index),("step",step),("file","slides/"+index.ToString("D3",System.Globalization.CultureInfo.InvariantCulture)+".png"),
        ("overflow",overflow?new JsonArray(CdpCases.Obj(("text","fixture overflow"))):new JsonArray()));
    private static JsonObject Svg(string path,bool outside)=>CdpCases.Obj(("file",path),("checked",3),("viewBox",new JsonArray(0,0,900,720)),
        ("outside",outside?new JsonArray(CdpCases.Obj(("tag","text"))):new JsonArray()));
    internal static async Task AssertContract(BrowserQaCase test,IBrowserQaOperation provider,IArtifactStore output,BrowserQaRequest request)
    {
        var before=Directory.EnumerateFiles(request.ProjectRoot,"*",SearchOption.AllDirectories).ToDictionary(x=>x,x=>ContentHash.Sha256(File.ReadAllBytes(x)),StringComparer.Ordinal);
        var result=await provider.RunAsync(request);Assert.Equal(test.Failure is null,result.Passed);Assert.Equal(3,result.Slides);Assert.Equal(2,result.Images);Assert.Equal(5,result.SvgFiles);
        var expected=Report(test);var bytes=await output.ReadAsync(result.Receipt.RelativePath);Assert.Equal(ContentHash.Sha256(bytes.Span),result.Receipt.Sha256);
        var report=JsonNode.Parse(bytes.Span);AndroidMcp.BuildTool.Tests.Sources.SourceExtractionContractCases.AssertSubset(expected,report);
        var launch=report!["launch"]!.AsObject();Assert.Equal(request.Browser.ExecutablePath,launch["executable"]!.GetValue<string>());Assert.Equal(request.Browser.Sha256,launch["sha256"]!.GetValue<string>());
        var argv=launch["argv"]!.AsArray().Select(x=>x!.GetValue<string>()).ToArray();
        Assert.Contains("--headless=new",argv);Assert.Contains("--remote-debugging-address=127.0.0.1",argv);Assert.Contains("--remote-debugging-port=0",argv);
        Assert.Contains("--user-data-dir="+Path.Combine(request.OutputRoot,"profile"),argv);Assert.DoesNotContain("--no-sandbox",argv);
        Assert.True(report["shutdown"]!["graceful_close_requested"]!.GetValue<bool>());Assert.False(report["shutdown"]!["stopped"]!.GetValue<bool>());Assert.Equal(0,report["shutdown"]!["exit_code"]!.GetValue<int>());
        Assert.Equal(test.Failure is null?Array.Empty<string>():new[]{test.Failure},result.Failures);
        foreach(var capture in result.Captures)
        {
            var image=await output.ReadAsync(capture.RelativePath);Assert.Equal(ContentHash.Sha256(image.Span),capture.Sha256);Assert.Equal("image/png",capture.MediaType);
            Assert.True(image.Length>=8);Assert.True(image.Span[..8].SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}));
        }
        Assert.Equal(new[]{"contacts/contact-00.png","diagrams/fixture.svg.png","slides/000.png","slides/001.png","slides/002.png"},result.Captures.Select(x=>x.RelativePath).Order(StringComparer.Ordinal));
        Assert.Equal(before.Keys.Order(StringComparer.Ordinal),Directory.EnumerateFiles(request.ProjectRoot,"*",SearchOption.AllDirectories).Order(StringComparer.Ordinal));
        foreach(var path in before.Keys)Assert.Equal(before[path],ContentHash.Sha256(File.ReadAllBytes(path)));
    }
}
internal sealed class ScriptedBrowserQa(BrowserQaCase test,IArtifactStore output,string? mutation=null):IBrowserQaOperation
{
    public BrowserQaRequest? Received {get;private set;}
    public async Task<BrowserQaResult> RunAsync(BrowserQaRequest request,CancellationToken cancellationToken=default)
    {
        Received=request;var report=BrowserQaCases.Report(test);if(mutation=="receipt-field")report.Remove("font_status");if(mutation=="navigation")report["next"]=99;
        report["launch"]=CdpCases.Obj(("executable",request.Browser.ExecutablePath),("sha256",request.Browser.Sha256),("argv",new JsonArray("--headless=new","--remote-debugging-address="+(mutation=="launch"?"0.0.0.0":"127.0.0.1"),"--remote-debugging-port=0","--user-data-dir="+Path.Combine(request.OutputRoot,"profile"))));
        report["shutdown"]=CdpCases.Obj(("graceful_close_requested",mutation!="shutdown"),("stopped",false),("exit_code",0));
        var captures=new List<StoredArtifact>();foreach(var path in new[]{"slides/000.png","slides/001.png","slides/002.png","contacts/contact-00.png","diagrams/fixture.svg.png"})
            captures.Add(await output.WriteAsync(new(path,mutation=="image-bytes"?"not a PNG"u8.ToArray():BrowserQaCases.Pixel,"image/png"),cancellationToken));
        var receipt=await output.WriteAsync(new("browser-qa.json",JsonSerializer.SerializeToUtf8Bytes(report),"application/json"),cancellationToken);
        if(mutation=="input-mutation")File.AppendAllText(Path.Combine(request.ProjectRoot,"docs/storyboards.html"),"changed");
        return new(receipt,mutation=="verdict"?test.Failure is not null:test.Failure is null,mutation=="slides"?2:3,mutation=="images"?1:2,mutation=="svg"?4:5,
            mutation=="capture-omission"?captures.Skip(1).ToArray():captures,mutation=="failure-list"?new[]{"wrong"}:test.Failure is null?[]:[test.Failure]);
    }
}
