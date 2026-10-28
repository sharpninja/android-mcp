using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Sources;

// Explicit input declaration. Only a registered provider with this marker can
// receive binary bytes before text UTF8 validation; never infer from filenames.
public interface IBinarySourceExtractor : ISourceExtractor { }

public interface IAnnotationTargetSpellingSourceFacts
{
    // Pure XML item spelling only. No scoped lookup, java.lang proof or binding.
    JsonObject NormalizeTarget(string itemName);
}
public interface ISdkAnnotationArchiveSourceFacts
{
    // Owned JSON concern in ExtractionResult.Metadata.annotation_archive.
    // No binding/declaration Entry rows are invented from this archive.
    JsonObject Parse(ReadOnlyMemory<byte> archive);
}
