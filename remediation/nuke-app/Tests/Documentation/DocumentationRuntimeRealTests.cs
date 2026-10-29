using System.Text;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using AndroidMcp.BuildTool.Documentation.Validation;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Publication;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

[Trait("Gate","Real"),Trait("Requirement","FR-BUILD-005"),Trait("Requirement","TEST-BUILD-005")]
public sealed class DocumentationRuntimeRealTests
{
    [Theory,InlineData(false),InlineData(true)]
    public async Task SameOriginalModeAndFailureObservationsBindActualLinuxEffects(bool rejectProperties)
    {
        if(!OperatingSystem.IsLinux())throw new PlatformNotSupportedException("This original executable-mode proof requires the admitted Linux adapter");
        var root=Path.Combine(Path.GetTempPath(),"documentation-bootstrap-linux",Guid.NewGuid().ToString("N"));
        var cache=new FileArtifactStore(root,bytes=>ContentHash.Sha256(bytes.Span));
        var canonical=new FileArtifactStore(Path.Combine(DocumentationFixture.Current.Workspace,"remediation/design-c4"),bytes=>ContentHash.Sha256(bytes.Span));
        var wrapper=(await canonical.ReadAsync("gradlew")).ToArray();
        var properties=WrapperPropertiesCases.Expected;
        if(rejectProperties)properties=properties.Replace("networkTimeout=10000","networkTimeout=1",StringComparison.Ordinal);
        var files=new Dictionary<string,byte[]>(StringComparer.Ordinal){
            [".tools/gradle-8.14.3/bin/gradle"]="abc"u8.ToArray(),[".tools/gradle-8.14.3/bin/existing-extra"]="zz"u8.ToArray(),
            ["gradlew"]=wrapper,["unrelated"]="retained input"u8.ToArray(),[WrapperPropertiesCases.Path]=Encoding.UTF8.GetBytes(properties)};
        var pins=new Dictionary<string,StoredArtifact>(StringComparer.Ordinal);
        foreach(var file in files)pins[file.Key]=await cache.WriteAsync(new(file.Key,file.Value,"application/octet-stream"));
        Directory.CreateDirectory(ArtifactPaths.Resolve(root,".tools/gradle-8.14.3/bin/existing-directory"));
        var before=new Dictionary<string,UnixFileMode>(StringComparer.Ordinal){
            [".tools/gradle-8.14.3/bin/gradle"]=(UnixFileMode)416,[".tools/gradle-8.14.3/bin/existing-extra"]=(UnixFileMode)384,
            [".tools/gradle-8.14.3/bin/existing-directory"]=(UnixFileMode)448,["gradlew"]=(UnixFileMode)438,["unrelated"]=(UnixFileMode)438};
        // Fixture initialization only. Production effects below use the single
        // Source-owned descriptor-bound editor, never these setup path calls.
        foreach(var mode in before)File.SetUnixFileMode(ArtifactPaths.Resolve(root,mode.Key),mode.Value);
        var e=new BootstrapHookEnvironment(rejectProperties?"PROPERTY-REJECTION":"FULL");
        e.Original.Receipts[0]=e.Original.Receipts[0] with{Files=[pins[".tools/gradle-8.14.3/bin/gradle"]]};
        var native=new NativeArtifactWorkspace(root);
        var captured=new Artifact(WrapperPropertiesCases.Path,await cache.ReadAsync(WrapperPropertiesCases.Path),"application/octet-stream");
        var effects=new DocumentationBootstrapFileEffects(native,native,new DocumentationWrapperPropertiesValidator(),captured);
        IReadOnlyList<ToolArtifactReceipt>? result=null;string? error=null;
        try{result=await new DocumentationToolBootstrap(e.Tools,effects).AcquireAsync(e.Original.Profile,ToolArtifactAccess.Online,null);}
        catch(ArchitectureAssetException failure){error=failure.Code;}
        var modes=new Dictionary<string,UnixFileMode>(StringComparer.Ordinal);
        foreach(var path in before.Keys)modes.Add(path,File.GetUnixFileMode(ArtifactPaths.Resolve(root,path)));
        var unchanged=true;foreach(var file in files)unchanged&=(await cache.ReadAsync(file.Key)).Span.SequenceEqual(file.Value);
        BootstrapRuntimeCases.AssertObservation(rejectProperties,new(error,e.Requests.Count,result?.Count??0,unchanged,modes));
    }
    [Theory,InlineData("GOOD"),InlineData("MULTIPLE-ERRORS")]
    public async Task SameOriginalFullRootObservationsBindNativeTreeStoreAndChecker(string id)
    {
        if(!OperatingSystem.IsLinux())throw new PlatformNotSupportedException("This full-root caller proof requires the admitted Linux adapter");
        var test=new Validation.RepositoryDocumentationCase(id);var e=new Validation.RepositoryDocumentationEnvironment(test);
        var root=Path.Combine(Path.GetTempPath(),"documentation-repository-linux",Guid.NewGuid().ToString("N"));
        var store=new FileArtifactStore(root,bytes=>ContentHash.Sha256(bytes.Span));
        foreach(var directory in test.Entries.Where(entry=>entry.Kind==PublicationFileKind.Directory))Directory.CreateDirectory(ArtifactPaths.Resolve(root,directory.Path));
        foreach(var file in test.Files)await store.WriteAsync(new(file.Key,file.Value,"application/octet-stream"));
        var tree=new NativeArtifactWorkspace(root);
        var validator=new RepositoryDocumentationValidator(e.Store(store),e.Resolver,new ArtifactReceiptVerifier(new NativeContentHash()),new SourceHtmlDecoder());
        var result=await new RepositoryDocumentationCheck(tree,validator).CheckAsync();
        var unchanged=true;foreach(var file in test.Files)unchanged&=(await store.ReadAsync(file.Key)).Span.SequenceEqual(file.Value);
        RepositoryRuntimeCases.AssertObservation(id,new(result,unchanged,e.Reads));
    }
}
