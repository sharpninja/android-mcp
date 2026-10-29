using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using AndroidMcp.BuildTool.Documentation.Validation;
using AndroidMcp.BuildTool.Sources;
using Microsoft.Win32.SafeHandles;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation.Validation;

[Trait("Gate","Real"),Trait("Requirement","FR-BUILD-005"),Trait("Requirement","TEST-BUILD-005")]
public sealed class RepositoryGitMetadataRealTests
{
    [Theory,MemberData(nameof(GitMetadataCases.Cases),MemberType=typeof(GitMetadataCases))]
    public Task SameExactMetadataOutcomeAndShortCircuitCasesBindNativeValidator(string scenario)
    {
        var e=new GitMetadataEnvironment(scenario);var receipts=new ArtifactReceiptVerifier(new NativeContentHash());var html=new SourceHtmlDecoder();
        var validator=scenario=="DEFAULT"?new RepositoryDocumentationValidator(e,e,receipts,html):new RepositoryDocumentationValidator(e,e,receipts,html,e);
        return GitMetadataCases.AssertContract(e,validator);
    }
    [Theory,InlineData("FILE"),InlineData("DIRECTORY"),InlineData("MISSING"),InlineData("PARENT-MISSING"),InlineData("NESTED"),InlineData("DECODED"),InlineData("LINK"),InlineData("UNRELATED-LINK")]
    public async Task SameReferencedExistenceCasesBindOneActualNativeTreeWithoutGitContents(string scenario)
    {
        if(!OperatingSystem.IsLinux())throw new PlatformNotSupportedException("This metadata OS proof requires the admitted Linux workspace");
        var e=new GitMetadataEnvironment(scenario);var root=Path.Combine(Path.GetTempPath(),"documentation-git-metadata",Guid.NewGuid().ToString("N"));
        var store=new FileArtifactStore(root,value=>ContentHash.Sha256(value.Span));
        foreach(var path in new[]{GitMetadataEnvironment.Checksum,"README.md",GitMetadataEnvironment.Html})await store.WriteAsync(new(path,await e.ReadAsync(path,e.Cancellation.Token),"application/octet-stream"));
        e.Reads.Clear();e.Tokens.Clear();
        var parent=e.Parent;
        if(scenario!="PARENT-MISSING")Directory.CreateDirectory(ArtifactPaths.Resolve(root,".git"));
        if(scenario!="PARENT-MISSING")Directory.CreateDirectory(ArtifactPaths.Resolve(root,parent));
        if(scenario is "FILE" or "NESTED" or "DECODED" or "UNRELATED-LINK")await store.WriteAsync(new(e.Target,"must never be read"u8.ToArray(),"application/octet-stream"));
        if(scenario=="DIRECTORY")Directory.CreateDirectory(ArtifactPaths.Resolve(root,e.Target));
        if(scenario=="LINK")File.CreateSymbolicLink(ArtifactPaths.Resolve(root,e.Target),"../README.md");
        if(scenario=="UNRELATED-LINK")File.CreateSymbolicLink(ArtifactPaths.Resolve(root,parent+"/unrelated-link"),"../README.md");
        var tree=new NativeArtifactWorkspace(root);var reads=new List<string>();var guarded=new GuardedStore(store,reads);
        var html=new SourceHtmlDecoder();var validator=new RepositoryDocumentationValidator(guarded,new ArtifactReferenceResolver(html.Decode),new ArtifactReceiptVerifier(new NativeContentHash()),html,tree);
        var caller=new RepositoryDocumentationCheck(tree,validator);
        var failure=await Record.ExceptionAsync(async()=>{
            var result=await caller.CheckAsync();Assert.Equal(scenario is "MISSING" or "PARENT-MISSING"?1:0,result.ExitCode);Assert.Equal(1,result.MarkdownLinksChecked);
            if(scenario is "MISSING" or "PARENT-MISSING")Assert.Equal(new[]{"Missing link in README.md: "+e.Raw},result.Errors);else Assert.Empty(result.Errors);
        });
        if(scenario=="LINK")Assert.Equal("DOCS_REPOSITORY_REFERENCE_SCOPE",Assert.IsType<ArchitectureAssetException>(failure).Code);else Assert.Null(failure);
        Assert.DoesNotContain(reads,path=>path.Split('/').Contains(".git",StringComparer.Ordinal));
    }
    [Fact]
    public void OriginalFourArgumentConstructorIsRetained()=>Assert.NotNull(typeof(RepositoryDocumentationValidator).GetConstructor([typeof(IArtifactStore),typeof(IArtifactReferenceResolver),typeof(IArtifactReceiptVerifier),typeof(ISourceHtmlDecoder)]));
    [Fact]
    public async Task ActualMetadataDirectoryChangePropagatesBeforeLaterContentReads()
    {
        if(!OperatingSystem.IsLinux())throw new PlatformNotSupportedException("Directory identity probe requires Linux");
        var root=Path.Combine(Path.GetTempPath(),"documentation-git-changed",Guid.NewGuid().ToString("N"));var e=new GitMetadataEnvironment("FILE");
        var store=new FileArtifactStore(root,value=>ContentHash.Sha256(value.Span));
        foreach(var path in new[]{GitMetadataEnvironment.Checksum,"README.md",GitMetadataEnvironment.Html})await store.WriteAsync(new(path,await e.ReadAsync(path,e.Cancellation.Token),"application/octet-stream"));
        await store.WriteAsync(new(e.Target,"not read"u8.ToArray(),"application/octet-stream"));
        var calls=new DirectoryChangeCalls(ArtifactPaths.Resolve(root,".git"));var tree=new NativeArtifactWorkspace(root,calls:calls);var reads=new List<string>();var html=new SourceHtmlDecoder();
        var checker=new RepositoryDocumentationCheck(tree,new RepositoryDocumentationValidator(new GuardedStore(store,reads),new ArtifactReferenceResolver(html.Decode),new ArtifactReceiptVerifier(new NativeContentHash()),html,tree));
        var failure=await Record.ExceptionAsync(()=>checker.CheckAsync());Assert.Contains("identity changed",Assert.IsType<IOException>(failure).Message,StringComparison.Ordinal);
        Assert.Equal(1,calls.Changes);Assert.DoesNotContain(GitMetadataEnvironment.Html,reads);Assert.DoesNotContain(reads,path=>path.Split('/').Contains(".git",StringComparer.Ordinal));
    }
    private sealed class DirectoryChangeCalls(string selected):IArtifactWorkspaceCalls
    {
        private readonly NativeArtifactWorkspaceCalls inner=new();public int Changes{get;private set;}
        public IReadOnlyList<string> Enumerate(string admittedDirectory){var entries=inner.Enumerate(admittedDirectory);if(admittedDirectory==selected){Changes++;File.WriteAllBytes(Path.Combine(selected,"changed-metadata"),new byte[]{1});}return entries;}
        public FileAttributes Attributes(string path)=>inner.Attributes(path);
        public UnixFileMode GetMode(SafeFileHandle handle)=>throw new InvalidOperationException("Metadata query cannot read modes");
        public void SetMode(SafeFileHandle handle,UnixFileMode mode)=>throw new InvalidOperationException("Metadata query cannot write modes");
        public string HashBorrowed(SafeFileHandle handle)=>throw new InvalidOperationException("Metadata query cannot read content");
    }
    private sealed class GuardedStore(IArtifactStore inner,List<string> reads):IArtifactStore
    {
        public Task<ReadOnlyMemory<byte>> ReadAsync(string path,CancellationToken token=default){reads.Add(path);if(path.Split('/').Contains(".git",StringComparer.Ordinal))throw new InvalidOperationException("Git content read forbidden");return inner.ReadAsync(path,token);}
        public Task<StoredArtifact> WriteAsync(Artifact artifact,CancellationToken token=default)=>throw new InvalidOperationException("Checker never writes");
    }
}
