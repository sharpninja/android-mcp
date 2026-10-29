using System.Text;
using System.Text.RegularExpressions;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

// Independently authored source-data migration. No generator, renderer or canonical writer supplies expected bytes.
public static class ToolingDocumentationMigrationCases
{
    public sealed record FileCase(string CanonicalPath,string RetainedName,string ProposedName,string BeforeSha256,string AfterSha256,string MediaType);
    public static readonly FileCase[] Files =
    [
        new("README.md","README.md","README.md","6169f84191b95dd3843ca5b72e95a80a96aae7b08a5402c3fdd48d6ada68c2f5","78d47af9952baecff756426fefd52a069e62aca070166eff807f47e8c7795516","text/markdown"),
        new("architecture/workspace.dsl","workspace.dsl","workspace.dsl","ee49fbdd60efac1c68356c5fa39ccb6c75609526fc580677405d61a0963d9281","bf1dd1206e4d0689154fdf296e9749653b0e32bc51cb8586501e675ebe953219","text/plain"),
        new("docs/Android MCP Bridge Design.md","design.md","design.md","0e3bcfcc202ade99cc9e19c5b376b42b848971f28ea624f145ccac066b46a71b","d364de8eb28f27dd80cf834d3758374f80206fe6f4a15fe9da172904ef99cac7","text/markdown"),
        new("architecture/code-diagrams.md","code-diagrams.md","code-diagrams.md","3a75a05e90a60231e1851f6cd23f779c887b0d4c8713c75a1bf24854f073b837","31c781d2e8d28c1fe39b52e2e31179b1e2fb4351532936b4ecfcab5f4c22cf7e","text/markdown")
    ];
    public static string Root
    {
        get
        {
            var directory=new DirectoryInfo(AppContext.BaseDirectory);
            while(directory is not null&&!File.Exists(Path.Combine(directory.FullName,"remediation/nuke-planning/docs/fixtures/pre-nuke-tooling.workspace.dsl")))directory=directory.Parent;
            return Path.Combine(directory?.FullName??throw new InvalidOperationException("Tooling migration fixtures unavailable"),"remediation/nuke-planning/docs");
        }
    }
    private static string CarrierPath(string path,string media)=>media=="text/markdown"?new DocumentationFixtureCarrierMap().Resolve(path):path;
    public static IReadOnlyList<Artifact> Proposed()=>Files.Select(file=>new Artifact(file.CanonicalPath,
        File.ReadAllBytes(Path.Combine(Root,CarrierPath("fixtures/current-tooling."+file.ProposedName,file.MediaType))),file.MediaType)).ToArray();
    public static string Retained(FileCase file)
    {
        var bytes=File.ReadAllBytes(Path.Combine(Root,CarrierPath("fixtures/pre-nuke-tooling."+file.RetainedName,file.MediaType)));
        Assert.Equal(file.BeforeSha256,DocumentationFixture.Hash(bytes));return Encoding.UTF8.GetString(bytes);
    }
    public static void AssertSnapshot(Func<IReadOnlyList<Artifact>> provider)
    {
        var expected=Proposed();var actual=provider();Assert.Equal(4,actual.Count);
        Assert.Equal(Files.Select(file=>file.CanonicalPath).Order(StringComparer.Ordinal),actual.Select(file=>file.RelativePath).Order(StringComparer.Ordinal));
        foreach(var file in Files)
        {
            _=Retained(file);var artifact=actual.Single(item=>item.RelativePath==file.CanonicalPath);
            Assert.Equal(file.MediaType,artifact.MediaType);Assert.Equal(file.AfterSha256,DocumentationFixture.Hash(artifact.Bytes.Span));
            Assert.Equal(expected.Single(item=>item.RelativePath==file.CanonicalPath).Bytes.ToArray(),artifact.Bytes.ToArray());
        }
        string Text(string path)=>Encoding.UTF8.GetString(actual.Single(item=>item.RelativePath==path).Bytes.Span);
        var oldDsl=Retained(Files[1]);var dsl=Text(Files[1].CanonicalPath);
        static string Between(string value,string start,string end)
        {var begin=value.IndexOf(start,StringComparison.Ordinal);Assert.True(begin>=0);var finish=value.IndexOf(end,begin,StringComparison.Ordinal);Assert.True(finish>begin);return value[begin..finish];}
        var oldAndroid=Between(oldDsl,"        bridge = softwareSystem","        owner -> bridge");
        oldAndroid=oldAndroid.Replace("            tooling = container \"Offline catalog tooling\" \"Source acquisition/extraction/schema validation and reference fixtures; no phone operations\" \"Python\"\n","",StringComparison.Ordinal);
        Assert.Equal(oldAndroid,Between(dsl,"        bridge = softwareSystem","        owner -> bridge"));
        static string[] ViewKeys(string source)=>Regex.Matches(source,@"^\s*(?:systemContext|container|component|dynamic|deployment)\s+\S+(?:\s+""ReferenceAndroid"")?\s+(\w+)\s+""",RegexOptions.Multiline).Select(match=>match.Groups[1].Value).ToArray();
        Assert.Equal(ViewKeys(oldDsl),ViewKeys(dsl));Assert.Equal(15,ViewKeys(dsl).Length);
        Assert.Equal(Regex.Matches(oldDsl,@"^\s*image \.\./docs/diagrams/[^\r\n]+",RegexOptions.Multiline).Select(match=>match.Value),Regex.Matches(dsl,@"^\s*image \.\./docs/diagrams/[^\r\n]+",RegexOptions.Multiline).Select(match=>match.Value));
        Assert.DoesNotContain("bridge.tooling",dsl,StringComparison.Ordinal);Assert.Contains("\"C# / .NET / NUKE\"",dsl,StringComparison.Ordinal);
        Assert.Single(Regex.Matches(dsl,@"containerInstance documentationSystem\.documentation"));
        var oldCode=Retained(Files[3]);var code=Text(Files[3].CanonicalPath);
        static string[] Puml(string value)=>Regex.Matches(value,@"```plantuml[^\n]*\n[\s\S]*?\n```",RegexOptions.Multiline).Select(match=>match.Value).ToArray();
        Assert.Equal(Puml(oldCode),Puml(code));Assert.Equal(6,Puml(code).Length);
        var design=Text(Files[2].CanonicalPath);var oldDesign=Retained(Files[2]);
        Assert.Equal(Between(oldDesign,"<!-- GENERATED UI NAVIGATION BEGIN -->","<!-- GENERATED UI NAVIGATION END -->"),Between(design,"<!-- GENERATED UI NAVIGATION BEGIN -->","<!-- GENERATED UI NAVIGATION END -->"));
        Assert.Contains("both the MCP server and management UI are Kotlin",design,StringComparison.Ordinal);
        Assert.Contains("thin adapter is not yet available",design,StringComparison.Ordinal);
        var readme=Text(Files[0].CanonicalPath);Assert.Contains("AndroidMcp.Build.csproj",readme,StringComparison.Ordinal);
        Assert.Contains("obsolete historical tooling",readme,StringComparison.Ordinal);Assert.Contains("adapter is not yet available",readme,StringComparison.Ordinal);
        Assert.Contains("browser-only inline",readme,StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("python3 tooling/bootstrap.py",readme,StringComparison.Ordinal);Assert.DoesNotContain("npm ci",readme,StringComparison.Ordinal);
    }
    public static IEnumerable<object[]> Controls=>Enumerable.Range(0,4).SelectMany(index=>new[]{"bytes","path","media","omission"}.Select(memberName=>new object[]{index,memberName}));
}

[Trait("Gate","Mock")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class ToolingDocumentationMigrationMockTests
{
    [Fact] public void CorrectIndependentlyAuthoredCurrentToolingDataPasses()=>ToolingDocumentationMigrationCases.AssertSnapshot(ToolingDocumentationMigrationCases.Proposed);
    [Theory,MemberData(nameof(ToolingDocumentationMigrationCases.Controls),MemberType=typeof(ToolingDocumentationMigrationCases))]
    public void EveryFileIdentityBytesAndOmissionControlIsRejected(int index,string field)
    {
        var wrong=ToolingDocumentationMigrationCases.Proposed().ToArray();var original=wrong[index];
        wrong[index]=field switch
        {
            "bytes"=>original with{Bytes=Encoding.UTF8.GetBytes("unapproved replacement")},
            "path"=>original with{RelativePath="../unapproved/source.md"},
            "media"=>original with{MediaType="application/octet-stream"},
            _=>original
        };
        IReadOnlyList<Artifact> response=field=="omission"?wrong.Where((_,position)=>position!=index).ToArray():wrong;
        Assert.NotNull(Record.Exception(()=>ToolingDocumentationMigrationCases.AssertSnapshot(()=>response)));
    }
}
