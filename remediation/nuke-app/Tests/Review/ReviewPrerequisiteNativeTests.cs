using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Review;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review;

public sealed class ReviewPrerequisiteNativeTests
{
    [Theory, MemberData(nameof(ReviewPrerequisiteCases.Data), MemberType = typeof(ReviewPrerequisiteCases))]
    public async Task SamePrerequisiteCasesBindToNativeSources(string name)
        => ReviewPrerequisiteCases.AssertContract(name, await new NativePrerequisiteCases().ExecuteAsync(name));
}
public sealed class NativePrerequisiteCases : IPrerequisiteCaseSystem
{
    public async Task<PrerequisiteObservation> ExecuteAsync(string name)
    {
        var store = new ReviewMemoryStore();
        var runner = new ReviewFakeRunner();
        var head = new string('a', 40);
        var unit = Encoding.UTF8.GetBytes("<Project Sdk='Microsoft.NET.Sdk'><PropertyGroup><IsTestProject>true</IsTestProject></PropertyGroup></Project>");
        var integration = Encoding.UTF8.GetBytes("<Project Sdk='Microsoft.NET.Sdk'><PropertyGroup><IsTestProject>$(EvaluationRequired)</IsTestProject></PropertyGroup></Project>");
        runner.Replies.Enqueue(Encoding.UTF8.GetBytes((name == "wrong-live-git-head" ? new string('b', 40) : head) + "\n"));
        runner.Replies.Enqueue(Encoding.UTF8.GetBytes("tests/unit.csproj\0tests/integration.csproj\0"));
        runner.Replies.Enqueue(integration); // Native source pins definitions in ordinal path order.
        runner.Replies.Enqueue(unit);
        var evaluation = JsonSerializer.SerializeToNode(new { head, source = "native-project-evaluation", toolSha256 = "verified-native-dotnet-sha",
            projects = new[]
            {
                new { path = "tests/unit.csproj", definitionSha256 = ReviewFixture.Hash(unit), isTestProject = true, kind = "unit" },
                new { path = "tests/integration.csproj", definitionSha256 = ReviewFixture.Hash(integration), isTestProject = true, kind = "integration" }
            } })!.AsObject();
        var requirements = JsonSerializer.SerializeToNode(new { head, source = "required-mcp-plugin", plugin = "mcpserver-codex-plugin", pluginVersion = "1.121.0", workspace = "/workspace",
            functional = new[] { new { id = "FR-BUILD-001", status = "pending", acceptanceCriteria = new[] { new { id = "ac-1", text = "Complete native review preflight", isSatisfied = false } } } },
            technical = new[] { new { id = "TR-BUILD-TOOL-001", status = "pending" } },
            testing = new[] { new { id = "TEST-BUILD-001", status = "pending" } },
            mappings = new[] { new { frId = "FR-BUILD-001", trIds = new[] { "TR-BUILD-TOOL-001" }, testIds = new[] { "TEST-BUILD-001" }, criteria = new[] { new { acId = "ac-1", testIds = new[] { "TEST-BUILD-001" } } } } } })!.AsObject();
        var checkRows = new List<object>();
        foreach (var code in GitReviewPrerequisiteSource.RequiredChecks)
        {
            var path = "checks/" + code + ".txt";
            var raw = Encoding.UTF8.GetBytes("Verified head-bound evidence: " + code);
            store.Values[path] = raw;
            checkRows.Add(new { code, path, sha256 = ReviewFixture.Hash(raw) });
        }
        var checklist = JsonSerializer.SerializeToNode(new { head, scope = "design-and-nuke-only", scopeIntegrationProjects = new[] { "tests/integration.csproj" }, checks = checkRows })!.AsObject();
        switch (name)
        {
            case "omitted-unit-project": evaluation["projects"]!.AsArray().RemoveAt(0); break;
            case "dynamic-unknown-kind": evaluation["projects"]![1]!["kind"] = "unknown"; break;
            case "missing-test-metadata": evaluation["projects"]![0]!.AsObject().Remove("isTestProject"); break;
            case "stale-project-definition": evaluation["projects"]![0]!["definitionSha256"] = "stale"; break;
            case "wrong-evaluation-head": evaluation["head"] = new string('b', 40); break;
            case "missing-fr-ac": requirements["functional"]![0]!["acceptanceCriteria"] = new JsonArray(); break;
            case "missing-tr-mapping": requirements["mappings"]![0]!["trIds"] = new JsonArray(); break;
            case "missing-test-mapping": requirements["mappings"]![0]!["testIds"] = new JsonArray(); break;
            case "missing-ac-coverage": requirements["mappings"]![0]!["criteria"] = new JsonArray(); break;
            case "forged-caller-complete": requirements.Remove("functional"); requirements["complete"] = true; break;
            case "missing-checklist-entry": checklist["checks"]!.AsArray().RemoveAt(0); break;
            case "stale-checklist-evidence": checklist["checks"]![0]!["sha256"] = "stale"; break;
        }
        ReviewEvidence Put(string path, JsonObject body)
        {
            var raw = Encoding.UTF8.GetBytes(body.ToJsonString());
            store.Values[path] = raw;
            return new(path, ReviewFixture.Hash(raw), "nuke");
        }
        var eval = Put("native-evaluation.json", evaluation);
        var req = Put("official-requirements.json", requirements);
        var checks = Put("preflight-checklist.json", checklist);
        if (name == "missing-evaluation") store.Values.Remove(eval.Path);
        var configuration = new ReviewPrerequisiteConfiguration("/workspace", "mcpserver-codex-plugin", "1.121.0", eval, req, checks,
            name == "omitted-scope-integration" ? [] : ["tests/integration.csproj"]);
        var source = new GitReviewPrerequisiteSource(runner, new ReviewCaptureReader(runner), store,
            new CanonicalJson(ReviewFixture.Hash), ReviewFixture.Hash, new("/approved/git", "pinned-git", "git-fixture"), "/approved/checkout", "/private/receipts", configuration);
        var snapshot = new ReviewSnapshot("prerequisite-fixture", "Codex-parent", "req-parent", head,
            "https://github.com/owner/repo/pull/1", ["tests/unit.csproj"], ["tests/integration.csproj"], ["FR-BUILD-001", "TR-BUILD-TOOL-001", "TEST-BUILD-001"], []);
        try
        {
            var actual = await source.ObserveAsync(snapshot);
            Assert.Equal(head, actual.Head);
            Assert.Equal(new[] { "FR-BUILD-001", "TEST-BUILD-001", "TR-BUILD-TOOL-001" }, actual.RequirementIds);
            Assert.All(runner.Calls, call => Assert.Contains(call.Arguments[0], new[] { "rev-parse", "ls-tree", "show" }));
            Assert.Equal(ReviewFixture.Hash(store.Values[actual.InventoryReceipt.RelativePath]), actual.InventoryReceipt.Sha256);
            return new("verified", string.Join(',', actual.UnitProjects), string.Join(',', actual.IntegrationProjects), runner.Calls.Count);
        }
        catch (ReviewException error) { return new(error.Code, "", "", runner.Calls.Count); }
    }
}
