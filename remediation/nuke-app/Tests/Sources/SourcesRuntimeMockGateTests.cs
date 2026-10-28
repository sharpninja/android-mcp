using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using AndroidMcp.BuildTool.Sources.Java;
using Xunit;
using Xunit.Sdk;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Mock")]
[Trait("Requirement", "TEST-BUILD-004")]
public sealed class SourcesRuntimeMockGateTests
{
    [Theory, MemberData(nameof(SourcesRuntimeContractCases.Cases), MemberType = typeof(SourcesRuntimeContractCases))]
    public void CorrectCompositionMockExercisesSameAssertions(string profile, bool java) => SourcesRuntimeContractCases.AssertComposition(profile, java, Rig());
    public static IEnumerable<object[]> CompositionControls => SourcesRuntimeContractCases.ManagedKinds.Select(x => new object[] { "omit:" + x })
        .Concat(new[] { "wrong-permission-metadata", "invent-permission-entry", "separate-callback", "separate-header", "wrong-permission-concern", "omit-java", "wrong-admission", "early-disposal" }.Select(x => new object[] { x }));
    [Theory, MemberData(nameof(CompositionControls))]
    public void EveryIndividuallyWrongCompositionMockIsRejected(string fault) => Mismatch(() => SourcesRuntimeContractCases.AssertComposition("release-011", true, Rig(fault)));
    [Theory, InlineData(false), InlineData(true)]
    public void CorrectLifecycleMockExercisesSameAssertions(bool java) => SourcesRuntimeContractCases.AssertLifecycle(java, Rig());
    [Theory, InlineData("omit-disposal"), InlineData("double-disposal"), InlineData("allow-disposed-access")]
    public void IndividuallyWrongLifecycleMocksAreRejected(string fault) => Mismatch(() => SourcesRuntimeContractCases.AssertLifecycle(true, Rig(fault)));
    [Fact] public void CorrectNullProfilePreflightMock() => SourcesRuntimeContractCases.AssertNullProfile(Rig());
    [Fact] public void WrongNullProfileOrderingMockIsRejected() => Mismatch(() => SourcesRuntimeContractCases.AssertNullProfile(Rig("late-profile")));
    [Theory, InlineData("format"), InlineData("io"), InlineData("dll"), InlineData("cancel")]
    public void CorrectAdmissionFailureMockPreservesFailure(string kind) => SourcesRuntimeContractCases.AssertAdmissionFailure(Rig(), Failure(kind));
    [Theory, InlineData("format"), InlineData("io"), InlineData("dll"), InlineData("cancel")]
    public void ReplacedAdmissionFailuresAreRejected(string kind) => Mismatch(() => SourcesRuntimeContractCases.AssertAdmissionFailure(Rig("replace-failure"), Failure(kind)));
    [Theory, InlineData("aidl"), InlineData("unexpected"), InlineData("")]
    public void CorrectWrongJavaKindCleanupMock(string kind) => SourcesRuntimeContractCases.AssertWrongJavaKind(Rig(), kind);
    [Theory, InlineData("aidl"), InlineData("unexpected"), InlineData("")]
    public void OmittedJavaRejectionCleanupIsRejected(string kind) => Mismatch(() => SourcesRuntimeContractCases.AssertWrongJavaKind(Rig("omit-rejection-cleanup"), kind));
    private static Exception Failure(string kind) => kind switch
    { "format" => new FormatException("manifest invalid"), "io" => new IOException("asset missing"), "dll" => new DllNotFoundException("exports absent"), _ => new OperationCanceledException("cancelled") };
    private static void Mismatch(Action assertion) => Assert.ThrowsAny<XunitException>(assertion);
    private static SourceRuntimeRig Rig(string? fault = null)
    {
        var java = new RecordingSourceJavaLoader(); var permission = new RecordingPermissionReference();
        return new(new ScriptedFactory(java, permission, fault), java, permission);
    }

    // Scripted providers contain independently pinned legacy fixture outputs.
    // This is a mock factory, not a parser or a second production composition.
    private sealed class ScriptedFactory(RecordingSourceJavaLoader java, RecordingPermissionReference permission, string? fault) : ISourcesRuntimeFactory
    {
        public ISourcesRuntime Create(SourceExtractionProfile profile, SourceJavaAdmission? admission = null)
        {
            if (fault != "late-profile") ArgumentNullException.ThrowIfNull(profile);
            IJavaSourceRuntime? resource = null;
            if (admission is not null)
            {
                try { resource = java.Open(fault == "wrong-admission" ? new("wrong/manifest.json") : admission); }
                catch (Exception error) { if (fault == "replace-failure") throw new InvalidOperationException("replaced", error); throw; }
            }
            ArgumentNullException.ThrowIfNull(profile);
            if (resource is not null && resource.Extractor.Kind != "java-source")
            {
                if (fault != "omit-rejection-cleanup") resource.Dispose();
                throw new ArgumentException("Unexpected Java source kind");
            }
            var providers = SourcesRuntimeContractCases.Routes.GroupBy(x => x.Test.Kind, StringComparer.Ordinal).Select(group =>
            {
                var fixture = group.First().Test;
                return (ISourceExtractor)new ScriptedSourceExtractor(fixture.Kind, _ => fixture.CorrectResult());
            }).ToList();
            providers.Add(new ScriptedSourceExtractor("permission-reference", document =>
            {
                var result = permission.Parse(document.Bytes);
                return new(fault == "invent-permission-entry" ? [new JsonObject { ["canonical"] = "invented" }] : [], [], StructuralParseStatus.Clean,
                    new JsonObject { ["permission_reference"] = fault == "wrong-permission-metadata" ? new JsonObject() : result.DeepClone() });
            }));
            if (resource is not null && fault != "omit-java") providers.Add(resource.Extractor);
            if (fault?.StartsWith("omit:", StringComparison.Ordinal) == true) providers.RemoveAll(x => x.Kind == fault[5..]);
            if (fault == "early-disposal") resource?.Dispose();
            var canonical = new CanonicalJson();
            var facts = new SourceFactProducer(canonical); var headers = new SourceTypeFacts(new JvmSourceLinker(), canonical);
            var concerns = new SourcesRuntimeConcerns(new PermissionExpressionSourceFacts(), fault == "wrong-permission-concern" ? new RecordingPermissionReference() : permission,
                headers, fault == "separate-header" ? new SourceTypeFacts(new JvmSourceLinker(), canonical) : headers,
                facts, fault == "separate-callback" ? new SourceFactProducer(canonical) : facts, facts,
                new SoongSourceFacts(new GlobMatcher()), new ApexManifestSourceFacts(), new NativeAbiSourceFacts(), new PublishedInterfaceHashSourceFacts(), new SourceHtmlDecoder());
            return new ScriptedRuntime(new SourcesExtractionDispatcher(providers, profile), concerns, resource, fault);
        }
    }
    private sealed class ScriptedRuntime(ISourcesExtractionDispatcher dispatcher, SourcesRuntimeConcerns concerns, IJavaSourceRuntime? java, string? fault) : ISourcesRuntime
    {
        private bool disposed;
        public ISourcesExtractionDispatcher Dispatcher { get { Check(); return dispatcher; } }
        public SourcesRuntimeConcerns Concerns { get { Check(); return concerns; } }
        private void Check() { if (disposed && fault != "allow-disposed-access") throw new ObjectDisposedException(nameof(ScriptedRuntime)); }
        public void Dispose()
        {
            if (disposed && fault != "double-disposal") return;
            disposed = true;
            if (fault != "omit-disposal") java?.Dispose();
        }
    }
}
