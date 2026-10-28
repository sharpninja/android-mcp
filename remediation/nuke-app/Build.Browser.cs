using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation.Browser;

namespace AndroidMcp.BuildTool;

public sealed partial class Build
{
    private async Task RunBrowserQa()
    {
        var project=Path.GetFullPath(ProjectRoot!);var output=Path.GetFullPath(OutputRoot!);
        var browser=await ReadToolIdentity(BrowserIdentity!,"Browser");
        var selection=new BrowserQaGenerationSelection(project,UiManifestSha256!,
            ArchitectureRoot is null?null:Path.GetFullPath(ArchitectureRoot),ArchitectureManifestSha256);
        var request=new BrowserQaRequest(project,output,browser,ExpectedArchitectureDiagrams:ExpectedArchitectureDiagrams!.Value,
            GenerationSelection:selection);
        if(ViewportWidth.HasValue)request=request with{ViewportWidth=ViewportWidth.Value};
        if(ViewportHeight.HasValue)request=request with{ViewportHeight=ViewportHeight.Value};
        var clock=new SystemClock();var processes=new NativeProcessRunner(clock);
        var result=await new BrowserQaOperation(processes,processes,clock).RunAsync(request);
        Console.WriteLine(JsonSerializer.Serialize(new{result.Passed,result.Slides,result.Images,result.SvgFiles,result.Receipt,
            CaptureCount=result.Captures.Count,result.Failures,OutputRoot=output}));
        if(!result.Passed)throw new InvalidOperationException("Browser QA failed: "+string.Join(", ",result.Failures)+"; retained receipt "+Path.Combine(output,result.Receipt.RelativePath));
    }
}
