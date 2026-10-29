using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed record ModuleMainCase(string Id, string Variant, string Group, string? Error = null);
public sealed record ModuleMainObservation(JsonObject? Report, IReadOnlyDictionary<string, byte[]> Files,
    IReadOnlyList<string> Reads, IReadOnlyList<string> Parses, IReadOnlyList<string> Writes,
    IReadOnlyList<string> DirectoryCalls, bool DirectoryExists, bool RetryRefused, string? Error,
    bool InputsRetained, bool ExistingRetained, bool TokensForwarded, IReadOnlyList<string> Profiles);
public interface IModuleMainProbe { Task<ModuleMainObservation> RunAsync(ModuleMainFixture fixture); }
public sealed class ModuleMainFixture
{
    public const string InventoryPath = HashMainFixture.InventoryPath, DeclaredPath = "source-inventory-release011.json";
    public const string RawPath = "raw/Android.bp", SecondRawPath = "raw/second.bp", Generation = "outside-corpus-audit/interface-module-facts-v1";
    public const string Reason = "nearest selected-interface build ancestor";
    public required ModuleMainCase Case { get; init; }
    public Dictionary<string, byte[]> Inputs { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, byte[]> Existing { get; } = new(StringComparer.Ordinal);
    public List<JsonObject> Facts { get; } = [];
    public JsonObject Counts { get; } = new();
    public InterfaceModuleWorkflowRequest Request { get; private set; } = null!;
    public string Text { get; private set; } = "";
    public string Profile => Case.Variant == "release" ? "release-011" : "current-candidate";
    public bool DirectoryCreated { get; private set; } = true;
    public bool ExistingDirectory => Case.Variant is "empty-dir" or "unrelated-dir";
    public bool PartialOutputs => Case.Variant is "write-facts" or "write-report" or "write-index" or "bad-receipt" or "inventory-change-after" or "raw-change-after" or "cancel-write";
    public List<string> InitialReads { get; } = [InventoryPath, DeclaredPath];
    public List<string> ExpectedParses { get; } = [];
    public bool RuntimeReached { get; private set; }
    public JsonObject ExpectedReport => new()
    {
        ["at"] = HashMainFixture.At.ToString("O"), ["counts"] = Counts.DeepClone(), ["modules"] = Facts.Count,
        ["code_sha256"] = new string('e', 64), ["native_producer"] = JsonSerializer.SerializeToNode(HashMainFixture.Producer), ["historical_python_identity"] = false,
        ["build_files_executed"] = false, ["runtime_module_or_os_access_inferred"] = false, ["complete"] = false,
        ["limitations"] = new JsonArray("Literal properties are source facts, not product selection or observed installed module/version", "Expressions, filegroups, defaults, imports and exclusions lacking literal proof remain unresolved", "Declared build module versions, AIDL frozen dump versions and Android/API/APEX versions remain separate")
    };
    public byte[] ExpectedFactBytes => Facts.SelectMany(row => HashMainFixture.Serialize(row).Concat(new byte[] { 10 })).ToArray();
    public byte[] ExpectedReportBytes => HashMainFixture.Serialize(ExpectedReport);
    public IReadOnlyList<StoredArtifact> ExpectedInputPins => new[] { Request.Inventory, Request.DeclaredSources }
        .Concat(InitialReads.Skip(2).Distinct(StringComparer.Ordinal).Select(path => HashMainFixture.Pin(path, Inputs[path], "application/octet-stream"))).ToArray();
    private static JsonObject ArrayNode(string raw, params JsonObject[] items) => new()
    { ["kind"] = "array", ["complete"] = true, ["raw"] = raw, ["items"] = new JsonArray(items.Select(item => (JsonNode)item).ToArray()) };
    private static JsonObject Source(string text) => new()
    {
        ["selection_reasons"] = new JsonArray(Reason), ["local_path"] = RawPath, ["repository"] = "repo", ["revision"] = "rev",
        ["path"] = "dir/Android.bp", ["sha256"] = ContentHash.Sha256(Encoding.UTF8.GetBytes(text))
    };
    private static JsonObject Declared(string kind, JsonNode? path, JsonNode? repository = null, JsonNode? revision = null) => new()
    { ["kind"] = kind, ["path"] = path?.DeepClone(), ["repository"] = repository?.DeepClone() ?? JsonValue.Create("repo"), ["revision"] = revision?.DeepClone() ?? JsonValue.Create("rev") };
    public static ModuleMainFixture Create(ModuleMainCase test)
    {
        var f = new ModuleMainFixture { Case = test }; var variant = test.Variant;
        var properties = new List<(string Name, string Raw, JsonObject Value)> { ("name", "\"foo\"", SoongContractCases.Lit("\"foo\"", "foo")) };
        string srcs = variant switch
        {
            "expression" => "common + [\"p/A.aidl\"]", "unsafe-selector" or "unsafe-before-null-path" => "[\"../escape\"]", "filegroup" => "[\":files\"]",
            "srcs-null" => "null", "srcs-number" => "7", "srcs-item-number" => "[7]", "srcs-empty" => "[]", "hal-link" => "[\"p/**/*.hal\"]", _ => "[\"p/**/*.aidl\"]"
        };
        JsonObject srcNode = srcs.StartsWith('[')
            ? new() { ["kind"] = "array", ["complete"] = true, ["raw"] = srcs, ["items"] = new JsonArray((srcs == "[]" ? Array.Empty<JsonNode?>() : new JsonNode?[] { srcs == "[7]" ? SoongContractCases.Lit("7", 7) : SoongContractCases.Lit(srcs[1..^1], srcs[2..^2]) })) }
            : srcs == "7" ? SoongContractCases.Lit("7", 7) : new() { ["kind"] = "unresolved-expression", ["raw"] = srcs };
        if (variant != "srcs-absent") properties.Add(("srcs", srcs, srcNode));
        if (variant.StartsWith("exclude-", StringComparison.Ordinal))
        {
            var raw = variant switch { "exclude-empty" => "[]", "exclude-expression" => "EXCLUDES", "exclude-null" => "null", "exclude-number" => "7", "exclude-item-number" => "[7]", _ => "[\"p/test/**\"]" };
            JsonObject value = raw.StartsWith('[') ? new() { ["kind"] = "array", ["complete"] = true, ["raw"] = raw, ["items"] = new JsonArray(raw == "[]" ? [] : raw == "[7]" ? [SoongContractCases.Lit("7", 7)] : [SoongContractCases.Lit("\"p/test/**\"", "p/test/**")]) }
                : raw == "7" ? SoongContractCases.Lit("7", 7) : new() { ["kind"] = "unresolved-expression", ["raw"] = raw };
            properties.Add(("exclude_srcs", raw, value)); if (variant == "exclude-duplicate") properties.Add(("exclude_srcs", raw, (JsonObject)value.DeepClone()));
        }
        var literalExtras = new JsonObject();
        if (variant == "all-literals")
        {
            foreach (var (name, value) in new[] { ("exclude_srcs", "p/test/**"), ("versions", "1"), ("imports", "bar-V2"), ("apex_available", "com.android.foo") })
            {
                var raw = "[\"" + value + "\"]"; properties.Add((name, raw, ArrayNode(raw, SoongContractCases.Lit("\"" + value + "\"", value)))); literalExtras[name] = new JsonArray(value);
            }
            var versionInfo = (JsonObject)SoongContractCases.Get("SOONG-01").Expected[0]!["properties"]!["items"]!.AsArray().Single(item => item!["name"]!.GetValue<string>() == "versions_with_info")!["value"]!.DeepClone();
            properties.Add(("versions_with_info", "[{version:\"1\",imports:[\"bar-V2\"]}]", versionInfo)); literalExtras["versions_with_info"] = new JsonArray(new JsonObject { ["version"] = "1", ["imports"] = new JsonArray("bar-V2") });
            foreach (var (name, value) in new[] { ("frozen", true), ("unstable", false), ("vendor_available", true) })
            { var raw = value ? "true" : "false"; properties.Add((name, raw, SoongContractCases.Lit(raw, value))); literalExtras[name] = value; }
            foreach (var (name, value) in new[] { ("stability", "vintf"), ("owner", "operator") })
            { var raw = "\"" + value + "\""; properties.Add((name, raw, SoongContractCases.Lit(raw, value))); literalExtras[name] = value; }
        }
        var moduleKind = variant == "hidl" ? "hidl_interface" : "aidl_interface";
        f.Text = moduleKind + " {" + string.Join(",", properties.Select(p => p.Name + ":" + p.Raw)) + "}";
        var treeRaw = f.Text[f.Text.IndexOf('{')..];
        var module = new JsonObject { ["kind"] = moduleKind, ["line"] = 1,
            ["properties"] = SoongContractCases.Object(treeRaw, true, properties.Select(p => SoongContractCases.Property(p.Name, p.Value)).ToArray()), ["raw"] = f.Text,
            ["status"] = "static-properties-parsed", ["runtime_module_identity_proven"] = false, ["android_applicability_proven"] = false };
        const string secondText = "hidl_interface {name:\"other\"}";
        var secondModule = new JsonObject { ["kind"] = "hidl_interface", ["line"] = 1, ["properties"] = SoongContractCases.Object("{name:\"other\"}", true, SoongContractCases.Property("name", SoongContractCases.Lit("\"other\"", "other"))), ["raw"] = secondText,
            ["status"] = "static-properties-parsed", ["runtime_module_identity_proven"] = false, ["android_applicability_proven"] = false };
        if (variant == "multiple-modules") f.Text += " " + secondText;
        var row = Source(f.Text); var sources = new JsonArray(row); JsonNode? inventory = new JsonObject { ["sources"] = sources };
        var declaredRows = new JsonArray(Declared("aidl", JsonValue.Create("dir/p/A.aidl")), Declared("aidl", JsonValue.Create("dir/p/sub/B.aidl")), Declared("evidence", JsonValue.Create("dir/p/sub/B.hal")), Declared("aidl", JsonValue.Create("dir/p/test/T.aidl")), Declared("aidl", JsonValue.Create("dir/p/Other.aidl"), JsonValue.Create("other")));
        JsonNode? declared = new JsonObject { ["sources"] = declaredRows };
        if (variant == "empty") { sources.Clear(); declaredRows.Clear(); }
        if (variant is "sources-empty-object" or "sources-empty-string") inventory!["sources"] = variant.EndsWith("object", StringComparison.Ordinal) ? new JsonObject() : JsonValue.Create("");
        if (variant == "skip") { row.Clear(); row["selection_reasons"] = new JsonArray("unrelated"); }
        if (variant == "non-soong") { row["path"] = "dir/Android.mk"; row.Remove("local_path"); row.Remove("sha256"); }
        if (variant == "reason-string") row["selection_reasons"] = "prefix " + Reason + " suffix";
        if (variant == "reason-map") row["selection_reasons"] = new JsonObject { [Reason] = false };
        if (variant == "missing-sources") inventory = new JsonObject();
        if (variant == "inventory-null") inventory = null;
        if (variant is "sources-null" or "sources-number" or "sources-bool" or "sources-map" or "sources-string") inventory!["sources"] = variant switch { "sources-null" => null, "sources-number" => JsonValue.Create(1), "sources-bool" => JsonValue.Create(true), "sources-map" => new JsonObject { ["row"] = 7 }, _ => JsonValue.Create("row") };
        if (variant == "missing-reasons") row.Remove("selection_reasons");
        if (variant == "reasons-null") row["selection_reasons"] = null;
        if (variant == "reasons-number") row["selection_reasons"] = 7;
        if (variant == "missing-path") row.Remove("path");
        if (variant == "path-null") row["path"] = null;
        if (variant == "missing-identity") row.Remove("repository");
        if (variant == "unsafe-local") row["local_path"] = "../escape";
        if (variant == "declared-missing-sources") declared = new JsonObject();
        if (variant == "declared-null") declared = null;
        if (variant == "declared-row-missing-kind") { declaredRows.Clear(); declaredRows.Add(new JsonObject()); }
        if (variant == "declared-row-null") { declaredRows.Clear(); declaredRows.Add(null); }
        if (variant == "declared-aidl-missing-path") declaredRows[0]!.AsObject().Remove("path");
        if (variant == "declared-other-null-path") { declaredRows.Clear(); declaredRows.Add(Declared("evidence", null)); }
        if (variant is "declared-null-path" or "unsafe-before-null-path") declaredRows[0]!["path"] = null;
        if (variant == "unhashable-declared") declaredRows[0]!["repository"] = new JsonArray(1);
        if (variant == "numeric-group") { row["repository"] = 1; declaredRows[0]!["repository"] = true; declaredRows[1]!["repository"] = 1.0; }
        if (variant == "different-revision") row["revision"] = "other";
        if (variant == "duplicate-declared") declaredRows.Add(declaredRows[0]!.DeepClone());
        if (variant == "duplicate-source") sources.Add(row.DeepClone());
        if (variant == "two-files") { var second = Source(secondText); second["local_path"] = SecondRawPath; sources.Add(second); }
        if (variant == "late-error") sources.Add(new JsonObject { ["selection_reasons"] = null });
        if (variant == "empty-module") f.Text = "cc_library {name:\"unrelated\"}";
        if (variant == "empty-source") f.Text = "";
        if (variant is "empty-module" or "empty-source") { row["sha256"] = ContentHash.Sha256(Encoding.UTF8.GetBytes(f.Text)); row.Remove("repository"); }
        var rawBytes = Encoding.UTF8.GetBytes(f.Text);
        if (variant is "invalid-utf8" or "digest-before-utf8") { rawBytes = [0xff]; row["sha256"] = ContentHash.Sha256(rawBytes); }
        if (variant is "digest" or "digest-before-utf8") row["sha256"] = new string('0', 64);
        f.Inputs[RawPath] = rawBytes; if (variant == "missing-raw") f.Inputs.Remove(RawPath);
        if (variant == "two-files") f.Inputs[SecondRawPath] = Encoding.UTF8.GetBytes(secondText);
        f.DirectoryCreated = !variant.StartsWith("declared-", StringComparison.Ordinal) || variant == "declared-null-path";
        if (variant is "unhashable-declared" or "cancel-before" or "unsafe-generation" or "inventory-pin" or "declared-pin" or "invalid-inventory" or "inventory-change-before" or "empty-dir" or "unrelated-dir" or "occupied") f.DirectoryCreated = false;
        var reachedRaw = f.DirectoryCreated && variant is not ("empty" or "sources-empty-object" or "sources-empty-string" or "skip" or "non-soong" or "missing-sources" or "inventory-null" or "sources-null" or "sources-number" or "sources-bool" or "sources-map" or "sources-string" or "missing-reasons" or "reasons-null" or "reasons-number" or "missing-path" or "path-null" or "unsafe-local");
        if (reachedRaw) f.InitialReads.Add(RawPath); if (variant == "duplicate-source") f.InitialReads.Add(RawPath);
        if (variant == "two-files") f.InitialReads.Add(SecondRawPath);
        f.RuntimeReached = reachedRaw && variant is not ("missing-raw" or "digest" or "invalid-utf8" or "digest-before-utf8" or "cancel-read");
        if (f.RuntimeReached && variant != "runtime") f.ExpectedParses.Add(f.Text); if (variant == "duplicate-source") f.ExpectedParses.Add(f.Text);
        if (variant == "two-files") f.ExpectedParses.Add(secondText);
        var hasModules = f.ExpectedParses.Count > 0 && variant is not ("empty-module" or "empty-source" or "parser" or "runtime");
        if (variant == "non-soong") f.Counts["non-soong-build-files-retained"] = 1;
        if (f.RuntimeReached && variant != "runtime") f.Counts["soong-files-parsed"] = variant is "duplicate-source" or "two-files" ? 2 : 1;
        if (hasModules && (test.Error is null || f.PartialOutputs))
        {
            var unresolved = variant is "expression" or "srcs-absent" or "srcs-null" or "srcs-number" or "srcs-item-number" or "exclude-expression" or "exclude-null" or "exclude-number" or "exclude-item-number" or "exclude-duplicate";
            var unsafeSelector = variant is "unsafe-selector" or "filegroup" or "unsafe-before-null-path";
            foreach (var logical in sources.OfType<JsonObject>())
            {
                var secondary = variant == "two-files" && logical["local_path"]!.GetValue<string>() == SecondRawPath;
                var fact = (JsonObject)(secondary ? secondModule : module).DeepClone(); var identity = new JsonObject(); foreach (var key in new[] { "repository", "revision", "path", "sha256" }) identity[key] = logical[key]?.DeepClone(); fact["source"] = identity;
                var literals = new JsonObject(); foreach (var name in new[] { "name", "srcs", "exclude_srcs", "versions", "versions_with_info", "imports", "frozen", "unstable", "stability", "apex_available", "vendor_available", "owner" }) literals[name] = null;
                literals["name"] = "foo"; if (srcs.StartsWith('[') && variant != "srcs-item-number") literals["srcs"] = variant == "srcs-empty" ? new JsonArray() : new JsonArray(srcs[2..^2]);
                if (variant == "srcs-absent") literals["srcs"] = null;
                if (variant == "srcs-number") literals["srcs"] = 7;
                if (variant == "srcs-item-number") literals["srcs"] = new JsonArray(7);
                if (variant is "exclude-filter" or "exclude-empty") literals["exclude_srcs"] = variant == "exclude-empty" ? new JsonArray() : new JsonArray("p/test/**");
                if (variant == "exclude-number") literals["exclude_srcs"] = 7; if (variant == "exclude-item-number") literals["exclude_srcs"] = new JsonArray(7);
                foreach (var extra in literalExtras) literals[extra.Key] = extra.Value?.DeepClone();
                if (secondary) { literals["name"] = "other"; literals["srcs"] = null; }
                fact["literal_properties"] = literals; fact["source_linkage_status"] = "unresolved source expression/filegroup/defaults";
                if (unsafeSelector) fact["source_linkage_reason"] = "Nonliteral, external or escaping source selection requires separate proof";
                else if (!unresolved && !secondary)
                {
                    fact["source_linkage_status"] = "literal-source-selectors-matched";
                    fact["matched_declared_paths"] = variant is "srcs-empty" or "different-revision" ? new JsonArray() : variant == "hal-link" ? new JsonArray("dir/p/sub/B.hal")
                        : variant is "exclude-filter" or "numeric-group" or "all-literals" ? new JsonArray("dir/p/A.aidl", "dir/p/sub/B.aidl") : new JsonArray("dir/p/A.aidl", "dir/p/sub/B.aidl", "dir/p/test/T.aidl");
                }
                f.Facts.Add(fact);
                if (variant == "multiple-modules")
                {
                    var other = (JsonObject)secondModule.DeepClone(); other["source"] = identity.DeepClone(); var otherLiterals = new JsonObject();
                    foreach (var name in literals.Select(pair => pair.Key)) otherLiterals[name] = name == "name" ? JsonValue.Create("other") : null;
                    other["literal_properties"] = otherLiterals; other["source_linkage_status"] = "unresolved source expression/filegroup/defaults"; f.Facts.Add(other);
                }
            }
            foreach (var group in f.Facts.GroupBy(fact => fact["kind"]!.GetValue<string>())) f.Counts[group.Key] = group.Count();
            f.Counts["matched-path-links"] = f.Facts.Sum(fact => fact["matched_declared_paths"]?.AsArray().Count ?? 0); f.Counts["modules-with-unresolved-selection"] = f.Facts.Count(fact => fact["source_linkage_status"]!.GetValue<string>() != "literal-source-selectors-matched");
        }
        f.Inputs[InventoryPath] = new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(inventory));
        f.Inputs[DeclaredPath] = new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(declared));
        if (variant == "invalid-inventory") f.Inputs[InventoryPath] = Encoding.UTF8.GetBytes("{");
        var inventoryPin = HashMainFixture.Pin(InventoryPath, f.Inputs[InventoryPath]); var declaredPin = HashMainFixture.Pin(DeclaredPath, f.Inputs[DeclaredPath]);
        if (variant == "inventory-pin") inventoryPin = inventoryPin with { Sha256 = new string('0', 64) }; if (variant == "declared-pin") declaredPin = declaredPin with { Sha256 = new string('0', 64) };
        f.Request = new(inventoryPin, declaredPin, variant == "unsafe-generation" ? "../escape" : Generation, SourceExtractionProfile.Resolve(f.Profile), HashMainFixture.Producer);
        if (variant == "producer") f.Request = f.Request with { Producer = HashMainFixture.Producer with { ProducerFiles = new Dictionary<string, string>() } };
        if (variant == "occupied") f.Existing[Generation + "/facts.jsonl"] = Encoding.UTF8.GetBytes("original");
        if (variant == "unrelated-dir") f.Existing[Generation + "/unrelated"] = Encoding.UTF8.GetBytes("original");
        return f;
    }
    public IReadOnlyList<string> ExpectedReads
    {
        get
        {
            if (Case.Variant is "unsafe-generation" or "cancel-before") return [];
            if (Case.Variant is "inventory-pin" or "invalid-inventory") return [InventoryPath];
            if (!DirectoryCreated && Case.Variant is not ("empty-dir" or "unrelated-dir" or "occupied" or "inventory-change-before")) return [InventoryPath, DeclaredPath];
            var reads = new List<string> { InventoryPath, DeclaredPath, InventoryPath, DeclaredPath }; reads.AddRange(InitialReads.Skip(2));
            if (Case.Error is null || Case.Variant is "write-index" or "inventory-change-after" or "raw-change-after")
            {
                reads.Add(InventoryPath); reads.Add(DeclaredPath);
                if (Case.Variant != "inventory-change-after" && ExpectedParses.Count > 0) reads.AddRange(InitialReads.Skip(2).Distinct(StringComparer.Ordinal));
            }
            return reads;
        }
    }
    public IReadOnlyList<string> ExpectedWrites => Case.Variant switch
    {
        "write-facts" or "cancel-write" => [Generation + "/facts.jsonl"],
        "write-report" or "bad-receipt" or "inventory-change-after" or "raw-change-after" => [Generation + "/facts.jsonl", Generation + "/report.json"],
        "write-index" => [Generation + "/facts.jsonl", Generation + "/report.json", Generation + "/index.json"],
        _ => Case.Error is null ? [Generation + "/facts.jsonl", Generation + "/report.json", Generation + "/index.json"] : []
    };
}
public static class InterfaceModuleMainContractCases
{
    public static IReadOnlyList<ModuleMainCase> All { get; } = Create();
    private static IReadOnlyList<ModuleMainCase> Create()
    {
        var result = new List<ModuleMainCase>();
        void Add(string group, string[] variants, string? error = null)
        { foreach (var variant in variants) result.Add(new("IM-" + result.Count, variant, group, error)); }
        Add("MAIN01", ["default", "empty", "sources-empty-object", "sources-empty-string", "empty-module", "empty-source", "release"]);
        Add("MAIN02", ["numeric-group", "different-revision", "duplicate-declared", "hal-link"]);
        Add("MAIN02", ["declared-missing-sources", "declared-null", "declared-row-missing-kind", "declared-row-null", "declared-aidl-missing-path", "declared-other-null-path", "unhashable-declared"], "ARGUMENT");
        Add("MAIN03", ["skip", "non-soong", "reason-string", "reason-map"]);
        Add("MAIN03", ["missing-sources", "inventory-null", "sources-null", "sources-number", "sources-bool", "sources-map", "sources-string", "missing-reasons", "reasons-null", "reasons-number", "missing-path", "path-null", "late-error"], "ARGUMENT");
        Add("MAIN04", ["missing-identity", "unsafe-local", "declared-null-path"], "ARGUMENT");
        Add("MAIN04", ["missing-raw"], "MISSING"); Add("MAIN04", ["digest", "digest-before-utf8"], "INTERFACE_MODULE_DIGEST"); Add("MAIN04", ["invalid-utf8"], "UTF8"); Add("MAIN04", ["parser"], "IO"); Add("MAIN04", ["runtime"], "RUNTIME");
        Add("MAIN05", ["expression", "srcs-absent", "srcs-null", "srcs-number", "srcs-item-number", "srcs-empty", "exclude-filter", "exclude-empty", "exclude-expression", "exclude-null", "exclude-number", "exclude-item-number", "exclude-duplicate", "unsafe-selector", "filegroup", "unsafe-before-null-path"]);
        Add("MAIN06", ["duplicate-source", "all-literals", "hidl", "multiple-modules", "two-files"]);
        Add("MAIN07", ["empty-dir", "unrelated-dir"], "IO"); Add("MAIN07", ["occupied"], "OUTPUT_EXISTS");
        Add("MAIN08", ["inventory-pin", "declared-pin"], "ARTIFACT_PIN_HASH"); Add("MAIN08", ["invalid-inventory"], "INVALID_JSON"); Add("MAIN08", ["unsafe-generation", "producer"], "ARGUMENT");
        Add("MAIN08", ["inventory-change-before"], "INPUT_PIN_MISMATCH"); Add("MAIN08", ["inventory-change-after"], "INPUT_CHANGED"); Add("MAIN08", ["raw-change-after"], "PRECOMMIT_VALIDATION_FAILED");
        Add("MAIN08", ["write-facts", "write-report", "write-index"], "WRITE_FAILED"); Add("MAIN08", ["bad-receipt"], "STORED_RECEIPT_MISMATCH"); Add("MAIN08", ["cancel-before", "cancel-read", "cancel-write"], "CANCEL");
        return result;
    }
    public static IEnumerable<object[]> Cases => All.Select(test => new object[] { test.Id });
    public static ModuleMainCase Get(string id) => All.Single(test => test.Id == id);
    public static async Task AssertCase(ModuleMainFixture f, IModuleMainProbe probe)
    {
        var actual = await probe.RunAsync(f); Assert.Equal(f.Case.Error, actual.Error);
        Assert.True(actual.InputsRetained); Assert.True(actual.ExistingRetained); Assert.True(actual.TokensForwarded);
        Assert.Equal(f.ExpectedReads, actual.Reads); Assert.Equal(f.ExpectedParses, actual.Parses); Assert.Equal(f.ExpectedWrites, actual.Writes);
        Assert.Equal(f.RuntimeReached ? new[] { f.Profile } : Array.Empty<string>(), actual.Profiles);
        var directoryCalled = f.DirectoryCreated || f.ExistingDirectory;
        Assert.Equal(directoryCalled ? new[] { ModuleMainFixture.Generation } : Array.Empty<string>(), actual.DirectoryCalls);
        Assert.Equal(f.DirectoryCreated || f.ExistingDirectory || f.Case.Variant == "occupied", actual.DirectoryExists);
        Assert.Equal(f.DirectoryCreated && f.Case.Error is not null, actual.RetryRefused);
        foreach (var original in f.Existing) Assert.Equal(original.Value, actual.Files[original.Key]);
        var factsPath = ModuleMainFixture.Generation + "/facts.jsonl"; var reportPath = ModuleMainFixture.Generation + "/report.json"; var indexPath = ModuleMainFixture.Generation + "/index.json";
        if (f.Case.Error is null)
        {
            Assert.NotNull(actual.Report); Assert.True(JsonNode.DeepEquals(f.ExpectedReport, actual.Report)); Assert.Equal(3, actual.Files.Count);
            Assert.Equal(f.ExpectedFactBytes, actual.Files[factsPath]); Assert.Equal(f.ExpectedReportBytes, actual.Files[reportPath]);
            var index = JsonNode.Parse(actual.Files[indexPath])!.AsObject(); Assert.Equal("androidmcp-native-interface-module-generation-v1", index["Schema"]!.GetValue<string>());
            Assert.Equal(ModuleMainFixture.Generation, index["GenerationId"]!.GetValue<string>()); Assert.Equal(f.Profile, index["ProfileId"]!.GetValue<string>());
            Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(HashMainFixture.Producer), index["Producer"]));
            var inputs = index["Inputs"]!.Deserialize<StoredArtifact[]>()!;
            Assert.Equal(f.ExpectedInputPins, inputs);
            var outputs = index["Outputs"]!.Deserialize<StoredArtifact[]>()!; Assert.Equal(2, outputs.Length);
            Assert.Equal(HashMainFixture.Pin(factsPath, actual.Files[factsPath], "application/x-ndjson"), outputs[0]); Assert.Equal(HashMainFixture.Pin(reportPath, actual.Files[reportPath]), outputs[1]);
        }
        else
        {
            Assert.Null(actual.Report); Assert.False(actual.Files.ContainsKey(indexPath));
            if (!f.PartialOutputs) Assert.Equal(f.Existing.Count, actual.Files.Count);
            else
            {
                Assert.True(actual.Files.ContainsKey(factsPath));
                if (f.Case.Variant is not ("write-facts" or "cancel-write")) Assert.Equal(f.ExpectedFactBytes, actual.Files[factsPath]);
                if (f.Case.Variant is "write-index" or "inventory-change-after" or "raw-change-after" or "bad-receipt") Assert.Equal(f.ExpectedReportBytes, actual.Files[reportPath]);
            }
        }
    }
}
