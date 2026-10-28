using System.Text;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

// These checks read current canonical bytes directly. Historical fixture carriers
// remain the separate oracle for the unchanged original parity assertions.
[Trait("Gate","Real")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class CurrentLayoutModelValidationRealTests
{
    private static string ProjectRoot=>Path.Combine(DocumentationFixture.Current.Workspace,"remediation/design-c4");
    private static readonly IReadOnlyDictionary<string,string> CurrentLayoutPins=new Dictionary<string,string>(StringComparer.Ordinal)
    {
        ["architecture/code-diagrams.md"]="6cca957a2f0da5b09d76d4c82a0c6817e59c22f09705314169b98dd330d215a1",
        ["architecture/workspace.dsl"]="f7a14cc8b830407c9db981e1b33d5232e99cc7b545a70b3171c297f710b2e815"
    };

    [Fact]
    public void CurrentSourcesContainExactlyTheReviewedPresentationAndProgressChanges()
    {
        var hashes=new NativeContentHash();
        foreach(var original in ToolingDocumentationMigrationCases.Proposed())
        {
            var current=File.ReadAllBytes(ArtifactPaths.Resolve(ProjectRoot,original.RelativePath));
            var text=Encoding.UTF8.GetString(original.Bytes.Span);
            if(original.RelativePath=="architecture/code-diagrams.md")
            {
                const string title="title Kotlin server core — proposed ownership and contracts (UML class notation)";
                Assert.Equal(1,text.Split(title,StringSplitOptions.None).Length-1);
                text=text.Replace(title,"<style>\ntitle {\n  Margin 0 0 96 0\n}\n</style>\n"+title,StringComparison.Ordinal);
            }
            else if(original.RelativePath=="architecture/workspace.dsl")
            {
                const string before="deployment * \"ReferenceAndroid\" Deployment \"Reference deployment; not installed by this task\" {\n            include *\n            autoLayout lr";
                Assert.Equal(1,text.Split(before,StringSplitOptions.None).Length-1);
                text=text.Replace(before,before[..^2]+"tb",StringComparison.Ordinal);
            }
            else if(original.RelativePath=="README.md")
            {
                const string beforeArchitecture="Architecture002 generated 21 SVGs and six editable class PlantUML files through native NUKE. Its normal-verification receipt is recorded in `../nuke-planning/docs/architecture-native-chronology.md`. The full model-contract prerequisite, general document validator, category package dispatch, stage-subset execution and thin Gradle adapter remain migration work. Do not interpret this bounded runtime proof as full legacy parity or hostile-review acceptance.";
                const string afterArchitecture="Current native generation produced 21 SVGs and six editable class PlantUML files. The two reported diagram layout findings were corrected, rendered, directly inspected and promoted to the canonical sources and exports. See the [current-source validation](../nuke-planning/docs/evidence/vqa-current-source-validation.json) and [promotion receipt](../nuke-planning/docs/evidence/vqa-layout-canonical-promotion-applied.json). Model-contract and private-stage services have native fixture proof; complete portable package composition and all legacy validation-receipt callers remain migration work. These bounded proofs do not establish full legacy parity or hostile-review acceptance.";
                const string beforeGradle="The authoritative C# stage planner retains 13 Gradle alias names and 12 private stages. The planned Gradle adapter reads a generated registry projection and forwards the complete alias selection once to `Design/GradleCompatibility`. It will contain no generation algorithm or competing dependency graph. The adapter is not yet available; the old Gradle file must not be used as the current native workflow.";
                const string afterGradle="The authoritative C# stage planner retains 13 Gradle alias names and 12 private stages. The generated `build.gradle.kts` adapter reads `tooling/nuke-design-stages.json` and forwards the complete alias selection once to `Design/GradleCompatibility`. Generation algorithms and ordering remain in the monolith. `Design/GradleProjection` regenerates these two adapter files. An actual offline Gradle invocation of `extractCodeViews` delegated to the current C# application and reproduced the six selected PlantUML outputs; its [execution receipt](../nuke-planning/docs/evidence/gradle-delegate-native-001.json) records the exact admitted tools, selected scope and nonfatal deprecation warning. This selected-alias proof does not claim that every packaging alias is complete.";
                foreach(var change in new[]{(beforeArchitecture,afterArchitecture),(beforeGradle,afterGradle)})
                {
                    Assert.Equal(1,text.Split(change.Item1,StringSplitOptions.None).Length-1);
                    text=text.Replace(change.Item1,change.Item2,StringComparison.Ordinal);
                }
                Assert.Equal("63c2001e58a0ca58aaaa70489dd9b10e1de0694f800b139e08eb22fbebc9315f",hashes.Sha256(current));
            }
            Assert.Equal(Encoding.UTF8.GetBytes(text),current);
            if(CurrentLayoutPins.TryGetValue(original.RelativePath,out var expected))Assert.Equal(expected,hashes.Sha256(current));
        }
    }

    [Fact]
    public async Task ActualCurrentCanonicalSourcesPassTheExistingFullModelPreflight()
    {
        var hashes=new NativeContentHash();var receipts=new ArtifactReceiptVerifier(hashes);var strict=new StrictJsonInspector();
        var project=new FileArtifactStore(ProjectRoot,bytes=>hashes.Sha256(bytes.Span));
        var catalog=new FileArtifactStore(Path.Combine(DocumentationFixture.Current.Workspace,"remediation/catalog"),bytes=>hashes.Sha256(bytes.Span));
        var pins=new Artifact("contracts/source-pins.json",await project.ReadAsync("contracts/source-pins.json"),"application/json");
        var collector=new DocumentationSourceCollection(project,catalog,pins,new DocumentationModelSourcePlanner(strict),receipts,new CanonicalJson());
        var snapshot=await collector.CaptureAsync();
        var required=snapshot.Sources.Where(row=>row.Observation.Source.Role!=DocumentationInputRole.CanonicalContractOptional).Select(row=>row.Observation.Content!).ToArray();
        Assert.Equal(12,required.Length);Assert.Equal(15,snapshot.Sources.Count);
        foreach(var pin in CurrentLayoutPins)Assert.Equal(pin.Value,receipts.Describe(required.Single(artifact=>artifact.RelativePath==pin.Key)).Sha256);
        var models=new DocumentationModelReader();
        var preflight=new DocumentationModelPreflight(models,new DocumentationProjector(models),new ModelContractValidator(hashes),new ModuleGraphValidator(),strict);
        var result=preflight.Prepare(new(required,snapshot));
        Assert.True(result.Contracts.Validated);Assert.Equal(11,result.Contracts.Screens);Assert.Equal(76,result.Contracts.States);
        Assert.Equal(96,result.Modules.ClassCount);Assert.Empty(result.Modules.Issues);Assert.Equal(222,result.Ui.Artifacts.Count);
        Assert.Equal(11,result.Model.Screens.Count);
        await collector.VerifyUnchangedAsync(snapshot);
    }
}
