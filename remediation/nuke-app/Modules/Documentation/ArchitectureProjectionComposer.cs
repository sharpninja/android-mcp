using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;

namespace AndroidMcp.BuildTool.Documentation;

/// <summary>Documentation recipe over the one shared immutable-generation protocol.</summary>
public sealed class ArchitectureProjectionComposer(IArtifactGenerationRuntime workRuntime,IArtifactStore work,
    IArtifactReceiptVerifier receipts,IArchitectureAssetPreparation preparation,IArchitectureRendererTransport renderer,
    IRendererImageAdmission admission,string admittedWorkGenerationId):IArchitectureProjectionComposer
{
    private const string ExportPrefix="build/structurizr-export/structurizr-";
    private const string ProfiledPrefix="build/structurizr-profiled/structurizr-";
    private const string DiagramPrefix="docs/diagrams/";
    private const string DockerConfig=".docker-cli/config.json";
    private static readonly Regex NativeView=new(@"(?m)^\s*(?:(?:systemContext|container|component|dynamic)\s+\S+|deployment\s+\S+\s+(?:""[^""]*""|\S+))\s+([A-Za-z_][A-Za-z0-9_]*)\s+""[^""]*""\s*\{",RegexOptions.CultureInvariant);

    public ArchitectureCompositionPlan Plan(IReadOnlyList<Artifact> inputs,DocumentationRendererProfile profile,IReadOnlyList<Artifact> codeViews)
    {
        var native=NativeKeys(inputs);
        var paths=codeViews.Select(a=>a.RelativePath).Concat(codeViews.Select(a=>DiagramPrefix+Path.GetFileNameWithoutExtension(a.RelativePath)+".svg"))
            .Concat(native.Select(key=>DiagramPrefix+"structurizr-"+key+".svg")).ToArray();
        return new(inputs,profile,codeViews,paths);
    }

    public async Task<IReadOnlyList<Artifact>> RenderAsync(ArchitectureCompositionPlan plan,ArchitectureRendererContext context,
        IReadOnlyList<ToolArtifactReceipt> admittedTools,CancellationToken cancellationToken=default)
    {
        // The host binds context.PlantUmlJar to the cache used by this already-admitted bootstrap result.
        ArgumentException.ThrowIfNullOrWhiteSpace(admittedWorkGenerationId);
        cancellationToken.ThrowIfCancellationRequested();
        var native=NativeKeys(plan.Inputs);var paths=WorkPaths(plan,native);
        var sources=plan.Inputs.Select(a=>new ArtifactSourceSpec(a.RelativePath,a.MediaType,receipts.Describe(a))).ToArray();
        var recipe=new ArtifactGenerationRecipe(sources,inputs=>new(paths.Keys.ToArray(),async token=>
        {
            foreach(var source in inputs.Concat(plan.CodeViews))await work.WriteAsync(source,token);
            await work.WriteAsync(new(DockerConfig,JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string,string>()),"application/json"),token);
            await admission.AdmitAsync(plan.Profile,context,token);
            await renderer.ExecuteAsync(plan.Profile,context,new(ArchitectureRendererPhase.RenderCode,plan.CodeViews.Select(a=>a.RelativePath).ToArray()),token);
            foreach(var code in plan.CodeViews)
            {
                var path=code.RelativePath[..^5]+".svg";
                var raw=new Artifact(path,await work.ReadAsync(path,token),"image/svg+xml");
                await work.WriteAsync(preparation.NormalizeVector(raw,DiagramPrefix+Path.GetFileNameWithoutExtension(code.RelativePath)+".svg"),token);
            }
            await renderer.ExecuteAsync(plan.Profile,context,new(ArchitectureRendererPhase.ValidateWorkspace,[]),token);
            await renderer.ExecuteAsync(plan.Profile,context,new(ArchitectureRendererPhase.ExportWorkspace,[]),token);
            foreach(var key in native)
            {
                var raw=await work.ReadAsync(ExportPrefix+key+".puml",token);
                await work.WriteAsync(new(ProfiledPrefix+key+".puml",ApplyExistingProfile(raw),"text/vnd.plantuml"),token);
            }
            await renderer.ExecuteAsync(plan.Profile,context,new(ArchitectureRendererPhase.RenderNative,native.Select(key=>ProfiledPrefix+key+".puml").ToArray()),token);
            foreach(var key in native)
            {
                var path=ProfiledPrefix+key+".svg";var raw=new Artifact(path,await work.ReadAsync(path,token),"image/svg+xml");
                await work.WriteAsync(preparation.NormalizeVector(raw,DiagramPrefix+"structurizr-"+key+".svg"),token);
            }
            var result=new List<Artifact>();
            foreach(var path in paths)result.Add(new(path.Key,await work.ReadAsync(path.Key,token),path.Value));
            return result;
        }));
        await workRuntime.ExecuteAsync(admittedWorkGenerationId,recipe,cancellationToken);
        var output=new List<Artifact>();
        foreach(var path in plan.OutputPaths)output.Add(new(path,await work.ReadAsync(path,cancellationToken),paths[path]));
        return output;
    }

    private static string[] NativeKeys(IReadOnlyList<Artifact> inputs)
    {
        var dsl=inputs.Single(a=>a.RelativePath=="architecture/workspace.dsl");
        var keys=NativeView.Matches(Encoding.UTF8.GetString(dsl.Bytes.Span)).Select(m=>m.Groups[1].Value).ToArray();
        if(keys.Length==0||keys.Distinct(StringComparer.Ordinal).Count()!=keys.Length)
            throw new ArchitectureAssetException("NATIVE_VIEW_PLAN","Native workspace view keys must be present and unique");
        return keys.Order(StringComparer.Ordinal).ToArray();
    }
    private static IReadOnlyDictionary<string,string> WorkPaths(ArchitectureCompositionPlan plan,IReadOnlyList<string> native)
    {
        var paths=plan.Inputs.Concat(plan.CodeViews).ToDictionary(a=>a.RelativePath,a=>a.MediaType,StringComparer.Ordinal);
        paths.Add(DockerConfig,"application/json");
        foreach(var code in plan.CodeViews){paths.Add(code.RelativePath[..^5]+".svg","image/svg+xml");paths.Add(DiagramPrefix+Path.GetFileNameWithoutExtension(code.RelativePath)+".svg","image/svg+xml");}
        foreach(var key in native)
        {
            paths.Add(ExportPrefix+key+".puml","text/vnd.plantuml");paths.Add(ProfiledPrefix+key+".puml","text/vnd.plantuml");
            paths.Add(ProfiledPrefix+key+".svg","image/svg+xml");paths.Add(DiagramPrefix+"structurizr-"+key+".svg","image/svg+xml");
        }
        return paths;
    }
    private static ReadOnlyMemory<byte> ApplyExistingProfile(ReadOnlyMemory<byte> raw)
    {
        const string include="!include ../../architecture/render-skin.puml";var text=Encoding.UTF8.GetString(raw.Span);
        if(text.Contains(include,StringComparison.Ordinal))return raw;
        var marker=text.IndexOf("@startuml",StringComparison.Ordinal);
        if(marker<0)throw new ArchitectureAssetException("NATIVE_EXPORT","Structurizr export has no PlantUML start marker");
        return Encoding.UTF8.GetBytes(text.Insert(marker+"@startuml".Length,"\n"+include));
    }
}
