using System.Text.Json;
using System.Text.Json.Serialization;

namespace AndroidMcp.BuildTool.Publication;

// Sole PlanCore projection proposal. No file read, proof parsing, filtering,
// source acquisition, or change to existing Plan/PlanSeed serialization.
public sealed record RehydrationCanonicalMetadataReference(
    bool ReceiptPathPresent,JsonValueKind ReceiptPathKind,string? ReceiptPath,
    string? PlatformVersion,bool PackagePathPresent,JsonValueKind PackagePathKind,string? PackagePath,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]
    RehydrationLegacyPlatformFacts? LegacyPlatformFacts=null);

// Append an optional `RehydrationCanonicalMetadataReference? MetadataReference=null`
// to RehydrationSnapshotRow. Emit ONLY for exact
// transport.kind == "bounded-sdk-zip-range-member"; keep absent/wrong receipt
// facts available for deferred caller errors, never a new Plan validation.
// PlatformVersion uses the original nonempty row.platform_api_version first,
// then transport.platform_api_version. Do not substitute transport.platform_version.
// Preserve package_path presence/kind: absent means default, null does not.
// Native platform_version can short-circuit row string admission. Optional
// detached legacy facts retain that already admitted nonstring domain without
// broadening Plan/Seed admission or changing their serialization/identity.
