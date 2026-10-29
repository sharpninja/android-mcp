using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Mock"), Trait("Requirement", "TEST-BUILD-004"), Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class AidlInventoryRuntimeMockTests
{
    [Theory, MemberData(nameof(AidlInventoryRuntimeContractCases.Cases), MemberType = typeof(AidlInventoryRuntimeContractCases))]
    public void CorrectFactoryMocksShareOneStatelessConcernAcrossBothProfiles(string profile, string id)
    {
        var factory = new ScriptedFactory(id);
        AidlInventoryRuntimeContractCases.AssertComposition(profile, id, factory);
        Assert.Equal(new[] { profile, profile == "release-011" ? "current-candidate" : "release-011" }, factory.Profiles);
        Assert.All(factory.JavaAdmissions, admission => Assert.Null(admission));
        var text = AidlLexicalInventoryContractCases.Get(id).Text;
        Assert.Equal(new[] { text, text }, factory.Inputs);
        Assert.Equal(0, factory.DispatcherReads);
        Assert.Equal(2, factory.Disposals);
    }

    [Theory, MemberData(nameof(AidlInventoryRuntimeContractCases.Fields), MemberType = typeof(AidlInventoryRuntimeContractCases))]
    public void EveryLexicalFieldRejectsAnIndependentWrongConcern(string profile, string id, string property) =>
        Assert.NotNull(Record.Exception(() => AidlInventoryRuntimeContractCases.AssertComposition(profile, id, new ScriptedFactory(id, wrongField: property))));

    [Theory, MemberData(nameof(AidlInventoryRuntimeContractCases.Boundaries), MemberType = typeof(AidlInventoryRuntimeContractCases))]
    public void MissingTransientNullAndFailingConcernMocksCannotPass(string profile, string id, string fault) =>
        Assert.NotNull(Record.Exception(() => AidlInventoryRuntimeContractCases.AssertComposition(profile, id, new ScriptedFactory(id, fault))));

    [Fact]
    public void ExistingTwelveArgumentConstructionAndDeconstructionRemainValid() =>
        AidlInventoryRuntimeContractCases.AssertLegacyRecordShape();

    private sealed class ScriptedFactory : ISourcesRuntimeFactory
    {
        private readonly string id;
        private readonly string? fault;
        private readonly string? wrongField;
        private readonly SourcesRuntimeConcerns baseline;
        private readonly IAidlLexicalInventorySourceFacts inventory;
        public List<string> Profiles { get; } = [];
        public List<SourceJavaAdmission?> JavaAdmissions { get; } = [];
        public List<string> Inputs { get; } = [];
        public int DispatcherReads { get; private set; }
        public int Disposals { get; private set; }

        public ScriptedFactory(string id, string? fault = null, string? wrongField = null)
        {
            this.id = id; this.fault = fault; this.wrongField = wrongField;
            baseline = AidlInventoryRuntimeContractCases.ExistingConcerns();
            inventory = NewInventory();
        }

        private IAidlLexicalInventorySourceFacts NewInventory() => new ScriptedAidlLexicalInventory(text =>
        {
            Inputs.Add(text);
            if (fault == "provider-failure") throw new IOException("scripted concern failure");
            if (fault == "null-result") return null!;
            var test = AidlLexicalInventoryContractCases.Get(id);
            Assert.Equal(test.Text, text);
            return wrongField is null ? test.Correct() : AidlLexicalInventoryContractCases.Wrong(test, wrongField);
        });

        public ISourcesRuntime Create(SourceExtractionProfile profile, SourceJavaAdmission? java = null)
        {
            Profiles.Add(profile.Id); JavaAdmissions.Add(java);
            var concerns = baseline with { AidlInventory = fault == "missing-concern" ? null : fault == "transient-concern" ? NewInventory() : inventory };
            return new Runtime(concerns, () => { DispatcherReads++; throw new InvalidOperationException("No extraction is part of the lexical concern contract"); }, () => Disposals++);
        }
    }

    private sealed class Runtime(SourcesRuntimeConcerns concerns, Func<ISourcesExtractionDispatcher> dispatcher, Action dispose) : ISourcesRuntime
    {
        private bool disposed;
        public ISourcesExtractionDispatcher Dispatcher { get { ObjectDisposedException.ThrowIf(disposed, this); return dispatcher(); } }
        public SourcesRuntimeConcerns Concerns { get { ObjectDisposedException.ThrowIf(disposed, this); return concerns; } }
        public void Dispose() { if (disposed) return; disposed = true; dispose(); }
    }
}
