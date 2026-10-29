using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed class ScriptedModuleMainProbe(Func<ModuleMainFixture, ModuleMainObservation>? response = null) : IModuleMainProbe
{
    public static ModuleMainObservation Correct(ModuleMainFixture f)
    {
        var files = f.Existing.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.Ordinal);
        var factsPath = ModuleMainFixture.Generation + "/facts.jsonl"; var reportPath = ModuleMainFixture.Generation + "/report.json"; var indexPath = ModuleMainFixture.Generation + "/index.json";
        JsonObject? report = null;
        if (f.Case.Error is null || f.PartialOutputs)
        {
            files[factsPath] = f.Case.Variant is "write-facts" or "cancel-write" ? [1] : f.ExpectedFactBytes;
            if (f.Case.Variant is not ("write-facts" or "cancel-write")) files[reportPath] = f.Case.Variant == "write-report" ? [1] : f.ExpectedReportBytes;
        }
        if (f.Case.Error is null)
        {
            report = f.ExpectedReport; var inputs = f.ExpectedInputPins;
            var index = JsonSerializer.SerializeToNode(new { Schema = "androidmcp-native-interface-module-generation-v1", GenerationId = ModuleMainFixture.Generation,
                ProfileId = f.Profile, Producer = HashMainFixture.Producer, Inputs = inputs,
                Outputs = new[] { HashMainFixture.Pin(factsPath, files[factsPath], "application/x-ndjson"), HashMainFixture.Pin(reportPath, files[reportPath]) } })!;
            files[indexPath] = HashMainFixture.Serialize(index);
        }
        return new(report, files, f.ExpectedReads, f.ExpectedParses, f.ExpectedWrites,
            f.DirectoryCreated || f.ExistingDirectory ? [ModuleMainFixture.Generation] : [],
            f.DirectoryCreated || f.ExistingDirectory || f.Case.Variant == "occupied", f.DirectoryCreated && f.Case.Error is not null,
            f.Case.Error, true, true, true, f.RuntimeReached ? [f.Profile] : []);
    }
    public Task<ModuleMainObservation> RunAsync(ModuleMainFixture fixture) => Task.FromResult((response ?? Correct)(fixture));
}
[Trait("Gate", "Mock"), Trait("Requirement", "TEST-BUILD-004"), Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class InterfaceModuleMainMockTests
{
    [Theory, MemberData(nameof(InterfaceModuleMainContractCases.Cases), MemberType = typeof(InterfaceModuleMainContractCases))]
    public Task IndependentOriginalMainAndNativeEffectMocks(string id) => InterfaceModuleMainContractCases.AssertCase(ModuleMainFixture.Create(InterfaceModuleMainContractCases.Get(id)), new ScriptedModuleMainProbe());
    [Theory, MemberData(nameof(InterfaceModuleMainContractCases.Cases), MemberType = typeof(InterfaceModuleMainContractCases))]
    public async Task AllOutputErrorLazyReadParseDirectoryRetentionAndWriteAssertionsDiscriminate(string id)
    {
        var f = ModuleMainFixture.Create(InterfaceModuleMainContractCases.Get(id));
        var mutations = new List<Func<ModuleMainObservation, ModuleMainObservation>>
        {
            value => value with { Error = "wrong" }, value => value with { Reads = value.Reads.Append("wrong-early-admission").ToArray() },
            value => value with { Parses = value.Parses.Append("wrong-parser").ToArray() }, value => value with { Writes = value.Writes.Append("overwrite-source").ToArray() },
            value => value with { DirectoryCalls = value.DirectoryCalls.Append("wrong-late-directory").ToArray() }, value => value with { DirectoryExists = !value.DirectoryExists },
            value => value with { RetryRefused = !value.RetryRefused }, value => value with { InputsRetained = false }, value => value with { ExistingRetained = false },
            value => value with { TokensForwarded = false }, value => value with { Profiles = value.Profiles.Append("invented-profile").ToArray() }
        };
        if (f.Case.Error is null)
        {
            mutations.Add(value => { value.Report!.Remove("counts"); return value; });
            foreach (var name in new[] { "facts.jsonl", "report.json", "index.json" }) mutations.Add(value => value with { Files = value.Files.ToDictionary(pair => pair.Key, pair => pair.Key.EndsWith("/" + name, StringComparison.Ordinal) ? new byte[] { 0 } : pair.Value) });
        }
        else mutations.Add(value => value with { Report = new JsonObject() });
        foreach (var mutate in mutations) Assert.NotNull(await Record.ExceptionAsync(() => InterfaceModuleMainContractCases.AssertCase(f, new ScriptedModuleMainProbe(value => mutate(ScriptedModuleMainProbe.Correct(value))))));
    }
    [Fact]
    public async Task EveryReportAndFactFieldRejectsWrongOrMissingValues()
    {
        foreach (var variant in new[] { "default", "numeric-group", "expression", "exclude-null", "duplicate-source", "empty", "all-literals", "hidl", "multiple-modules", "two-files" })
        {
            var f = ModuleMainFixture.Create(InterfaceModuleMainContractCases.All.Single(test => test.Variant == variant));
            var contract = new SourceExtractionCase(f.Case.Id, "interface-module-main", "input", StructuralParseStatus.Clean, [f.ExpectedReport], [], "Original module-main report fields");
            foreach (var mutation in SourceMutationCases.For(contract).Select(data => (SourceMutation)data[1]))
            {
                var wrong = SourceMutationCases.Apply(contract, mutation).Records[0];
                Assert.NotNull(await Record.ExceptionAsync(() => InterfaceModuleMainContractCases.AssertCase(f, new ScriptedModuleMainProbe(value => ScriptedModuleMainProbe.Correct(value) with { Report = wrong }))));
            }
            if (f.Facts.Count == 0) continue;
            var factContract = new SourceExtractionCase(f.Case.Id, "interface-module-main", "input", StructuralParseStatus.Clean, f.Facts, [], "Original module-main complete fact fields");
            foreach (var mutation in SourceMutationCases.For(factContract).Select(data => (SourceMutation)data[1]))
            {
                var wrong = SourceMutationCases.Apply(factContract, mutation).Records;
                var bytes = wrong.SelectMany(row => HashMainFixture.Serialize(row).Concat(new byte[] { 10 })).ToArray();
                Assert.NotNull(await Record.ExceptionAsync(() => InterfaceModuleMainContractCases.AssertCase(f, new ScriptedModuleMainProbe(value =>
                { var result = ScriptedModuleMainProbe.Correct(value); return result with { Files = result.Files.ToDictionary(pair => pair.Key, pair => pair.Key.EndsWith("/facts.jsonl", StringComparison.Ordinal) ? bytes : pair.Value) }; }))));
            }
        }
    }
}
