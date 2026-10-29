using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Mock")]
[Trait("Requirement", "TEST-BUILD-004")]
public sealed class SourceRuntimeArchiveMockTests
{
    public static readonly string[] Faults = ["omit-archive", "omit-java", "missing-cache-kind", "extra-cache-kind", "wrong-binary-admission", "drop-concern", "invent-entry", "wrong-status", "wrong-issues", "wrong-profile", "mutate-input", "no-dispose", "double-dispose", "allow-disposed-cache"];
    public static IEnumerable<object[]> Controls => SourceRuntimeArchiveContractCases.Cases.SelectMany(data => Faults.Select(fault => data.Concat([fault]).ToArray()));
    [Theory, MemberData(nameof(SourceRuntimeArchiveContractCases.Cases), MemberType = typeof(SourceRuntimeArchiveContractCases))]
    public void CorrectMocksProveSeventhBinaryConcernRegistrationAndActualCacheDescriptorFlags(string profile, bool java, bool partial) =>
        SourceRuntimeArchiveContractCases.AssertContract(profile, java, partial, (loader, archive) => new ScriptedFactory(loader, archive));
    [Theory, MemberData(nameof(Controls))]
    public void EachIncorrectCompositionCacheCapabilityEvidenceOrLifetimeMockIsRejected(string profile, bool java, bool partial, string fault)
    {
        // Java-only lifetime/provider controls use a Java-selected runtime so
        // an omitted operation cannot pass merely because it was not required.
        if (fault is "omit-java" or "no-dispose" or "double-dispose") java = true;
        Assert.NotNull(Record.Exception(() => SourceRuntimeArchiveContractCases.AssertContract(profile, java, partial, (loader, archive) => new ScriptedFactory(loader, archive, fault))));
    }
    private sealed class ScriptedFactory(RecordingSourceJavaLoader loader, RecordingSdkAnnotationArchive archive, string? fault = null) : ISourcesRuntimeFactory
    {
        public ISourcesRuntime Create(SourceExtractionProfile profile, SourceJavaAdmission? admission = null)
        {
            var resource = admission is null ? null : loader.Open(admission);
            var kinds = SourceRuntimeArchiveContractCases.ManagedKinds.Concat(resource is null || fault == "omit-java" ? Array.Empty<string>() : ["java-source"]).ToList();
            if (fault == "omit-archive") kinds.Remove("sdk-annotations");
            var dispatcher = new ScriptedSourcesExtractionDispatcher(kinds.Order(StringComparer.Ordinal).ToArray(), (source, kind, scope) =>
            {
                Assert.Equal("sdk-annotations", kind); Assert.Null(scope);
                if (fault == "mutate-input") { Assert.True(MemoryMarshal.TryGetArray(source.Bytes, out var raw)); raw.Array![raw.Offset] ^= 1; }
                if (fault == "wrong-binary-admission") return new([], [new("source-encoding-unsupported", "Incorrectly decoded binary", null, null)], StructuralParseStatus.Unsupported);
                var report = archive.Parse(source.Bytes);
                var issues = report["issues"]!.AsArray().Select(issue => new SourceIssue(issue!["code"]!.GetValue<string>(), issue["reason"]!.GetValue<string>(), null, null)).ToArray();
                return new(fault == "invent-entry" ? [new JsonObject { ["canonical"] = "invented" }] : [],
                    fault == "wrong-issues" ? issues.Length == 0 ? [new("invented", "Invented", null, null)] : [] : issues,
                    fault == "wrong-status" ? StructuralParseStatus.Unsupported : report["status"]!.GetValue<string>() == "partial" ? StructuralParseStatus.Partial : StructuralParseStatus.Clean,
                    new JsonObject { ["annotation_archive"] = fault == "drop-concern" ? new JsonObject() : report.DeepClone(),
                        ["source_identity"] = new JsonObject { ["sha256"] = SourceDispatcherCase.HashFf, ["profile_id"] = fault == "wrong-profile" ? "wrong" : profile.Id } });
            });
            return new Runtime(dispatcher, resource, fault);
        }
    }
    private sealed class Runtime(ISourcesExtractionDispatcher dispatcher, IJavaSourceRuntime? java, string? fault) : ISourcesCacheRuntime
    {
        private bool disposed;
        public ISourcesExtractionDispatcher Dispatcher => dispatcher;
        public SourcesRuntimeConcerns Concerns => throw new InvalidOperationException("Old factory tests retain full concern composition assertions");
        public IReadOnlySet<string> CacheProducerKinds
        {
            get
            {
                ObjectDisposedException.ThrowIf(disposed && fault != "allow-disposed-cache", this);
                var kinds = new HashSet<string>(StringComparer.Ordinal);
                if (fault != "missing-cache-kind") kinds.Add("sdk-api-index");
                if (java is not null) kinds.Add("java-source");
                if (fault == "extra-cache-kind") kinds.Add("manifest");
                return kinds;
            }
        }
        public void Dispose()
        { if (disposed && fault != "double-dispose") return; disposed = true; if (fault != "no-dispose") java?.Dispose(); }
    }
}
