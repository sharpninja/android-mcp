using System.Text;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using AndroidMcp.BuildTool.Documentation.Validation;
using AndroidMcp.BuildTool.Publication;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation.Validation;

public sealed class GitMetadataEnvironment:IArtifactTreeReader,IArtifactStore,IArtifactReferenceResolver
{
    public const string Checksum="remediation/design-c4/docs/checksums.sha256",Html="remediation/design-c4/docs/storyboards.html";
    public string Scenario{get;}
    public string Raw=>Scenario=="NESTED"?"nested/.git/info/exclude":Scenario=="DECODED"?".git/a%20b":Scenario=="ORDINARY"?"ordinary/file":Scenario=="REMOTE"?"https://example.test/.git/config":".git/config";
    public string Target=>Scenario=="NESTED"?"nested/.git/info/exclude":Scenario=="DECODED"?".git/a b":Scenario=="ORDINARY"?"ordinary/file":".git/config";
    public string Parent=>Scenario=="NESTED"?"nested/.git/info":".git";
    public bool Fallback=>Scenario is not("DEFAULT" or "SNAPSHOT" or "ORDINARY" or "REMOTE" or "CANCELLED");
    public bool Missing=>Scenario is "DEFAULT" or "MISSING" or "PARENT-MISSING" or "PARENT-DIRECTORY-MISSING" or "ORDINARY";
    public bool Scope=>Scenario is "LINK" or "NONREGULAR" or "NO-NATIVE" or "KIND-DRIFT" or "LENGTH-DRIFT" or "OFF-PARENT" or "DUPLICATE";
    public List<string> Reads{get;}=[];public List<(string?,bool)> Calls{get;}=[];public List<CancellationToken> Tokens{get;}=[];public List<ArtifactReferenceRequest> References{get;}=[];
    public CancellationTokenSource Cancellation{get;}=new();public IOException Changed{get;}=new("tree directory identity changed during enumeration");
    public IReadOnlyList<PublicationFile> Entries{get;}
    public GitMetadataEnvironment(string scenario)
    {
        Scenario=scenario;Entries=[new(Checksum,0),new("README.md",1),new(Html,1),new(".git",0,PublicationFileKind.Directory)];
        if(scenario=="SNAPSHOT")Entries=Entries.Append(new PublicationFile(Target,1)).ToArray();if(scenario=="CANCELLED")Cancellation.Cancel();
    }
    public Task<ReadOnlyMemory<byte>> ReadAsync(string path,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();Tokens.Add(token);Reads.Add(path);
        return Task.FromResult<ReadOnlyMemory<byte>>(path switch{Checksum=>Array.Empty<byte>(),"README.md"=>Encoding.UTF8.GetBytes("[target]("+Raw+")"),Html=>"<p>local</p>"u8.ToArray(),_=>throw new InvalidOperationException("No metadata target content may be read")});
    }
    public Task<StoredArtifact> WriteAsync(Artifact artifact,CancellationToken token=default)=>throw new InvalidOperationException("Checker is read-only");
    public ArtifactReferenceResolution Resolve(ArtifactReferenceRequest request)
    {References.Add(request);return new ArtifactReferenceResolver(value=>value).Resolve(request);}
    public Task<IReadOnlyList<ArtifactTreeEntry>> EnumerateAsync(string? relativeDirectory=null,bool recursive=true,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();Tokens.Add(token);Calls.Add((relativeDirectory,recursive));
        if(Scenario=="CHANGED")throw Changed;if(Scenario=="PARENT-MISSING")throw new FileNotFoundException("parent missing");if(Scenario=="PARENT-DIRECTORY-MISSING")throw new DirectoryNotFoundException("parent missing");
        if(Scenario=="MISSING")return Task.FromResult<IReadOnlyList<ArtifactTreeEntry>>([]);
        var kind=Scenario=="DIRECTORY"?NativeArtifactKind.Directory:Scenario is "LINK" or "NONREGULAR"?NativeArtifactKind.Other:NativeArtifactKind.RegularFile;
        var entry=new ArtifactTreeEntry(Target,kind,1,kind==NativeArtifactKind.Other?null:BootstrapFileEnvironment.Identity(kind,17,1));
        entry=Scenario switch{"NO-NATIVE"=>entry with{Native=null},"KIND-DRIFT"=>entry with{Native=BootstrapFileEnvironment.Identity(NativeArtifactKind.Directory,17,1)},"LENGTH-DRIFT"=>entry with{Length=2},"OFF-PARENT"=>entry with{RelativePath=Parent+"/other/target"},_=>entry};
        IReadOnlyList<ArtifactTreeEntry> result=Scenario=="DUPLICATE"?[entry,entry]:Scenario=="UNRELATED-LINK"?[entry,new(Parent+"/unrelated-link",NativeArtifactKind.Other,0,null)]:[entry];return Task.FromResult(result);
    }
}
public static class GitMetadataCases
{
    public static readonly string[] Names=["FILE","DIRECTORY","MISSING","PARENT-MISSING","PARENT-DIRECTORY-MISSING","LINK","NONREGULAR","CHANGED","NO-NATIVE","KIND-DRIFT","LENGTH-DRIFT","OFF-PARENT","DUPLICATE","DEFAULT","SNAPSHOT","ORDINARY","REMOTE","NESTED","DECODED","UNRELATED-LINK","CANCELLED"];
    public static IEnumerable<object[]> Cases=>Names.Select(value=>new object[]{value});
    public static async Task AssertContract(GitMetadataEnvironment e,IRepositoryDocumentationValidator provider)
    {
        RepositoryDocumentationCheckResult? result=null;var failure=await Record.ExceptionAsync(async()=>result=await provider.CheckAsync(e.Entries,e.Cancellation.Token));
        if(e.Scenario=="CHANGED")Assert.Same(e.Changed,failure);else if(e.Scenario=="CANCELLED")Assert.IsAssignableFrom<OperationCanceledException>(failure);
        else if(e.Scope)Assert.Equal("DOCS_REPOSITORY_REFERENCE_SCOPE",Assert.IsType<ArchitectureAssetException>(failure).Code);
        else {Assert.Null(failure);Assert.NotNull(result);Assert.Equal(0,result.ChecksumsChecked);Assert.Equal(e.Scenario=="REMOTE"?0:1,result.MarkdownLinksChecked);Assert.Equal(0,result.RenderedSvgCount);Assert.Equal(0,result.EditableUiCopies);Assert.Equal(e.Missing?["Missing link in README.md: "+e.Raw]:Array.Empty<string>(),result.Errors);Assert.Equal(e.Missing?1:0,result.ExitCode);}
        if(e.Scope||e.Scenario is "CHANGED" or "CANCELLED")Assert.Null(result);
        Assert.Equal(e.Scenario=="CANCELLED"?Array.Empty<string>():e.Scope||e.Scenario=="CHANGED"?new[]{GitMetadataEnvironment.Checksum,"README.md"}:new[]{GitMetadataEnvironment.Checksum,"README.md",GitMetadataEnvironment.Html},e.Reads);
        Assert.Equal(e.Fallback?new (string?,bool)[]{(e.Parent,false)}:Array.Empty<(string?,bool)>(),e.Calls);
        Assert.All(e.Tokens,token=>Assert.Equal(e.Cancellation.Token,token));
        Assert.Equal(e.Scenario=="CANCELLED"?Array.Empty<ArtifactReferenceRequest>():new[]{new ArtifactReferenceRequest("README.md",e.Raw,ArtifactReferenceProfile.RepositoryMarkdown)},e.References);
    }
}
public sealed class ScriptedGitMetadataValidator(GitMetadataEnvironment e,string mutation=""):IRepositoryDocumentationValidator
{
    public async Task<RepositoryDocumentationCheckResult> CheckAsync(IReadOnlyList<PublicationFile> entries,CancellationToken token=default)
    {
        if(mutation=="outcome")return new(99,99,99,99,["wrong"]);
        token.ThrowIfCancellationRequested();Assert.Same(e.Entries,entries);
        await e.ReadAsync(GitMetadataEnvironment.Checksum,token);await e.ReadAsync("README.md",token);
        e.Resolve(new("README.md",e.Raw,ArtifactReferenceProfile.RepositoryMarkdown));
        if(e.Fallback&&mutation!="skip-metadata"){
            try{await e.EnumerateAsync(mutation=="parent"?".git/wrong":e.Parent,mutation=="recursive",mutation=="token"?CancellationToken.None:token);}
            catch(Exception error)when(error is FileNotFoundException or DirectoryNotFoundException){ }
            if(mutation=="duplicate-call")await e.EnumerateAsync(e.Parent,false,token);
        }
        if(e.Scope)throw new ArchitectureAssetException("DOCS_REPOSITORY_REFERENCE_SCOPE","fixture scope");
        if(mutation=="content")await e.ReadAsync(e.Target,token);
        await e.ReadAsync(GitMetadataEnvironment.Html,token);
        var result=new RepositoryDocumentationCheckResult(0,e.Scenario=="REMOTE"?0:1,0,0,e.Missing?["Missing link in README.md: "+e.Raw]:[]);
        return mutation switch{"checksum"=>result with{ChecksumsChecked=1},"links"=>result with{MarkdownLinksChecked=99},"vectors"=>result with{RenderedSvgCount=1},"editable"=>result with{EditableUiCopies=1},"errors"=>result with{Errors=["wrong"]},_=>result};
    }
}
[Trait("Gate","Mock"),Trait("Requirement","FR-BUILD-005"),Trait("Requirement","TEST-BUILD-005")]
public sealed class RepositoryGitMetadataMockTests
{
    [Theory,MemberData(nameof(GitMetadataCases.Cases),MemberType=typeof(GitMetadataCases))]
    public Task CorrectOptionalMetadataBoundaryMocksPass(string scenario){var e=new GitMetadataEnvironment(scenario);return GitMetadataCases.AssertContract(e,new ScriptedGitMetadataValidator(e));}
    [Theory,MemberData(nameof(GitMetadataCases.Cases),MemberType=typeof(GitMetadataCases))]
    public async Task EveryWrongOutcomeIsDiscriminating(string scenario){var e=new GitMetadataEnvironment(scenario);Assert.NotNull(await Record.ExceptionAsync(()=>GitMetadataCases.AssertContract(e,new ScriptedGitMetadataValidator(e,"outcome"))));}
    [Theory,InlineData("parent"),InlineData("recursive"),InlineData("token"),InlineData("skip-metadata"),InlineData("duplicate-call"),InlineData("content"),InlineData("checksum"),InlineData("links"),InlineData("vectors"),InlineData("editable"),InlineData("errors")]
    public async Task EachMetadataCallContentAndResultFaultIsRejected(string mutation){var e=new GitMetadataEnvironment("FILE");Assert.NotNull(await Record.ExceptionAsync(()=>GitMetadataCases.AssertContract(e,new ScriptedGitMetadataValidator(e,mutation))));}
}
