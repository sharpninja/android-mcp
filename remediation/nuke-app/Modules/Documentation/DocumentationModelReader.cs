using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Documentation;

/// <summary>The canonical blueprint parser shared by projection and design validation.</summary>
public sealed class DocumentationModelReader:IDocumentationModelReader
{
    public DocumentationModel Read(Artifact blueprintMarkdown)
    {
        var source=Encoding.UTF8.GetString(blueprintMarkdown.Bytes.Span);
        var blocks=Regex.Matches(source,"```json ui-blueprint-v2\\r?\\n([\\s\\S]*?)\\r?\\n```");
        if(blocks.Count!=1)throw new InvalidDataException("Exactly one canonical ui-blueprint-v2 block is required");
        var blueprint=JsonNode.Parse(blocks[0].Groups[1].Value)!.AsObject();
        if((blueprint["schema_version"]?.GetValue<string>()??"")!="2.0.0")throw new InvalidDataException("Unsupported UI blueprint schema");
        var screens=blueprint["screens"]!.AsArray().Select(n=>n!.AsObject()).ToArray();
        var stories=blueprint["storyboards"]!.AsArray().Select(n=>n!.AsObject()).ToArray();
        var screenIndex=screens.ToDictionary(s=>s["id"]?.GetValue<string>()??"",StringComparer.Ordinal);
        return new(blueprint,screens,stories,screenIndex);
    }
}
