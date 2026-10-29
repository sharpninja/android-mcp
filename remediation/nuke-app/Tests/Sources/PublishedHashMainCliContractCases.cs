using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed record HashMainCliObservation(IReadOnlyList<int> Exits, JsonObject Report, JsonObject Summary, byte[] Facts,
    IReadOnlyList<IReadOnlyList<string>> Arguments, bool InputsRetained, bool OutputsRetained, bool WrongPinNoGeneration,
    bool NativeProducerMatches, bool PinsMatch, string RuntimeRoot, string ProjectRoot);
public interface IHashMainCliProbe { Task<HashMainCliObservation> RunAsync(); }
public static class PublishedHashMainCliContractCases
{
    public static string[] Arguments(string runtime, string project, StoredArtifact pin, string task = "InterfaceHashes") =>
    ["exec", "AndroidMcp.Build.dll", "--root", runtime, "--target", "Sources", "--operation", "Extract", "--extract-task", task,
        "--dotnet-host", "/native/dotnet", "--project-root", project, "--pipeline-request", pin.RelativePath,
        "--pipeline-request-sha256", pin.Sha256, "--pipeline-request-length", pin.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)];
    public static async Task AssertCase(IHashMainCliProbe probe)
    {
        var actual = await probe.RunAsync(); var fixture = HashMainFixture.Create(PublishedHashMainContractCases.All.Single(test => test.Variant == "aidl"));
        Assert.Equal(4, actual.Exits.Count); Assert.Equal(0, actual.Exits[0]); Assert.All(actual.Exits.Skip(1), exit => Assert.NotEqual(0, exit));
        Assert.True(actual.InputsRetained); Assert.True(actual.OutputsRetained); Assert.True(actual.WrongPinNoGeneration); Assert.True(actual.NativeProducerMatches); Assert.True(actual.PinsMatch);
        Assert.Equal(fixture.ExpectedFactBytes, actual.Facts); Assert.False(actual.Summary.ContainsKey("files"));
        foreach (var key in new[] { "counts", "files", "runtime_observations", "metadata_cannot_grant_os_or_client_authority", "complete", "historical_python_identity" }) Assert.True(JsonNode.DeepEquals(fixture.ExpectedReport[key], actual.Report[key]));
        foreach (var pair in actual.Report.Where(pair => pair.Key != "files")) Assert.True(JsonNode.DeepEquals(pair.Value, actual.Summary[pair.Key]));
        Assert.True(DateTimeOffset.TryParse(actual.Report["at"]!.GetValue<string>(), out var at)); Assert.Equal(TimeZoneInfo.Local.GetUtcOffset(at), at.Offset);
        var identity = actual.Report["native_producer"]!.Deserialize<SourceExtractProducerIdentity>()!; Assert.Equal(identity.ProducerFiles["native/assembly"], actual.Report["code_sha256"]!.GetValue<string>());
        Assert.Equal(4, actual.Arguments.Count);
        for (var index = 0; index < actual.Arguments.Count; index++)
        {
            var args = actual.Arguments[index]; Assert.Equal("exec", args[0]); Assert.DoesNotContain(args, value => value is "build" or "run" or "python" or "--internal-process-probe");
            string Value(string name) { var offset = args.ToList().IndexOf(name); Assert.InRange(offset, 0, args.Count - 2); return args[offset + 1]; }
            Assert.Equal(actual.RuntimeRoot, Value("--root")); Assert.Equal(actual.ProjectRoot, Value("--project-root")); Assert.NotEqual(actual.RuntimeRoot, actual.ProjectRoot);
            Assert.Equal("Sources", Value("--target")); Assert.Equal("Extract", Value("--operation")); Assert.Equal(index == 3 ? "Bogus" : "InterfaceHashes", Value("--extract-task"));
            Assert.Equal("workflows/hash.json", Value("--pipeline-request")); Assert.Equal(64, Value("--pipeline-request-sha256").Length); Assert.True(long.Parse(Value("--pipeline-request-length")) > 0);
        }
    }
}
public sealed class ScriptedHashMainCliProbe(Func<HashMainCliObservation, HashMainCliObservation>? mutate = null) : IHashMainCliProbe
{
    public Task<HashMainCliObservation> RunAsync()
    {
        var fixture = HashMainFixture.Create(PublishedHashMainContractCases.All.Single(test => test.Variant == "aidl")); var observation = ScriptedHashMainProbe.Correct(fixture);
        // The CLI clock is measured-local in native runs; the fixture chooses its
        // own offset from the local zone, without requiring the native producer.
        var at = new DateTimeOffset(2026, 10, 29, 3, 0, 0, TimeZoneInfo.Local.GetUtcOffset(new DateTime(2026, 10, 29)));
        observation.Report!["at"] = at.ToString("O"); observation.Summary!["at"] = at.ToString("O");
        var pin = new StoredArtifact("workflows/hash.json", new string('a', 64), 100, "application/json");
        var args = new[] { PublishedHashMainCliContractCases.Arguments("/runtime", "/project", pin), PublishedHashMainCliContractCases.Arguments("/runtime", "/project", pin with { Sha256 = new string('0', 64) }),
            PublishedHashMainCliContractCases.Arguments("/runtime", "/project", pin), PublishedHashMainCliContractCases.Arguments("/runtime", "/project", pin, "Bogus") };
        var result = new HashMainCliObservation([0, 1, 1, 1], observation.Report, observation.Summary, fixture.ExpectedFactBytes, args, true, true, true, true, true, "/runtime", "/project");
        return Task.FromResult(mutate is null ? result : mutate(result));
    }
}
[Trait("Gate", "Mock"), Trait("Requirement", "TEST-BUILD-004"), Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class PublishedHashMainCliMockTests
{
    [Fact] public Task CorrectCompiledCallerFixture() => PublishedHashMainCliContractCases.AssertCase(new ScriptedHashMainCliProbe());
    [Fact] public async Task EveryCompiledCallerOutputRetentionPinAndArgvControlRejects()
    {
        var mutations = new List<Func<HashMainCliObservation, HashMainCliObservation>>
        {
            value => value with { Exits = [1, 1, 1, 1] }, value => value with { Exits = [0, 0, 1, 1] }, value => value with { Exits = [0, 1, 0, 1] }, value => value with { Exits = [0, 1, 1, 0] },
            value => value with { InputsRetained = false }, value => value with { OutputsRetained = false }, value => value with { WrongPinNoGeneration = false },
            value => value with { NativeProducerMatches = false }, value => value with { PinsMatch = false }, value => value with { Facts = [0] },
            value => { value.Summary["files"] = new JsonArray(); return value; }, value => { value.Report["complete"] = true; return value; },
            value => { value.Report["code_sha256"] = "old-python-hash"; return value; }
        };
        foreach (var parameter in new[] { "--root", "--project-root", "--operation", "--target", "--extract-task", "--pipeline-request", "--pipeline-request-sha256", "--pipeline-request-length" })
            mutations.Add(value => { var args = value.Arguments.Select(row => row.ToArray()).ToArray(); args[0][Array.IndexOf(args[0], parameter) + 1] = "wrong"; return value with { Arguments = args }; });
        foreach (var mutate in mutations) Assert.NotNull(await Record.ExceptionAsync(() => PublishedHashMainCliContractCases.AssertCase(new ScriptedHashMainCliProbe(mutate))));
    }
}
