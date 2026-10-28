using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;

namespace AndroidMcp.BuildTool.Documentation;

public sealed record RendererFilePin(string RelativePath,string Sha256);
public sealed record DocumentationRendererProfile(string Image,string JavaVersion,string StructurizrVersion,
    string PlantUmlVersion,string GradleVersion,IReadOnlyList<SourcePin> Downloads,IReadOnlyList<RendererFilePin> RendererFiles,
    IReadOnlyDictionary<string,IReadOnlyList<Uri>>? ToolRedirects=null);
public interface IDocumentationRendererProfileReader
{
    DocumentationRendererProfile Read(Artifact toolchainLock,Artifact rendererLock);
}
