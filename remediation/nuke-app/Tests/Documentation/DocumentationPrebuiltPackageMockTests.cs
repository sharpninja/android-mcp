using System.IO.Compression;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using AndroidMcp.BuildTool.Publication;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public sealed record DocumentationPrebuiltPackageCase(string Id,DocumentationPackageInput Input,
    DocumentationManifest Manifest,string? Error)
{public override string ToString()=>Id;}
public sealed class RecordingPrebuiltManifestAdmission(DocumentationPrebuiltPackageCase test):IDocumentationManifestBuilder
{
    public int BuildCalls {get;private set;}
    public int VerifyCalls {get;private set;}
    public DocumentationManifest Build(IReadOnlyList<Artifact> artifacts){BuildCalls++;throw new InvalidOperationException("The selected Manifest stage already built the manifest exactly once");}
    public void Verify(IReadOnlyList<Artifact> artifacts,DocumentationManifest manifest)
    {
        VerifyCalls++;Assert.Same(test.Input.Artifacts,artifacts);Assert.Same(test.Manifest,manifest);
        if(test.Error is not null)throw new ArchitectureAssetException(test.Error,"fixture manifest admission");
    }
}
public static class DocumentationPrebuiltPackageCases
{
    public static IEnumerable<object[]> Cases=>DocumentationManifestCases.VerifyCases.Select(row=>
    {
        var manifest=(DocumentationManifestCase)row[0];
        return new object[]{new DocumentationPrebuiltPackageCase("PACKAGE-PREBUILT-"+manifest.Id,
            new(manifest.Inputs,manifest.Inputs.Select(a=>a.RelativePath).ToArray(),new byte[]{1,2,3}),manifest.Expected,manifest.Error)};
    });
    public static void AssertContract(DocumentationPrebuiltPackageCase test,IDocumentationManifestPackageBuilder provider,
        RecordingPrebuiltManifestAdmission admission,RecordingDocumentationPackageChecker checker)
    {
        var before=test.Input.Artifacts.Select(a=>a.Bytes.ToArray()).ToArray();var policy=test.Input.Policy.ToArray();var checksum=test.Manifest.Checksums.Bytes.ToArray();
        if(test.Error is not null)
        {Assert.Equal(test.Error,Assert.Throws<ArchitectureAssetException>(()=>provider.Build(test.Input,test.Manifest)).Code);Assert.Empty(checker.Calls);}
        else
        {
            var output=provider.Build(test.Input,test.Manifest);Assert.Same(test.Manifest.Checksums,output.Checksums);
            Assert.Equal("package/android-mcp-design-source.zip",output.Archive.RelativePath);Assert.Equal("application/zip",output.Archive.MediaType);
            var expected=test.Input.Artifacts.Append(test.Manifest.Checksums).OrderBy(a=>a.RelativePath,StringComparer.Ordinal).ToArray();
            using var stream=new MemoryStream(output.Archive.Bytes.ToArray(),false);using var archive=new ZipArchive(stream,ZipArchiveMode.Read);
            Assert.Equal(expected.Select(a=>a.RelativePath),archive.Entries.Select(e=>e.FullName));
            foreach(var artifact in expected)
            {
                var entry=archive.GetEntry(artifact.RelativePath)!;using var body=entry.Open();using var bytes=new MemoryStream();body.CopyTo(bytes);
                Assert.Equal(artifact.Bytes.ToArray(),bytes.ToArray());Assert.Equal(artifact.Bytes.Length,entry.Length);
                Assert.Equal(new DateTime(1980,1,1),entry.LastWriteTime.DateTime);Assert.Equal(0,entry.ExternalAttributes);
            }
            var call=Assert.Single(checker.Calls);Assert.Equal(policy,call.Policy.ToArray());Assert.Equal(test.Input.Artifacts.Select(a=>new PublicationFile(a.RelativePath,a.Bytes.Length)),call.Files);
        }
        Assert.Equal(0,admission.BuildCalls);Assert.Equal(1,admission.VerifyCalls);Assert.Equal(policy,test.Input.Policy.ToArray());Assert.Equal(checksum,test.Manifest.Checksums.Bytes.ToArray());
        for(var index=0;index<before.Length;index++)Assert.Equal(before[index],test.Input.Artifacts[index].Bytes.ToArray());
    }
}
public sealed class ScriptedPrebuiltDocumentationPackageBuilder(DocumentationPrebuiltPackageCase test,
    RecordingPrebuiltManifestAdmission admission,RecordingDocumentationPackageChecker checker,string mutation=""):IDocumentationManifestPackageBuilder
{
    public DocumentationPackage Build(DocumentationPackageInput input,DocumentationManifest manifest)
    {
        Assert.Same(test.Input,input);Assert.Same(test.Manifest,manifest);
        if(mutation=="rebuild")admission.Build(input.Artifacts);
        if(mutation!="skip-admission")admission.Verify(input.Artifacts,manifest);
        checker.Check(input.Policy,input.Artifacts.Select(a=>new PublicationFile(a.RelativePath,a.Bytes.Length)).ToArray());
        var checksum=mutation=="replace-manifest"?manifest.Checksums with{Bytes=new byte[]{0}}:manifest.Checksums;
        var archive=DocumentationPackageCases.FixtureArchive(input.Artifacts.Append(checksum).ToArray(),mutation);
        return new(archive,checksum);
    }
    public DocumentationPackage Build(DocumentationPackageInput input)=>throw new InvalidOperationException("Legacy package Build retains its unchanged assertion suite");
}
[Trait("Gate","Mock")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class DocumentationPrebuiltPackageMockTests
{
    [Theory,MemberData(nameof(DocumentationPrebuiltPackageCases.Cases),MemberType=typeof(DocumentationPrebuiltPackageCases))]
    public void CorrectPrebuiltManifestPackageMocksPass(DocumentationPrebuiltPackageCase test)
    {var admission=new RecordingPrebuiltManifestAdmission(test);var checker=new RecordingDocumentationPackageChecker(false);DocumentationPrebuiltPackageCases.AssertContract(test,new ScriptedPrebuiltDocumentationPackageBuilder(test,admission,checker),admission,checker);}
    [Theory,MemberData(nameof(DocumentationPrebuiltPackageCases.Cases),MemberType=typeof(DocumentationPrebuiltPackageCases))]
    public void EveryManifestAdmissionBypassIsRejected(DocumentationPrebuiltPackageCase test)
    {var admission=new RecordingPrebuiltManifestAdmission(test);var checker=new RecordingDocumentationPackageChecker(false);Assert.NotNull(Record.Exception(()=>DocumentationPrebuiltPackageCases.AssertContract(test,new ScriptedPrebuiltDocumentationPackageBuilder(test,admission,checker,"skip-admission"),admission,checker)));}
    [Theory]
    [InlineData("rebuild")][InlineData("replace-manifest")][InlineData("missing")][InlineData("extra")][InlineData("bytes")][InlineData("date")][InlineData("attributes")][InlineData("order")]
    public void EveryRebuildAndArchiveMutationIsRejected(string mutation)
    {var test=(DocumentationPrebuiltPackageCase)DocumentationPrebuiltPackageCases.Cases.First()[0];var admission=new RecordingPrebuiltManifestAdmission(test);var checker=new RecordingDocumentationPackageChecker(false);Assert.NotNull(Record.Exception(()=>DocumentationPrebuiltPackageCases.AssertContract(test,new ScriptedPrebuiltDocumentationPackageBuilder(test,admission,checker,mutation),admission,checker)));}
}
