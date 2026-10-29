using System.Text.Json.Serialization;
using AndroidMcp.BuildTool.Publication;

namespace AndroidMcp.BuildTool.Documentation.Validation;

// The shared filesystem caller owns complete root enumeration and entry
// admission. This service consumes that snapshot and the existing bound store.
public sealed record RepositoryDocumentationCheckResult(
    [property:JsonPropertyName("checksums_checked")]int ChecksumsChecked,
    [property:JsonPropertyName("markdown_links_checked")]int MarkdownLinksChecked,
    [property:JsonPropertyName("rendered_svg_count")]int RenderedSvgCount,
    [property:JsonPropertyName("editable_ui_copies")]int EditableUiCopies,
    [property:JsonPropertyName("errors")]IReadOnlyList<string> Errors)
{
    [JsonIgnore]public int ExitCode=>Errors.Count==0?0:1;
}
public interface IRepositoryDocumentationValidator
{
    Task<RepositoryDocumentationCheckResult> CheckAsync(IReadOnlyList<PublicationFile> entries,CancellationToken cancellationToken=default);
}
