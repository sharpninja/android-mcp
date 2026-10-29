using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using AndroidMcp.BuildTool.Tests.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public static class InterfaceModulePlannerCases
{
    public static IEnumerable<object[]> Cases => new[] { "InterfaceModules", "Declarations", "InterfaceHashes" }.Select(task => new object[] { new PlannerCase("MODULE-TASK-" + task,
        PlannerContractCases.Request("Sources", "Extract", [("project-root", "inputs"), ("pipeline-request", "modules.json"), ("pipeline-request-sha256", new string('a', 64)), ("pipeline-request-length", "42"), ("extract-task", task)]), new(Category.Sources, Operation.Extract),
        ["admit-pinned-workflow", "admit-task-inputs", "verify-task-source-pins", "capture-runtime-identity", "extract-source-generation", "seal-extraction"], [],
        ["admit-task-inputs<-admit-pinned-workflow", "verify-task-source-pins<-admit-task-inputs", "capture-runtime-identity<-verify-task-source-pins", "extract-source-generation<-capture-runtime-identity", "seal-extraction<-extract-source-generation"]) });
}
[Trait("Gate", "Mock"), Trait("Requirement", "TEST-BUILD-003")]
public sealed class InterfaceModulePlannerMockTests
{
    [Theory, MemberData(nameof(InterfaceModulePlannerCases.Cases), MemberType = typeof(InterfaceModulePlannerCases))]
    public void SameExistingGenericTaskStepsAndPriorTasksRemainSupported(PlannerCase test)
    {
        PlannerContractCases.AssertContract(test, new ScriptedPlanner(PlannerContractCases.Correct(test)));
        Assert.NotNull(Record.Exception(() => PlannerContractCases.AssertContract(test, new ScriptedPlanner(PlannerContractCases.Correct(test), "effect-before-pin"))));
        var wrong = PlannerContractCases.Correct(test) with { Plan = null, Errors = [new("UNSUPPORTED_PARAMETER", "extract-task", "wrong")] };
        Assert.NotNull(Record.Exception(() => PlannerContractCases.AssertContract(test, new ScriptedPlanner(wrong))));
    }
}
public sealed record ModuleMainCliObservation(IReadOnlyList<int> Exits, JsonObject Report, JsonObject Stdout, byte[] Facts,
    IReadOnlyList<IReadOnlyList<string>> Arguments, bool InputsRetained, bool OutputsRetained, bool WrongPinNoDirectory,
    bool FailedSourceDirectoryRetained, bool FailedRetryBeforeSourceRead, bool NativeProducerMatches, bool PinsMatch,
    string RuntimeRoot, string ProjectRoot);
public interface IModuleMainCliProbe { Task<ModuleMainCliObservation> RunAsync(); }
public static class InterfaceModuleCallerContractCases
{
    public static string[] Arguments(string runtime, string project, StoredArtifact pin, string task = "InterfaceModules") => PublishedHashMainCliContractCases.Arguments(runtime, project, pin, task);
    public static async Task AssertCase(IModuleMainCliProbe probe)
    {
        var actual = await probe.RunAsync(); var fixture = ModuleMainFixture.Create(InterfaceModuleMainContractCases.All.Single(test => test.Variant == "default"));
        Assert.Equal(6, actual.Exits.Count); Assert.Equal(0, actual.Exits[0]); Assert.All(actual.Exits.Skip(1), exit => Assert.NotEqual(0, exit));
        Assert.True(actual.InputsRetained); Assert.True(actual.OutputsRetained); Assert.True(actual.WrongPinNoDirectory);
        Assert.True(actual.FailedSourceDirectoryRetained); Assert.True(actual.FailedRetryBeforeSourceRead); Assert.True(actual.NativeProducerMatches); Assert.True(actual.PinsMatch);
        Assert.Equal(fixture.ExpectedFactBytes, actual.Facts); Assert.True(JsonNode.DeepEquals(actual.Report, actual.Stdout));
        foreach (var key in new[] { "counts", "modules", "build_files_executed", "runtime_module_or_os_access_inferred", "complete", "historical_python_identity", "limitations" })
            Assert.True(JsonNode.DeepEquals(fixture.ExpectedReport[key], actual.Report[key]));
        Assert.True(DateTimeOffset.TryParse(actual.Report["at"]!.GetValue<string>(), out var at)); Assert.Equal(TimeZoneInfo.Local.GetUtcOffset(at), at.Offset);
        var identity = actual.Report["native_producer"]!.Deserialize<SourceExtractProducerIdentity>()!; Assert.Equal(identity.ProducerFiles["native/assembly"], actual.Report["code_sha256"]!.GetValue<string>());
        Assert.Equal(6, actual.Arguments.Count);
        for (var index = 0; index < actual.Arguments.Count; index++)
        {
            var args = actual.Arguments[index]; Assert.Equal("exec", args[0]); Assert.DoesNotContain(args, value => value is "build" or "run" or "python" or "--internal-process-probe");
            string Value(string name) { var offset = args.ToList().IndexOf(name); Assert.InRange(offset, 0, args.Count - 2); return args[offset + 1]; }
            Assert.Equal(actual.RuntimeRoot, Value("--root")); Assert.Equal(actual.ProjectRoot, Value("--project-root")); Assert.NotEqual(actual.RuntimeRoot, actual.ProjectRoot);
            Assert.Equal("Sources", Value("--target")); Assert.Equal("Extract", Value("--operation")); Assert.Equal(index == 5 ? "Bogus" : "InterfaceModules", Value("--extract-task"));
            Assert.Equal(index is 3 or 4 ? "workflows/broken.json" : "workflows/modules.json", Value("--pipeline-request"));
            Assert.Equal(64, Value("--pipeline-request-sha256").Length); Assert.True(long.Parse(Value("--pipeline-request-length")) > 0);
        }
    }
}
public sealed class ScriptedModuleMainCliProbe(Func<ModuleMainCliObservation, ModuleMainCliObservation>? mutate = null) : IModuleMainCliProbe
{
    public Task<ModuleMainCliObservation> RunAsync()
    {
        var f = ModuleMainFixture.Create(InterfaceModuleMainContractCases.All.Single(test => test.Variant == "default")); var report = f.ExpectedReport;
        report["at"] = new DateTimeOffset(2026, 10, 29, 3, 0, 0, TimeZoneInfo.Local.GetUtcOffset(new DateTime(2026, 10, 29))).ToString("O");
        var pin = new StoredArtifact("workflows/modules.json", new string('a', 64), 100, "application/json");
        var args = Enumerable.Range(0, 6).Select(index => InterfaceModuleCallerContractCases.Arguments("/runtime", "/project", index is 3 or 4 ? pin with { RelativePath = "workflows/broken.json" } : index == 1 ? pin with { Sha256 = new string('0', 64) } : pin, index == 5 ? "Bogus" : "InterfaceModules")).ToArray();
        var result = new ModuleMainCliObservation([0, 1, 1, 1, 1, 1], report, (JsonObject)report.DeepClone(), f.ExpectedFactBytes, args, true, true, true, true, true, true, true, "/runtime", "/project");
        return Task.FromResult(mutate is null ? result : mutate(result));
    }
}
[Trait("Gate", "Mock"), Trait("Requirement", "TEST-BUILD-004"), Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class InterfaceModuleCallerMockTests
{
    [Fact] public Task IndependentCompiledTaskRequestAndOutputContract() => InterfaceModuleCallerContractCases.AssertCase(new ScriptedModuleMainCliProbe());
    [Fact] public async Task CompiledCallerOutputEffectsPinsAndArgvControlsDiscriminate()
    {
        var mutations = new List<Func<ModuleMainCliObservation, ModuleMainCliObservation>>
        {
            value => value with { Exits = [1, 1, 1, 1, 1, 1] }, value => value with { Exits = [0, 0, 1, 1, 1, 1] }, value => value with { Exits = [0, 1, 0, 1, 1, 1] },
            value => value with { Exits = [0, 1, 1, 0, 1, 1] }, value => value with { Exits = [0, 1, 1, 1, 0, 1] }, value => value with { Exits = [0, 1, 1, 1, 1, 0] },
            value => value with { InputsRetained = false }, value => value with { OutputsRetained = false }, value => value with { WrongPinNoDirectory = false },
            value => value with { FailedSourceDirectoryRetained = false }, value => value with { FailedRetryBeforeSourceRead = false }, value => value with { NativeProducerMatches = false },
            value => value with { PinsMatch = false }, value => value with { Facts = [0] }, value => { value.Stdout["complete"] = true; return value; },
            value => { value.Report["code_sha256"] = "old-python-hash"; return value; }
        };
        foreach (var parameter in new[] { "--root", "--project-root", "--operation", "--target", "--extract-task", "--pipeline-request", "--pipeline-request-sha256", "--pipeline-request-length" })
            mutations.Add(value => { var args = value.Arguments.Select(row => row.ToArray()).ToArray(); args[0][Array.IndexOf(args[0], parameter) + 1] = "wrong"; return value with { Arguments = args }; });
        foreach (var mutate in mutations) Assert.NotNull(await Record.ExceptionAsync(() => InterfaceModuleCallerContractCases.AssertCase(new ScriptedModuleMainCliProbe(mutate))));
    }
}
