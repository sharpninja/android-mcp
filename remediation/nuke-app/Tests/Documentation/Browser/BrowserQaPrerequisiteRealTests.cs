using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation.Browser;
using AndroidMcp.BuildTool.Tests.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation.Browser;

// Real generation admission and dispatch barrier; the admitted callback records
// the QA invocation. Actual Chromium is exercised separately by the same7 CLI
// cases, rather than attributing a browser process to these preflight tests.
public sealed class NativeBrowserQaPrerequisiteProvider:IBrowserQaPrerequisiteCaseProvider
{
    private static string Rewrite(string root,Action<JsonObject> change)
    {
        var path=Path.Combine(root,"generation-manifest.json");var document=JsonNode.Parse(File.ReadAllBytes(path))!.AsObject();change(document);
        var bytes=JsonSerializer.SerializeToUtf8Bytes(document);File.WriteAllBytes(path,bytes);return ContentHash.Sha256(bytes);
    }
    public BrowserQaPrerequisiteReply Run(BrowserQaPrerequisiteCall call)
    {
        var root=ProcessSessionFixturePaths.NewRoot("browser-prerequisite-tests");var fixture=BrowserQaCliCases.Input(root,BrowserQaCases.Get("QA-good"));
        var selection=new BrowserQaGenerationSelection(fixture.Request.ProjectRoot,fixture.UiManifestSha256,fixture.ArchitectureRoot,fixture.ArchitectureManifestSha256);
        var request=fixture.Request with{ExpectedArchitectureDiagrams=call.ExpectedArchitectureDiagrams};
        var manifest=Path.Combine(request.ProjectRoot,"generation-manifest.json");
        switch(call.Variant)
        {
            case "split":break;
            case "ui-only":case "missing-architecture":selection=selection with{ArchitectureRoot=null,ArchitectureManifestSha256=null};break;
            case "missing-manifest":
                request=request with{ProjectRoot=Path.Combine(root,"missing-generation")};Directory.CreateDirectory(request.ProjectRoot);selection=selection with{ProjectRoot=request.ProjectRoot};break;
            case "manifest-pin":selection=selection with{UiManifestSha256=new string('0',64)};break;
            case "artifact-pin":File.AppendAllText(Path.Combine(request.ProjectRoot,"docs/storyboards.html"),"changed fixture");break;
            case "missing-html":File.Delete(Path.Combine(request.ProjectRoot,"docs/storyboards.html"));break;
            case "malformed-manifest":
                var invalid=JsonSerializer.SerializeToUtf8Bytes(new{});File.WriteAllBytes(manifest,invalid);selection=selection with{UiManifestSha256=ContentHash.Sha256(invalid)};break;
            case "output-overlap":request=request with{OutputRoot=Path.Combine(fixture.ArchitectureRoot,"forbidden-output")};break;
            case "populated-output":Directory.CreateDirectory(request.OutputRoot);File.WriteAllText(Path.Combine(request.OutputRoot,"retained.txt"),"existing output");break;
            case "undeclared-svg":File.WriteAllText(Path.Combine(request.ProjectRoot,"docs/extra.svg"),BrowserQaCases.Svg());break;
            case "diagram-count":break;
            case "unsafe-path":
                selection=selection with{UiManifestSha256=Rewrite(request.ProjectRoot,document=>document["artifacts"]!.AsArray().Add(new JsonObject{["path"]="../outside.svg",["sha256"]=new string('a',64),["size_bytes"]=1,["media_type"]="image/svg+xml"}))};break;
            case "duplicate-label":
                var duplicate=Path.Combine(request.ProjectRoot,"docs/diagrams/fixture.svg");Directory.CreateDirectory(Path.GetDirectoryName(duplicate)!);File.Copy(Path.Combine(fixture.ArchitectureRoot,"docs/diagrams/fixture.svg"),duplicate);
                selection=selection with{UiManifestSha256=Rewrite(request.ProjectRoot,document=>document["artifacts"]!.AsArray().Add(new JsonObject{["path"]="docs/diagrams/fixture.svg",["sha256"]=ContentHash.Sha256(File.ReadAllBytes(duplicate)),["size_bytes"]=new FileInfo(duplicate).Length,["media_type"]="image/svg+xml"}))};break;
            default:throw new InvalidOperationException("Unknown fixture variant "+call.Variant);
        }
        request=request with{GenerationSelection=selection};
        var before=Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories).ToDictionary(path=>path,path=>ContentHash.Sha256(File.ReadAllBytes(path)),StringComparer.Ordinal);
        var existed=Directory.Exists(request.OutputRoot);var invocations=0;BrowserQaGenerationAdmission? admitted=null;string outcome="ok";
        try
        {
            new BrowserQaGenerationVerifier().AdmitAndRunAsync(request,(value,token)=>
            {
                token.ThrowIfCancellationRequested();invocations++;admitted=value;Directory.CreateDirectory(request.OutputRoot);File.WriteAllText(Path.Combine(request.OutputRoot,"admitted-callback.txt"),value.Scope);
                return Task.FromResult(true);
            }).GetAwaiter().GetResult();
        }
        catch(BrowserQaInputException error){outcome=error.Code;}
        var unchanged=before.All(pair=>File.Exists(pair.Key)&&ContentHash.Sha256(File.ReadAllBytes(pair.Key))==pair.Value);
        var addedInputs=Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories).Where(path=>!before.ContainsKey(path)&&!path.StartsWith(request.OutputRoot+Path.DirectorySeparatorChar,StringComparison.Ordinal)).Any();
        return new(outcome,invocations,!existed&&Directory.Exists(request.OutputRoot),unchanged&&!addedInputs,
            admitted?.SvgFiles.Count??0,admitted?.ArchitectureDiagrams??0,admitted?.Scope??"refused",
            admitted?.Generations.Any(generation=>generation.Role=="ui"&&generation.Manifest.Sha256==selection.UiManifestSha256)??false,
            admitted?.Generations.Any(generation=>generation.Role=="architecture"&&generation.Manifest.Sha256==selection.ArchitectureManifestSha256)??false);
    }
}
[Trait("Gate","Real")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class BrowserQaPrerequisiteRealTests
{
    [Theory,MemberData(nameof(BrowserQaPrerequisiteCases.Cases),MemberType=typeof(BrowserQaPrerequisiteCases))]
    public void NativeReadOnlyAdmissionUsesSameCaseAndInteractionAssertions(string id)
    {var test=BrowserQaPrerequisiteCases.Get(id);BrowserQaPrerequisiteCases.AssertContract(test,new NativeBrowserQaPrerequisiteProvider());}
}
