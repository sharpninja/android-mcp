using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public static class AidlInventoryRuntimeContractCases
{
    public static readonly string[] FixtureIds = ["LEX-NESTED-ORIGINAL", "LEX-UNBALANCED", "LEX-NO-DECLARATION"];
    public static IEnumerable<object[]> Cases() => new[] { "release-011", "current-candidate" }.SelectMany(profile =>
        FixtureIds.Select(id => new object[] { profile, id }));
    public static IEnumerable<object[]> Fields() => Cases().SelectMany(row =>
        new[] { "Declarations", "Statements", "Reconciled", "Issues", "ExpectedRecords" }
            .Select(property => new object[] { row[0], row[1], property }));
    public static IEnumerable<object[]> Boundaries() => Cases().SelectMany(row =>
        new[] { "missing-concern", "transient-concern", "null-result", "provider-failure" }
            .Select(fault => new object[] { row[0], row[1], fault }));

    public static void AssertComposition(string profileId, string fixtureId, ISourcesRuntimeFactory factory)
    {
        var profile = SourceExtractionProfile.Resolve(profileId);
        var otherProfile = profileId == "release-011" ? SourceExtractionProfile.CurrentCandidate : SourceExtractionProfile.RELEASE011;
        using var first = factory.Create(profile);
        var firstConcerns = first.Concerns;
        Assert.Same(firstConcerns, first.Concerns);
        var inventory = Assert.IsAssignableFrom<IAidlLexicalInventorySourceFacts>(firstConcerns.AidlInventory);
        Assert.Same(inventory, first.Concerns.AidlInventory);
        AidlLexicalInventoryContractCases.AssertContract(AidlLexicalInventoryContractCases.Get(fixtureId), inventory);
        using var second = factory.Create(otherProfile);
        Assert.Same(inventory, second.Concerns.AidlInventory);
        AidlLexicalInventoryContractCases.AssertContract(AidlLexicalInventoryContractCases.Get(fixtureId), second.Concerns.AidlInventory!);
    }

    // Reuse the existing admitted bundle for unrelated concerns; do not copy its provider table.
    internal static SourcesRuntimeConcerns ExistingConcerns()
    {
        using var runtime = new SourcesRuntimeFactory().Create(SourceExtractionProfile.CurrentCandidate);
        return runtime.Concerns;
    }

    public static void AssertLegacyRecordShape()
    {
        var old = ExistingConcerns();
        var copy = new SourcesRuntimeConcerns(old.PermissionExpressions, old.PermissionReference, old.AnnotationTargets,
            old.TypeHeaders, old.Callbacks, old.Taxonomy, old.Applicability, old.Soong, old.Apex, old.NativeAbi, old.PublishedHashes, old.Html);
        Assert.Null(copy.AidlInventory);
        var (permission, reference, targets, headers, callbacks, taxonomy, applicability, soong, apex, abi, hashes, html) = copy;
        Assert.Same(old.PermissionExpressions, permission); Assert.Same(old.PermissionReference, reference);
        Assert.Same(old.AnnotationTargets, targets); Assert.Same(old.TypeHeaders, headers);
        Assert.Same(old.Callbacks, callbacks); Assert.Same(old.Taxonomy, taxonomy); Assert.Same(old.Applicability, applicability);
        Assert.Same(old.Soong, soong); Assert.Same(old.Apex, apex); Assert.Same(old.NativeAbi, abi);
        Assert.Same(old.PublishedHashes, hashes); Assert.Same(old.Html, html);
    }
}
