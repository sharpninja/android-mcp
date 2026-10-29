using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Review;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review;

public sealed class ReviewPrepareCallerNativeTests
{
    [Theory, MemberData(nameof(ReviewPrepareCallerCases.Data), MemberType = typeof(ReviewPrepareCallerCases))]
    public async Task SameCallerCasesBindToPinnedInputsAndNativePreparation(string name)
        => ReviewPrepareCallerCases.AssertContract(ReviewPrepareCallerCases.Find(name), await Execute(name));

    private static async Task<PrepareCallerObservation> Execute(string name)
    {
        var root = Path.Combine(Path.GetTempPath(), "review-caller-" + Guid.NewGuid().ToString("N"));
        var inputRoot = Path.Combine(root, "project");
        var outputRoot = Path.Combine(root, "output");
        var inputs = new FileArtifactStore(inputRoot, ReviewFixture.Hash);
        var outputs = new FileArtifactStore(outputRoot, ReviewFixture.Hash);
        async Task<StoredArtifact> Input<T>(string path, T value)
            => await inputs.WriteAsync(new(path, ReviewFixture.Json(value), "application/json"));
        var evidence = new List<ReviewEvidence>();
        async Task Add(string path, byte[] body, string kind, string? project = null)
        {
            await outputs.WriteAsync(new(path, body, "application/octet-stream"));
            evidence.Add(new(path, ReviewFixture.Hash(body), kind, project, "native-fixture", "fixture-host", "head", "NUKE Validate Tests"));
        }
        await Add("design.md", Encoding.UTF8.GetBytes("Complete fixture design"), "design");
        var trx = Encoding.UTF8.GetBytes("<TestRun><ResultSummary><Counters total='1' passed='1' failed='0' notExecuted='0'/></ResultSummary><Results><UnitTestResult outcome='Passed'/></Results></TestRun>");
        await Add("unit.trx", trx, "unit-trx", "Repo.Tests");
        await Add("integration.trx", trx, "integration-trx", "Scope.Integration");
        await Add("requirements.json", ReviewFixture.Json(new { pendingHv = true, head = "head" }), "requirements");
        var snapshot = new ReviewSnapshot("fixture", "Codex-parent", "req-parent", "head", "https://github.com/owner/repo/pull/1",
            ["Repo.Tests"], ["Scope.Integration"], ["FR-BUILD-001", "TR-BUILD-TOOL-001", "TEST-BUILD-001"], evidence);
        var snapshotJson = JsonSerializer.SerializeToNode(snapshot)!.AsObject();
        if (name == "unknown-snapshot-property") snapshotJson["Complete"] = true;
        if (name == "null-inventory") snapshotJson["UnitProjects"] = null;
        if (name == "missing-snapshot-field") snapshotJson.Remove("Head");
        if (name == "private-snapshot-payload") snapshotJson["ParentRequestId"] = "Authorization: Bearer real-private-credential";
        var snapshotBody = snapshotJson.ToJsonString();
        if (name == "duplicate-snapshot-property") snapshotBody = snapshotBody[..^1] + ",\"RunId\":\"other\"}";
        var snapshotPin = await inputs.WriteAsync(new("snapshot.json", Encoding.UTF8.GetBytes(snapshotBody), "application/json"));
        if (name == "private-control-path") snapshotPin = snapshotPin with { RelativePath = "private/PROFILE-MERGED.md" };
        if (name == "changed-snapshot-hash") snapshotPin = snapshotPin with { Sha256 = new string('0', 64) };
        var gitPin = await Input("git.json", new ToolIdentity("/approved/git", name == "invalid-git-pin" ? "bad" : new string('a', 64), "fixture"));
        var ghPin = name == "missing-gh-identity"
            ? new StoredArtifact("missing-gh.json", new string('b', 64), 0, "application/json")
            : await Input("gh.json", new ToolIdentity("/approved/gh", new string('b', 64), "fixture"));
        var req = evidence.Single(x => x.Kind == "requirements");
        var placeholder = new ReviewEvidence("trusted-evaluation.json", new string('c', 64), "nuke");
        var configuration = new ReviewPrerequisiteConfiguration(name == "wrong-workspace" ? Path.Combine(root, "other") : inputRoot,
            "mcpserver-codex-plugin", "fixture", placeholder,
            name == "unsafe-receipt-pin" ? req with { Path = "../outside.json" } : req,
            name == "missing-prerequisite-pin" ? null! : placeholder, ["Scope.Integration"]);
        var configPin = await Input("configuration.json", configuration);
        if (name == "changed-config-length") configPin = configPin with { Length = configPin.Length + 1 };
        // Fixture data and expected observations come from the approved contract, never production output.
        var requestPin = await Input("request.json", new { Snapshot = snapshotPin, GitIdentity = gitPin, GhIdentity = ghPin, PrerequisiteConfiguration = configPin });
        if (name == "invalid-input-path") requestPin = requestPin with { RelativePath = "../outside.json" };
        var countedInputs = new CallerStore(inputs);
        var countedOutputs = new CallerStore(outputs);
        var ready = new CallerReadiness(name);
        var stub = new StoredArtifact(placeholder.Path, placeholder.Sha256, 0, "application/json");
        var trusted = new ReviewPrerequisites("head", snapshot.UnitProjects, snapshot.IntegrationProjects, snapshot.RequirementIds,
            stub, new(req.Path, req.Sha256, (await outputs.ReadAsync(req.Path)).Length, "application/json"), stub, stub);
        var prerequisites = new CallerPrerequisites(trusted, name == "trusted-prerequisite-failure");
        try
        {
            var decoded = await new ReviewPrepareInputsReader(new PinnedArtifactReader(countedInputs), new CanonicalJson(ReviewFixture.Hash))
                .ReadAsync(requestPin, inputRoot);
            Assert.Equal(new[] { requestPin, snapshotPin, gitPin, ghPin, configPin }, decoded.InputPins);
            Assert.Equal("/approved/git", decoded.Git.ExecutablePath);
            Assert.Equal("/approved/gh", decoded.Gh.ExecutablePath);
            var prepared = await new ReviewPrepareService(countedOutputs, new FileArtifactLeaseProvider(Path.Combine(outputRoot, ".leases")),
                new ReviewClock(), new CanonicalJson(ReviewFixture.Hash), ReviewFixture.Hash, ready, prerequisites).PrepareAsync(decoded.Snapshot);
            Assert.Equal(snapshot, prepared.Snapshot with { UnitProjects = snapshot.UnitProjects, IntegrationProjects = snapshot.IntegrationProjects,
                RequirementIds = snapshot.RequirementIds, Evidence = snapshot.Evidence });
            foreach (var item in evidence)
                Assert.Equal((await outputs.ReadAsync(item.Path)).ToArray(), (await outputs.ReadAsync("reviews/fixture/input/" + item.Path)).ToArray());
            Assert.False(Directory.Exists(Path.Combine(outputRoot, "reviews/fixture/calls")));
            return Observation("prepared", prepared.Snapshot.Head, prepared.Snapshot.ParentRequestId);
        }
        catch (ReviewException error) { return Observation(error.Code); }
        catch (PinnedArtifactException error) { return Observation(error.Code); }

        PrepareCallerObservation Observation(string status, string head = "", string parent = "")
            => new(status, countedInputs.Reads, ready.Calls, prerequisites.Calls, countedOutputs.Writes, head, parent, 0);
    }

    private sealed class CallerStore(IArtifactStore inner) : IArtifactStore
    {
        public int Reads { get; private set; }
        public int Writes { get; private set; }
        public Task<ReadOnlyMemory<byte>> ReadAsync(string path, CancellationToken cancellationToken = default)
        { Reads++; return inner.ReadAsync(path, cancellationToken); }
        public Task<StoredArtifact> WriteAsync(Artifact artifact, CancellationToken cancellationToken = default)
        { Writes++; return inner.WriteAsync(artifact, cancellationToken); }
    }
    private sealed class CallerReadiness(string name) : IReviewReadinessSource
    {
        public int Calls { get; private set; }
        public Task<ReviewReadiness> ObserveAsync(string url, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(new ReviewReadiness(name == "wrong-head" ? "different" : "head", name == "draft", true, url,
                ReviewFixture.Json(new { headRefOid = "head", isDraft = name == "draft", state = "OPEN", url })));
        }
    }
    private sealed class CallerPrerequisites(ReviewPrerequisites value, bool unavailable) : IReviewPrerequisiteSource
    {
        public int Calls { get; private set; }
        public Task<ReviewPrerequisites> ObserveAsync(ReviewSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            Calls++;
            return unavailable ? throw new ReviewException("PREREQUISITE_UNAVAILABLE", "Synthetic trusted source failure") : Task.FromResult(value);
        }
    }
}
