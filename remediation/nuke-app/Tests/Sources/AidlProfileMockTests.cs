using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Mock"), Trait("Requirement", "TEST-BUILD-004"), Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class AidlProfileMockTests
{
    [Theory, MemberData(nameof(AidlProfileContractCases.Cases), MemberType = typeof(AidlProfileContractCases))]
    public void ExactCurrentAndFrozenAssertionsDiscriminateIndependentScriptedResults(string id)
    {
        var test = AidlProfileContractCases.Get(id);
        foreach (var dispatched in new[] { false, true })
        {
            AidlProfileContractCases.AssertContract(test, (_, _) => AidlProfileContractCases.Correct(test, dispatched), dispatched);
            foreach (var item in SourceMutationCases.For(test.Source))
            {
                var wrong = SourceMutationCases.Apply(test.Source, (SourceMutation)item[1]);
                Assert.NotNull(Record.Exception(() => AidlProfileContractCases.AssertContract(test, (_, _) => wrong with { Metadata = AidlProfileContractCases.Correct(test, dispatched).Metadata }, dispatched)));
            }
        }
        foreach (var member in test.Source.ExpectedRecords.Select((row, index) => (row, index)).Where(pair => pair.row["extras"] is JsonObject extras && !extras.ContainsKey("aidl_kind") && pair.row["owner"] is not null))
            foreach (var mode in new[] { "omit", "null", "true", "false" })
            {
                var wrong = AidlProfileContractCases.Correct(test, true); var extras = wrong.Records[member.index]["extras"]!.AsObject();
                if (test.Profile != SourceExtractionProfile.RELEASE011 && (mode == "true" && extras["interface_oneway"]?.GetValue<bool>() == true || mode == "false" && extras["interface_oneway"]?.GetValue<bool>() == false)) continue;
                if (test.Profile == SourceExtractionProfile.RELEASE011 && mode == "omit") continue;
                if (mode == "omit") extras.Remove("interface_oneway"); else extras["interface_oneway"] = mode == "null" ? null : JsonValue.Create(mode == "true");
                // Rows without a matched declaration may be retained unknown roots.
                if (!AidlProfileContractCases.ForProfile(test.Source, SourceExtractionProfile.CurrentCandidate).ExpectedRecords[member.index]["extras"]!.AsObject().ContainsKey("interface_oneway")) continue;
                Assert.NotNull(Record.Exception(() => AidlProfileContractCases.AssertContract(test, (_, _) => wrong, true)));
            }
        foreach (var key in AidlProfileContractCases.Identity(test).Select(pair => pair.Key))
        {
            var wrong = AidlProfileContractCases.Correct(test, true); wrong.Metadata!["source_identity"]!.AsObject().Remove(key);
            Assert.NotNull(Record.Exception(() => AidlProfileContractCases.AssertContract(test, (_, _) => wrong, true)));
        }
        if (test.Source.Text.Length > 0) Assert.NotNull(Record.Exception(() => AidlProfileContractCases.AssertContract(test, (source, _) =>
        {
            if (source.Bytes.Length > 0 && MemoryMarshal.TryGetArray(source.Bytes, out var bytes)) bytes.Array![bytes.Offset] ^= 1;
            return AidlProfileContractCases.Correct(test, true);
        }, true)));
    }
    [Theory, MemberData(nameof(AidlProfileContractCases.CurrentCases), MemberType = typeof(AidlProfileContractCases))]
    public void PairedProfileAssertionsRejectAdditionalMetadataAndRecordChanges(string id)
    {
        var current = AidlProfileContractCases.Get(id);
        ExtractionResult Correct(SourceDocument source, SourceExtractionProfile profile) => AidlProfileContractCases.Correct(AidlProfileContractCases.All.Single(test => test.Source.Id == current.Source.Id && test.Profile == profile), true);
        AidlProfileContractCases.AssertOnlyProfileDelta(current, Correct);
        Assert.NotNull(Record.Exception(() => AidlProfileContractCases.AssertOnlyProfileDelta(current, (source, profile) =>
        {
            var result = Correct(source, profile); if (profile == SourceExtractionProfile.RELEASE011) result.Metadata!["unexpected-release-field"] = true; return result;
        })));
    }
}
