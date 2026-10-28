using System.Text.Json;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public sealed record DocumentationFixtureCarrierCase(string VirtualPath,string PhysicalPath,string Sha256,int Length)
{public override string ToString()=>VirtualPath;}
public static class DocumentationFixtureCarrierCases
{
    public static IEnumerable<object[]> Cases
    {
        get
        {
            var path=Path.Combine(ToolingDocumentationMigrationCases.Root,"evidence/markdown-fixture-carrier-plan.json");
            var bytes=File.ReadAllBytes(path);Assert.Equal("3011e3639a0c0b4c253c6fdbcb2625bd065701a03e4164378a36a3ebf4884235",DocumentationFixture.Hash(bytes));
            using var json=JsonDocument.Parse(bytes);const string prefix="remediation/nuke-planning/docs/";
            var rows=json.RootElement.GetProperty("files").EnumerateArray().ToArray();Assert.Equal(7,rows.Length);
            foreach(var row in rows)
            {
                var original=row.GetProperty("virtualFixturePath").GetString()!;var physical=row.GetProperty("plannedPhysicalPath").GetString()!;
                Assert.StartsWith(prefix,original,StringComparison.Ordinal);Assert.StartsWith(prefix,physical,StringComparison.Ordinal);
                yield return [new DocumentationFixtureCarrierCase(original[prefix.Length..],physical[prefix.Length..],row.GetProperty("sha256").GetString()!,row.GetProperty("length").GetInt32())];
            }
        }
    }
    public static IEnumerable<object[]> UnknownPaths=>new[]{"fixtures/not-admitted.md","../../outside.md","README.md","docs/ui-blueprint.md","docs/Android MCP Bridge Design.md","architecture/code-diagrams.md","fixtures/tooling-source-migration-notes.md"}.Select(path=>new object[]{path});
    public static void AssertCarrier(DocumentationFixtureCarrierCase test,IDocumentationFixtureCarrierMap provider,Func<string,byte[]>? read=null)
    {
        var physical=provider.Resolve(test.VirtualPath);Assert.Equal(test.PhysicalPath,physical);
        var bytes=(read??(relative=>File.ReadAllBytes(Path.Combine(ToolingDocumentationMigrationCases.Root,relative))))(physical);
        Assert.Equal(test.Length,bytes.Length);Assert.Equal(test.Sha256,DocumentationFixture.Hash(bytes));
    }
    public static void AssertUnknown(string path,IDocumentationFixtureCarrierMap provider)
        =>Assert.Throws<KeyNotFoundException>(()=>provider.Resolve(path));
}
public sealed class ScriptedDocumentationFixtureCarrier(Func<string,string> resolve):IDocumentationFixtureCarrierMap
{
    public string Resolve(string virtualFixturePath)=>resolve(virtualFixturePath);
}
[Trait("Gate","Mock")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class DocumentationFixtureCarrierMockTests
{
    [Theory,MemberData(nameof(DocumentationFixtureCarrierCases.Cases),MemberType=typeof(DocumentationFixtureCarrierCases))]
    public void CorrectExplicitMapAndRetainedDataMocksPass(DocumentationFixtureCarrierCase test)
        =>DocumentationFixtureCarrierCases.AssertCarrier(test,new ScriptedDocumentationFixtureCarrier(_=>test.PhysicalPath));
    [Theory,MemberData(nameof(DocumentationFixtureCarrierCases.Cases),MemberType=typeof(DocumentationFixtureCarrierCases))]
    public void EveryWrongPhysicalPathIsRejected(DocumentationFixtureCarrierCase test)
        =>Assert.NotNull(Record.Exception(()=>DocumentationFixtureCarrierCases.AssertCarrier(test,new ScriptedDocumentationFixtureCarrier(_=>test.VirtualPath))));
    [Theory,MemberData(nameof(DocumentationFixtureCarrierCases.Cases),MemberType=typeof(DocumentationFixtureCarrierCases))]
    public void EachDataTamperIsRejectedWithoutChangingAnyFixture(DocumentationFixtureCarrierCase test)
        =>Assert.NotNull(Record.Exception(()=>DocumentationFixtureCarrierCases.AssertCarrier(test,new ScriptedDocumentationFixtureCarrier(_=>test.PhysicalPath),relative=>
        {var bytes=File.ReadAllBytes(Path.Combine(ToolingDocumentationMigrationCases.Root,relative));bytes[0]^=1;return bytes;})));
    [Theory,MemberData(nameof(DocumentationFixtureCarrierCases.UnknownPaths),MemberType=typeof(DocumentationFixtureCarrierCases))]
    public void UnknownAndCurrentCanonicalPathsAreNotFixtureRoutes(string path)
        =>DocumentationFixtureCarrierCases.AssertUnknown(path,new ScriptedDocumentationFixtureCarrier(_=>throw new KeyNotFoundException(path)));
    [Theory,MemberData(nameof(DocumentationFixtureCarrierCases.UnknownPaths),MemberType=typeof(DocumentationFixtureCarrierCases))]
    public void EachUnknownOrCanonicalPassThroughIsRejected(string path)
        =>Assert.NotNull(Record.Exception(()=>DocumentationFixtureCarrierCases.AssertUnknown(path,new ScriptedDocumentationFixtureCarrier(value=>value))));
}
