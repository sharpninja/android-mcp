using System.IO.Compression;
using System.Text;
using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using AndroidMcp.BuildTool.Publication;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public sealed record DocumentationPackageCase(string Id,DocumentationPackageInput Input,string Error)
{public override string ToString()=>Id;}
public sealed class RecordingDocumentationPackageChecker(bool reject):IPublicationChecker
{
    public List<(ReadOnlyMemory<byte> Policy,IReadOnlyList<PublicationFile> Files)> Calls {get;}=[];
    public PublicationCheckResult Check(ReadOnlyMemory<byte> policy,IReadOnlyList<PublicationFile> files)
    {Calls.Add((policy,files));return new(files.Count,files.Sum(f=>f.Length),reject?[new("DENIED","tooling/old.py")]:[]);}
}
public static class DocumentationPackageCases
{
    public const string ChecksumPath="docs/checksums.sha256";
    public static IReadOnlyList<Artifact> OriginalArtifacts()
    {
        var fixture=DocumentationFixture.Current;var result=fixture.Correct.Artifacts.ToList();
        using var pins=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixture.Workspace,"remediation/nuke-planning/docs/saved-fixture-inventory.json")));
        // The saved diagram bytes remain independent original fixtures. They do not prove native rendering.
        foreach(var pin in pins.RootElement.GetProperty("docsVectorFixtures").EnumerateArray().Where(p=>p.GetProperty("path").GetString()!.StartsWith("remediation/design-c4/docs/diagrams/",StringComparison.Ordinal)))
        {var path=pin.GetProperty("path").GetString()!;var bytes=File.ReadAllBytes(Path.Combine(fixture.Workspace,"remediation/nuke-planning/docs/fixtures/architecture-svg-original",Path.GetFileName(path)));Assert.Equal(pin.GetProperty("sha256").GetString(),DocumentationFixture.Hash(bytes));result.Add(new(path["remediation/design-c4/".Length..],bytes,"image/svg+xml"));}
        Assert.Equal(21,result.Count(a=>a.RelativePath.StartsWith("docs/diagrams/",StringComparison.Ordinal)));
        Assert.Equal(221,result.Count(a=>a.RelativePath.StartsWith("docs/",StringComparison.Ordinal)&&a.RelativePath.EndsWith(".svg",StringComparison.Ordinal)));
        result.Add(fixture.Input.BlueprintMarkdown);
        foreach(var path in new[]{"architecture/workspace.dsl","architecture/code-diagrams.md"})
        {var bytes=fixture.ReadOriginalDesignBytes(path);Assert.Equal(fixture.ExpectedHashes["remediation/design-c4/"+path],DocumentationFixture.Hash(bytes));result.Add(new(path,bytes,"text/plain"));}
        result.Add(new("architecture/render-skin.puml",Encoding.UTF8.GetBytes("skinparam defaultFontName DejaVu Sans\nskinparam backgroundColor white\nskinparam shadowing false\n"),"text/plain"));
        result.Add(new("build-tool/AndroidMcp.Build.csproj",Encoding.UTF8.GetBytes("<Project Sdk=\"Microsoft.NET.Sdk\"/>\n"),"application/xml"));
        result.Add(new("build-tool/Modules/Example.cs",Encoding.UTF8.GetBytes("namespace AndroidMcp.BuildTool;\n"),"text/plain"));
        return result;
    }
    public static DocumentationPackageCase Original()
    {
        var artifacts=OriginalArtifacts();var policy=JsonSerializer.SerializeToUtf8Bytes(new{maximum_file_bytes=64L*1024*1024,allow=new[]{"docs/*","architecture/*","ui-svg/*","storyboards/*","build-tool/*"},deny=new[]{"*.py","*.cjs","*.js","*/bin/*","*/obj/*","*/.tools/*","*.png","*.exe"}});
        return new("PACKAGE-FULL-FIXTURE",new(artifacts,artifacts.Select(a=>a.RelativePath).ToArray(),policy),"");
    }
    public static IEnumerable<object[]> Data
    {
        get
        {
            var original=Original();var rows=original.Input.Artifacts;
            return new[]
            {
                original,
                original with{Id="PACKAGE-MISSING-VECTOR",Input=original.Input with{Artifacts=rows.Where(a=>a.RelativePath!=rows.First(a=>a.RelativePath.StartsWith("docs/diagrams/",StringComparison.Ordinal)).RelativePath).ToArray()},Error="PACKAGE_MISSING"},
                original with{Id="PACKAGE-RESERVED-CHECKSUM",Input=original.Input with{Artifacts=rows.Append(new Artifact(ChecksumPath,Encoding.UTF8.GetBytes("untrusted"),"text/plain")).ToArray()},Error="PACKAGE_RESERVED"},
                original with{Id="PACKAGE-POLICY-REJECTION",Input=original.Input with{Artifacts=rows.Append(new Artifact("tooling/old.py",Encoding.UTF8.GetBytes("retired source fixture"),"text/plain")).ToArray()},Error="PACKAGE_POLICY"}
            }.Select(c=>new object[]{c});
        }
    }
    public static Artifact Checksums(DocumentationPackageInput input)=>new(ChecksumPath,Encoding.UTF8.GetBytes(string.Concat(input.Artifacts.OrderBy(a=>a.RelativePath,StringComparer.Ordinal).Select(a=>DocumentationFixture.Hash(a.Bytes.Span)+"  "+a.RelativePath+"\n"))),"text/plain");
    public static Artifact FixtureArchive(IReadOnlyList<Artifact> artifacts,string mutation="")
    {
        using var buffer=new MemoryStream();
        using(var archive=new ZipArchive(buffer,ZipArchiveMode.Create,true))
        {
            var ordered=artifacts.OrderBy(a=>a.RelativePath,StringComparer.Ordinal).ToArray();if(mutation=="order")Array.Reverse(ordered);
            if(mutation=="missing")ordered=ordered.Skip(1).ToArray();if(mutation=="extra")ordered=ordered.Append(new Artifact("docs/extra",new byte[]{1},"text/plain")).ToArray();
            for(var index=0;index<ordered.Length;index++)
            {
                var artifact=ordered[index];var entry=archive.CreateEntry(index==0&&mutation=="path"?"wrong":artifact.RelativePath,CompressionLevel.NoCompression);
                entry.LastWriteTime=new DateTimeOffset(index==0&&mutation=="date"?1981:1980,1,1,0,0,0,TimeSpan.Zero);entry.ExternalAttributes=index==0&&mutation=="attributes"?1:0;
                using var stream=entry.Open();stream.Write(index==0&&mutation=="bytes"?new byte[]{1}:artifact.Bytes.Span);
            }
        }
        return new("package/android-mcp-design-source.zip",buffer.ToArray(),"application/zip");
    }
    public static void AssertContract(DocumentationPackageCase test,RecordingDocumentationPackageChecker checker,IDocumentationPackageBuilder provider)
    {
        var snapshots=test.Input.Artifacts.ToDictionary(a=>a.RelativePath,a=>a.Bytes.ToArray(),StringComparer.Ordinal);var policy=test.Input.Policy.ToArray();
        if(test.Error.Length>0)Assert.Equal(test.Error,Assert.Throws<ArchitectureAssetException>(()=>provider.Build(test.Input)).Code);
        else
        {
            var result=provider.Build(test.Input);var checksums=Checksums(test.Input);Assert.Equal(checksums.RelativePath,result.Checksums.RelativePath);Assert.Equal(checksums.MediaType,result.Checksums.MediaType);Assert.Equal(checksums.Bytes.ToArray(),result.Checksums.Bytes.ToArray());
            Assert.Equal("package/android-mcp-design-source.zip",result.Archive.RelativePath);Assert.Equal("application/zip",result.Archive.MediaType);
            using var buffer=new MemoryStream(result.Archive.Bytes.ToArray(),false);using var archive=new ZipArchive(buffer,ZipArchiveMode.Read);
            var expected=test.Input.Artifacts.Append(checksums).OrderBy(a=>a.RelativePath,StringComparer.Ordinal).ToArray();Assert.Equal(expected.Select(a=>a.RelativePath),archive.Entries.Select(e=>e.FullName));
            foreach(var artifact in expected)
            {
                var entry=archive.GetEntry(artifact.RelativePath)!;using var stream=entry.Open();using var bytes=new MemoryStream();stream.CopyTo(bytes);
                Assert.Equal(artifact.Bytes.ToArray(),bytes.ToArray());Assert.Equal(artifact.Bytes.Length,entry.Length);Assert.Equal(new DateTime(1980,1,1),entry.LastWriteTime.DateTime);Assert.Equal(0,entry.ExternalAttributes);
            }
            var repeated=provider.Build(test.Input);Assert.Equal(result.Archive.Bytes.ToArray(),repeated.Archive.Bytes.ToArray());
        }
        if(test.Error is "PACKAGE_MISSING" or "PACKAGE_RESERVED")Assert.Empty(checker.Calls);
        else
        {
            Assert.Equal(test.Error.Length==0?2:1,checker.Calls.Count);
            foreach(var call in checker.Calls){Assert.Equal(policy,call.Policy.ToArray());Assert.Equal(test.Input.Artifacts.Select(a=>new PublicationFile(a.RelativePath,a.Bytes.Length)),call.Files);}
        }
        Assert.Equal(policy,test.Input.Policy.ToArray());foreach(var artifact in test.Input.Artifacts)Assert.Equal(snapshots[artifact.RelativePath],artifact.Bytes.ToArray());
    }
}
public sealed class ScriptedDocumentationPackageBuilder(DocumentationPackageCase test,RecordingDocumentationPackageChecker checker,string mutation=""):IDocumentationPackageBuilder
{
    public DocumentationPackage Build(DocumentationPackageInput input)
    {
        Assert.Same(test.Input,input);
        if(test.Error is "PACKAGE_MISSING" or "PACKAGE_RESERVED")throw new ArchitectureAssetException(mutation=="outcome"?"WRONG":test.Error,"Scripted preflight rejection");
        var files=input.Artifacts.Select(a=>new PublicationFile(a.RelativePath,a.Bytes.Length)).ToArray();
        checker.Check(mutation=="policy"?new byte[]{1}:input.Policy,mutation=="policy-files"?files.Skip(1).ToArray():files);
        if(test.Error.Length>0||mutation=="outcome")throw new ArchitectureAssetException(mutation=="outcome"?"WRONG":test.Error,"Scripted package rejection");
        var checksums=DocumentationPackageCases.Checksums(input);if(mutation=="checksums")checksums=checksums with{Bytes=Encoding.UTF8.GetBytes("wrong")};
        if(mutation=="checksum-path")checksums=checksums with{RelativePath="wrong"};if(mutation=="checksum-media")checksums=checksums with{MediaType="wrong"};
        var embedded=mutation=="embedded-checksum"?checksums with{Bytes=Encoding.UTF8.GetBytes("wrong")}:checksums;
        var archive=DocumentationPackageCases.FixtureArchive(input.Artifacts.Append(embedded).ToArray(),mutation);
        if(mutation=="archive-path")archive=archive with{RelativePath="wrong"};if(mutation=="archive-media")archive=archive with{MediaType="wrong"};
        return new(archive,checksums);
    }
}
[Trait("Gate","Mock")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class DocumentationPackageMockTests
{
    [Theory,MemberData(nameof(DocumentationPackageCases.Data),MemberType=typeof(DocumentationPackageCases))]
    public void CorrectSourcePackageMocksPass(DocumentationPackageCase test){var checker=new RecordingDocumentationPackageChecker(test.Error=="PACKAGE_POLICY");DocumentationPackageCases.AssertContract(test,checker,new ScriptedDocumentationPackageBuilder(test,checker));}
    [Theory,MemberData(nameof(DocumentationPackageCases.Data),MemberType=typeof(DocumentationPackageCases))]
    public void WrongPackageOutcomeRejected(DocumentationPackageCase test){var checker=new RecordingDocumentationPackageChecker(false);Assert.NotNull(Record.Exception(()=>DocumentationPackageCases.AssertContract(test,checker,new ScriptedDocumentationPackageBuilder(test,checker,"outcome"))));}
    [Theory]
    [InlineData("path")][InlineData("bytes")][InlineData("date")][InlineData("attributes")][InlineData("order")][InlineData("missing")][InlineData("extra")][InlineData("checksums")][InlineData("checksum-path")][InlineData("checksum-media")][InlineData("embedded-checksum")][InlineData("archive-path")][InlineData("archive-media")][InlineData("policy")][InlineData("policy-files")]
    public void EachPackagePropertyRejectsWrongResponse(string mutation){var test=DocumentationPackageCases.Original();var checker=new RecordingDocumentationPackageChecker(false);Assert.NotNull(Record.Exception(()=>DocumentationPackageCases.AssertContract(test,checker,new ScriptedDocumentationPackageBuilder(test,checker,mutation))));}
}
