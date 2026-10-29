using AndroidMcp.BuildTool.Sources;
using AndroidMcp.BuildTool.Sources.Java;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Mock")]
[Trait("Requirement", "TEST-BUILD-004")]
public sealed class SourceProducerIdentityMockTests
{
    public static IEnumerable<object[]> Ownership => SourceProducerIdentityContractCases.Ids.SelectMany(id => new[] { "capture", "response" }.Select(wrong => new object[] { id, wrong }));
    public static IEnumerable<object[]> Fields => new[] { "managed", "java", "java-pinned" }.SelectMany(id => new[]
        { "producer", "runtime", "parser", "context", "assembly-pin", "file-set", "admission", "admission-length" }.Select(wrong => new object[] { id, wrong }));
    public static IEnumerable<object[]> Disposal => new[] { "java", "java-pinned", "java-unavailable", "java-identity-fault", "java-wrong-path", "java-wrong-pin" }.Select(id => new object[] { id });
    [Theory, MemberData(nameof(SourceProducerIdentityContractCases.Cases), MemberType = typeof(SourceProducerIdentityContractCases))]
    public void CorrectMocksProveRuntimeAssemblyJavaPinsAndLifetimeAssertions(string id) => SourceProducerIdentityContractCases.AssertContract(id, (fixture, rig) => new Scripted(fixture, rig));
    [Theory, MemberData(nameof(Ownership))]
    public void EveryWrongCaptureOrFailureResponseIsRejected(string id, string wrong) =>
        Assert.NotNull(Record.Exception(() => SourceProducerIdentityContractCases.AssertContract(id, (fixture, rig) => new Scripted(fixture, rig, wrong))));
    [Theory, MemberData(nameof(Fields))]
    public void EveryWrongIdentityOrPinnedAdmissionFieldIsRejected(string id, string wrong) =>
        Assert.NotNull(Record.Exception(() => SourceProducerIdentityContractCases.AssertContract(id, (fixture, rig) => new Scripted(fixture, rig, wrong))));
    [Theory, MemberData(nameof(Disposal))]
    public void EveryUndisposedEvaluatedJavaRuntimeIsRejected(string id) =>
        Assert.NotNull(Record.Exception(() => SourceProducerIdentityContractCases.AssertContract(id, (fixture, rig) => new Scripted(fixture, rig, "dispose"))));
    private sealed class Scripted(SourceProducerIdentityFixture fixture, SourceProducerIdentityRig rig, string? wrong = null) : ISourceProducerIdentityBuilder
    {
        public SourceProducerIdentityAdmission Create(SourceJavaAdmission? java = null)
        {
            Assert.Equal(fixture.Java, java); SourceProducerAssemblyObservation observation;
            try { observation = rig.Capture(); }
            catch (Exception) when (wrong == "response") { return fixture.Expected; }
            catch (Exception) when (wrong == "capture") { rig.Capture(); throw; }
            Assert.Equal(fixture.Assembly, observation);
            if (wrong == "capture") rig.Capture();
            if (fixture.Early) { if (wrong == "response") return fixture.Expected; throw fixture.Failure!; }
            IJavaSourceRuntime? handle = null;
            try
            {
                if (java is not null)
                {
                    try { handle = rig.Open(java); if (handle is IJavaSourceProducerIdentityRuntime evaluated) Assert.Equal(fixture.JavaIdentity, evaluated.Identity); }
                    catch (Exception) when (wrong == "response") { return fixture.Expected; }
                }
                if (fixture.Failure is not null && wrong != "response") throw fixture.Failure;
                if (fixture.Failure is null && wrong == "response") throw new IOException("Invented identity failure");
                var result = fixture.Expected; var files = result.Identity.ProducerFiles.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
                if (wrong == "assembly-pin") files["native/assembly"] = new('0', 64);
                if (wrong == "file-set") files["invented/python"] = new('0', 64);
                var identity = result.Identity with { ProducerFiles = files };
                identity = wrong switch
                { "producer" => identity with { ProducerId = "python-v7" }, "runtime" => identity with { RuntimeIdentity = "python3" },
                    "parser" => identity with { ParserIdentity = new('0', 64) }, "context" => identity with { ContextIdentity = new('0', 64) }, _ => identity };
                var admission = result.JavaAdmission;
                if (wrong == "admission") admission = admission is null ? new("invented") : admission with { ExpectedManifestSha256 = new('0', 64) };
                if (wrong == "admission-length") admission = admission is null ? new("invented", new('0', 64), 1) : admission with { ExpectedManifestLength = admission.ExpectedManifestLength + 1 };
                return new(identity, admission);
            }
            finally { if (wrong != "dispose") handle?.Dispose(); }
        }
    }
}
