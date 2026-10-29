using System.Text.Json.Serialization;

namespace AndroidMcp.BuildTool.Catalog;

// Catalog-only provided normalization. No source kind/context/repository/revision
// field exists. Native source evidence and OriginalRow remain unchanged.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CatalogSourceNormalization(int RowIndex,string ExpectedOriginalRowCanonicalSha256,
    long? ApiLevel,string Channel,string? License=null,string? Scope=null,string? RetrievedAt=null);
