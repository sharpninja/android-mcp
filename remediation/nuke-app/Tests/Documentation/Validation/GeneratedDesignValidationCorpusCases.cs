using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using AndroidMcp.BuildTool.Documentation.Validation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation.Validation;

// Reads only the same typed expected source descriptors and existing pinned
// golden carriers. No filesystem enumeration or private source-route table.
public static class GeneratedDesignValidationCorpusCases
{
    public static IReadOnlyList<GeneratedDesignValidationCase> Create()
    {
        var fixture=DocumentationFixture.Current;var root=Path.Combine(fixture.Workspace,"remediation/design-c4");
        var packageData=DocumentationPackageCases.OriginalArtifacts();var sources=new List<Artifact>();
        var selected=DocumentationSelectedProjectSourceCases.ExpectedPlan(DocumentationProjectSourceScope.GeneratedDesign).Sources
            .Where(source=>source.Role!=DocumentationInputRole.CanonicalContractOptional).ToArray();
        Assert.Equal(18,selected.Length);
        foreach(var source in selected)
        {
            Artifact artifact;
            if(source.Role==DocumentationInputRole.ContractSnapshotRequired)
            {
                artifact=ModelContractFixture.Current.Copies.Single(copy=>copy.Snapshot.RelativePath==source.RelativePath).Snapshot;
                Assert.Equal(source.ExpectedSha256,ContentHash.Sha256(artifact.Bytes.Span));
            }
            else
            {
                var bytes=File.ReadAllBytes(Path.Combine(root,source.RelativePath));
                var additional=DocumentationSelectedProjectSourceCases.AdditionalSourcePins.SingleOrDefault(pin=>pin.Path==source.RelativePath);
                if(additional is not null){Assert.Equal(additional.Length,bytes.Length);Assert.Equal(additional.Sha256,ContentHash.Sha256(bytes));}
                else if(source.RelativePath==DocumentationAssetPaths.Blueprint)Assert.Equal(fixture.HealthMigration["afterSha256"]!.GetValue<string>(),ContentHash.Sha256(bytes));
                else if(source.RelativePath=="toolchain-lock.json")
                {
                    // GeneratedDesign selects the admitted current source. The
                    // original renderer cases intentionally retain the earlier
                    // byte fixture and its historical baseline hash.
                    var migrationBytes=File.ReadAllBytes(Path.Combine(fixture.Workspace,"remediation/nuke-planning/docs/evidence/toolchain-lock-redirect-migration.json"));
                    Assert.Equal("5902596d6c8ce160892db56b01317c89e669d01c3284d91f9731c4e0f6c19f7a",ContentHash.Sha256(migrationBytes));
                    var migration=JsonNode.Parse(migrationBytes)!.AsObject();
                    Assert.Equal(fixture.ExpectedHashes["remediation/design-c4/toolchain-lock.json"],migration["beforeSha256"]!.GetValue<string>());
                    Assert.Equal(migration["beforeSha256"]!.GetValue<string>(),ContentHash.Sha256(RendererProfileCases.Original().Toolchain.Bytes.Span));
                    Assert.Equal(migration["afterSha256"]!.GetValue<string>(),ContentHash.Sha256(bytes));
                    var redirected=RendererRedirectCases.Data.Select(row=>(RendererProfileCase)row[0]).Single(value=>value.Id=="REDIRECT-OBSERVED").Toolchain;
                    Assert.True(JsonNode.DeepEquals(JsonNode.Parse(redirected.Bytes.Span),JsonNode.Parse(bytes)),"Current lock must be exactly the already authored approved redirect migration.");
                }
                else if(fixture.ExpectedHashes.TryGetValue("remediation/design-c4/"+source.RelativePath,out var expected))Assert.Equal(expected,ContentHash.Sha256(bytes));
                else Assert.Equal(packageData.Single(value=>value.RelativePath==source.RelativePath).Bytes.ToArray(),bytes);
                artifact=new(source.RelativePath,bytes,source.MediaType);
            }
            Assert.Equal(source.RelativePath,artifact.RelativePath);sources.Add(artifact);
        }
        var artifacts=sources.ToDictionary(value=>value.RelativePath,StringComparer.Ordinal);
        // The generated artifact replaces its source path in the final union;
        // admitted source identities above are still checked independently.
        foreach(var artifact in fixture.Correct.Artifacts.Concat(packageData.Where(value=>value.RelativePath.StartsWith("docs/diagrams/",StringComparison.Ordinal))))
            artifacts[artifact.RelativePath]=artifact;
        Assert.Equal(260,artifacts.Count);
        var ordered=artifacts.Values.OrderBy(value=>value.RelativePath,StringComparer.Ordinal).ToArray();
        var tracePath=Path.Combine(fixture.Workspace,"remediation/nuke-planning/java/docsverify/generated-design-markdown-trace.fixture.json");
        var traceBytes=File.ReadAllBytes(tracePath);Assert.Equal("e1811c8320b4e1db432e807ee482900f8e8af9f212d98506ea9c9b7be4bae81a",ContentHash.Sha256(traceBytes));
        var trace=JsonNode.Parse(traceBytes)!.AsObject();var files=trace["files"]!.AsArray().Select(node=>node!.AsObject()).ToArray();
        Assert.Equal(10,files.Length);Assert.Equal(261,trace["markdownLinksChecked"]!.GetValue<int>());
        Assert.Equal(files.Select(row=>row["path"]!.GetValue<string>()).Order(StringComparer.Ordinal),ordered.Where(value=>value.RelativePath.EndsWith(".md",StringComparison.Ordinal)).Select(value=>value.RelativePath).Order(StringComparer.Ordinal));
        foreach(var row in files)
        {
            var artifact=artifacts[row["path"]!.GetValue<string>()];Assert.Equal(row["sha256"]!.GetValue<string>(),ContentHash.Sha256(artifact.Bytes.Span));Assert.Equal(row["length"]!.GetValue<long>(),artifact.Bytes.Length);
        }
        var expectedPaths=fixture.Inventory["docsVectorFixtures"]!.AsArray().Select(node=>node!["path"]!.GetValue<string>()["remediation/design-c4/".Length..]).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(221,expectedPaths.Length);Assert.Equal(221,ordered.Count(value=>value.RelativePath.StartsWith("docs/",StringComparison.Ordinal)&&value.RelativePath.EndsWith(".svg",StringComparison.Ordinal)));
        Assert.Equal(17,ordered.Count(value=>value.RelativePath.StartsWith("ui-svg/",StringComparison.Ordinal)&&value.RelativePath.EndsWith(".svg",StringComparison.Ordinal)));
        var model=new DocumentationModelReader().Read(sources.Single(value=>value.RelativePath==DocumentationAssetPaths.Blueprint));
        var input=new GeneratedDesignValidationInput(ordered,model,artifacts["architecture/workspace.dsl"],artifacts["docs/ui-asset-manifest.json"],DocumentationValidationPhase.BeforeManifest);
        var before=new GeneratedDesignValidationResult(input.Phase,new(0,261,221,17),expectedPaths,expectedPaths,[],[]);
        var checksum=DocumentationPackageCases.Checksums(new(input.Artifacts,[],ReadOnlyMemory<byte>.Empty));
        var afterInput=input with{Phase=DocumentationValidationPhase.AfterManifest,Artifacts=input.Artifacts.Append(checksum).ToArray()};
        return [new("DV-CORPUS-PINNED-BEFORE",input,before),new("DV-CORPUS-PINNED-AFTER",afterInput,before with{Phase=afterInput.Phase,Counters=before.Counters with{ChecksumsChecked=260}})];
    }
}
