using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed class ScriptedHashMainProbe(Func<HashMainFixture, HashMainObservation>? response = null) : IHashMainProbe
{
    public static HashMainObservation Correct(HashMainFixture f)
    {
        var files = f.Existing.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.Ordinal);
        var factsPath = HashMainFixture.Generation + "/facts.jsonl"; var reportPath = HashMainFixture.Generation + "/report.json"; var indexPath = HashMainFixture.Generation + "/index.json";
        JsonObject? report = null, summary = null;
        if (f.Error is null || f.PartialOutputs)
        {
            files[factsPath] = f.Case.Variant is "write-facts" or "cancel-write" ? new byte[] { 1 } : f.ExpectedFactBytes;
            if (f.Case.Variant is not ("write-facts" or "cancel-write")) files[reportPath] = f.Case.Variant == "write-report" ? new byte[] { 1 } : f.ExpectedReportBytes;
        }
        if (f.Error is null)
        {
            report = f.ExpectedReport; var facts = HashMainFixture.Pin(factsPath, files[factsPath], "application/x-ndjson"); var reportFile = HashMainFixture.Pin(reportPath, files[reportPath]);
            var inputs = new List<StoredArtifact> { f.Request.Inventory };
            if (f.ExpectedParses.Count > 0) inputs.Add(HashMainFixture.Pin(HashMainFixture.RawPath, f.Inputs[HashMainFixture.RawPath], "application/octet-stream"));
            var commit = JsonSerializer.SerializeToNode(new { Schema = "androidmcp-native-interface-hash-generation-v1", GenerationId = HashMainFixture.Generation,
                ProfileId = f.Profile, Producer = HashMainFixture.Producer, Inputs = inputs, Outputs = new[] { facts, reportFile } })!;
            files[indexPath] = HashMainFixture.Serialize(commit);
            summary = (JsonObject)report.DeepClone(); summary.Remove("files");
            summary["native_generation"] = JsonSerializer.SerializeToNode(new { Facts = facts, Report = reportFile, Index = HashMainFixture.Pin(indexPath, files[indexPath]) });
        }
        return new(report, summary, files, f.ExpectedReads, f.ExpectedParses, f.ExpectedWrites, f.Error, true, true, true, f.FactoryReached ? new[] { f.Profile } : []);
    }
    public Task<HashMainObservation> RunAsync(HashMainFixture fixture) => Task.FromResult((response ?? Correct)(fixture));
}
[Trait("Gate", "Mock"), Trait("Requirement", "TEST-BUILD-004"), Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class PublishedHashMainMockTests
{
    [Theory, MemberData(nameof(PublishedHashMainContractCases.Cases), MemberType = typeof(PublishedHashMainContractCases))]
    public Task IndependentMainInputOutputErrorAndEffectMocks(string id) => PublishedHashMainContractCases.AssertCase(HashMainFixture.Create(PublishedHashMainContractCases.Get(id)), new ScriptedHashMainProbe());
    [Theory, MemberData(nameof(PublishedHashMainContractCases.Cases), MemberType = typeof(PublishedHashMainContractCases))]
    public async Task EveryOutcomeReadParseWriteRetentionAndArtifactAssertionRejectsWrong(string id)
    {
        var f = HashMainFixture.Create(PublishedHashMainContractCases.Get(id));
        var mutations = new List<Func<HashMainObservation, HashMainObservation>>
        {
            value => value with { Error = "wrong" }, value => value with { Reads = value.Reads.Append("unselected-file-read").ToArray() },
            value => value with { Parses = value.Parses.Append("wrong-parser").ToArray() }, value => value with { Writes = value.Writes.Append("overwrite-input").ToArray() },
            value => value with { InputsRetained = false }, value => value with { ExistingRetained = false }, value => value with { TokensForwarded = false },
            value => value with { Profiles = value.Profiles.Append("invented-profile").ToArray() }
        };
        if (f.Error is null)
        {
            mutations.Add(value => { value.Report!.Remove("counts"); return value; });
            mutations.Add(value => { value.Summary!["files"] = new JsonArray(); return value; });
            foreach (var name in new[] { "facts.jsonl", "report.json", "index.json" }) mutations.Add(value => value with { Files = value.Files.ToDictionary(pair => pair.Key, pair => pair.Key.EndsWith("/" + name, StringComparison.Ordinal) ? new byte[] { 0 } : pair.Value) });
        }
        else mutations.Add(value => value with { Report = new JsonObject() });
        if (f.Case.Variant == "write-index") mutations.Add(value => value with { Reads = value.Reads.Take(value.Reads.Count - 2).ToArray() });
        foreach (var mutate in mutations) Assert.NotNull(await Record.ExceptionAsync(() => PublishedHashMainContractCases.AssertCase(f, new ScriptedHashMainProbe(value => mutate(ScriptedHashMainProbe.Correct(value))))));
    }
    [Fact]
    public async Task EveryOriginalReportFieldIncludingIdentityCountsScopeAndConservativeFlagsDiscriminates()
    {
        foreach (var variant in new[] { "aidl", "hidl", "identity-object", "empty-source", "duplicate" })
        {
            var test = PublishedHashMainContractCases.All.Single(value => value.Variant == variant); var f = HashMainFixture.Create(test);
            var extraction = new SourceExtractionCase(test.Id, "hash-main", "input", StructuralParseStatus.Clean, [f.ExpectedReport], [], "Original main report fields");
            foreach (var mutation in SourceMutationCases.For(extraction).Select(data => (SourceMutation)data[1]))
            {
                var wrong = SourceMutationCases.Apply(extraction, mutation).Records[0];
                Assert.NotNull(await Record.ExceptionAsync(() => PublishedHashMainContractCases.AssertCase(f, new ScriptedHashMainProbe(value => ScriptedHashMainProbe.Correct(value) with { Report = wrong }))));
            }
        }
    }
}
