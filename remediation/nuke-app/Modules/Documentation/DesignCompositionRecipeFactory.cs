using System.Text;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Documentation;

/// <summary>Domain composition only; stores, leases, final writes and commit are shared runtime concerns.</summary>
public sealed class DesignCompositionRecipeFactory(IDocumentationRendererProfileReader profiles,IModuleGraphValidator modules,
    IArchitectureAssetPreparation preparation,IArchitectureProjectionComposer renderer,IDocumentationToolBootstrap tools,
    IDocumentationPackageBuilder packages):IDesignCompositionRecipeFactory
{
    public ArtifactGenerationRecipe Architecture(DesignArchitectureRequest request)=>new(
        [new("architecture/workspace.dsl","text/plain"),new("architecture/code-diagrams.md","text/markdown"),new("architecture/module-contract.json","application/json"),new("architecture/render-skin.puml","text/plain"),new("toolchain-lock.json","application/json"),new("renderer-lock.json","application/json")],inputs=>
        {
            Artifact Input(string path)=>inputs.Single(a=>a.RelativePath==path);
            var profile=profiles.Read(Input("toolchain-lock.json"),Input("renderer-lock.json"));
            var code=Input("architecture/code-diagrams.md");
            var module=modules.Validate(new(JsonNode.Parse(Input("architecture/module-contract.json").Bytes.Span)!.AsObject(),Encoding.UTF8.GetString(code.Bytes.Span)));
            if(module.Issues.Count>0)throw new ArchitectureAssetException("MODULE_CONTRACT","Architecture module contract failed");
            var views=preparation.ExtractCodeViews(new(Input("architecture/workspace.dsl"),code));
            var plan=renderer.Plan(inputs,profile,views);
            return new(plan.OutputPaths,async token=>
            {
                var admitted=await tools.AcquireAsync(profile,request.ToolAccess,request.OfflineGradleChecksumProof,token);
                return await renderer.RenderAsync(plan,request.Renderer,admitted,token);
            });
        });
    public ArtifactGenerationRecipe Package(DesignPackageRequest request)=>new(
        request.Sources.Select(s=>new ArtifactSourceSpec(s.RelativePath,s.MediaType,s)).ToArray(),inputs=>
        {
            var package=packages.Build(new(inputs,request.RequiredPaths,request.Policy));
            IReadOnlyList<Artifact> artifacts=[package.Archive,package.Checksums];
            return new(artifacts.Select(a=>a.RelativePath).ToArray(),_=>Task.FromResult(artifacts));
        });
}
