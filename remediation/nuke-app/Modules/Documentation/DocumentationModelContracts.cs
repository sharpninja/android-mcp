using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Documentation;

/// <summary>One parsed canonical blueprint, with indexes referencing the same JSON nodes.</summary>
public sealed record DocumentationModel(JsonObject Blueprint,IReadOnlyList<JsonObject> Screens,
    IReadOnlyList<JsonObject> Storyboards,IReadOnlyDictionary<string,JsonObject> ScreensById);
public interface IDocumentationModelReader
{
    DocumentationModel Read(Artifact blueprintMarkdown);
}
