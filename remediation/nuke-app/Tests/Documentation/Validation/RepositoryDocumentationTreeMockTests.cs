using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using AndroidMcp.BuildTool.Documentation.Validation;
using AndroidMcp.BuildTool.Publication;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation.Validation;

public sealed class RepositoryTreeEnvironment:IArtifactTreeReader,IRepositoryDocumentationValidator
{
    public string Scenario{get;}
    public IReadOnlyList<ArtifactTreeEntry> Entries{get;}
    public PublicationFile[] ExpectedEntries{get;}
    public RepositoryDocumentationCheckResult Expected{get;}=new(1,4,2,1,["independent checker observation"]);
    public List<(string?,bool)> TreeCalls{get;}=[];
    public List<CancellationToken> Tokens{get;}=[];
    public int CheckerCalls{get;private set;}
    public CancellationTokenSource Cancellation{get;}=new();
    public RepositoryTreeEnvironment(string scenario)
    {
        Scenario=scenario;
        var corpus=new RepositoryDocumentationCase("GOOD");
        ExpectedEntries=corpus.Entries.Where(entry=>!entry.Path.Contains("/.git/",StringComparison.Ordinal)).ToArray();
        Entries=ExpectedEntries.Select((entry,index)=>new ArtifactTreeEntry(entry.Path,entry.Kind==PublicationFileKind.Regular?NativeArtifactKind.RegularFile:NativeArtifactKind.Directory,entry.Length,
            BootstrapFileEnvironment.Identity(entry.Kind==PublicationFileKind.Regular?NativeArtifactKind.RegularFile:NativeArtifactKind.Directory,(ulong)index+1,entry.Length))).ToArray();
        if(scenario=="OTHER")Entries=Entries.Append(new("link",NativeArtifactKind.Other,0,null)).ToArray();
        if(scenario=="CANCELLED")Cancellation.Cancel();
    }
    public Task<IReadOnlyList<ArtifactTreeEntry>> EnumerateAsync(string? relativeDirectory=null,bool recursive=true,CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();Tokens.Add(cancellationToken);TreeCalls.Add((relativeDirectory,recursive));
        if(Scenario=="TREE-FAIL")throw new IOException("tree fixture");return Task.FromResult(Entries);
    }
    public Task<RepositoryDocumentationCheckResult> CheckAsync(IReadOnlyList<PublicationFile> entries,CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();Tokens.Add(cancellationToken);CheckerCalls++;Assert.Equal(ExpectedEntries,entries);
        if(Scenario=="CHECKER-FAIL")throw new IOException("checker fixture");return Task.FromResult(Expected);
    }
}
public static class RepositoryTreeCases
{
    public static IEnumerable<object[]> Cases=>new[]{"FULL","OTHER","TREE-FAIL","CHECKER-FAIL","CANCELLED"}.Select(id=>new object[]{id});
    public static async Task AssertContract(RepositoryTreeEnvironment e,IRepositoryDocumentationCheck provider)
    {
        RepositoryDocumentationCheckResult? actual=null;var error=await Record.ExceptionAsync(async()=>actual=await provider.CheckAsync(e.Cancellation.Token));
        if(e.Scenario=="FULL"){Assert.Null(error);Assert.Same(e.Expected,actual);}
        else if(e.Scenario=="CANCELLED")Assert.IsAssignableFrom<OperationCanceledException>(error);
        else if(e.Scenario=="OTHER")Assert.Equal("DOCS_REPOSITORY_REFERENCE_SCOPE",Assert.IsType<ArchitectureAssetException>(error).Code);
        else Assert.IsType<IOException>(error);
        if(e.Scenario!="FULL")Assert.Null(actual);
        Assert.Equal(e.Scenario=="CANCELLED"?[]:new (string?,bool)[]{(null,true)},e.TreeCalls);
        Assert.Equal(e.Scenario is "FULL" or "CHECKER-FAIL"?1:0,e.CheckerCalls);
        foreach(var token in e.Tokens)Assert.Equal(e.Cancellation.Token,token);
    }
}
public sealed class ScriptedRepositoryTreeCheck(RepositoryTreeEnvironment e,string mutation=""):IRepositoryDocumentationCheck
{
    public async Task<RepositoryDocumentationCheckResult> CheckAsync(CancellationToken token=default)
    {
        if(mutation=="cancel")return e.Expected;
        token.ThrowIfCancellationRequested();
        var entries=await e.EnumerateAsync(mutation=="scope"?"remediation/design-c4":null,mutation!="nonrecursive",mutation=="token"?CancellationToken.None:token);
        if(entries.Any(entry=>entry.Kind==NativeArtifactKind.Other))throw new ArchitectureAssetException("DOCS_REPOSITORY_REFERENCE_SCOPE","nonregular original scope");
        var selected=entries.Where(entry=>mutation switch{"drop-directory"=>entry.Kind!=NativeArtifactKind.Directory,"drop-untracked"=>entry.RelativePath!="nested/untracked.md","drop-git-metadata"=>!entry.RelativePath.EndsWith(".git",StringComparison.Ordinal),_=>true});
        var files=selected.Select(entry=>new PublicationFile(entry.RelativePath,entry.Length,entry.Kind==NativeArtifactKind.RegularFile?PublicationFileKind.Regular:PublicationFileKind.Directory)).ToArray();
        var result=await e.CheckAsync(files,token);return mutation=="result"?result with{Errors=[]}:result;
    }
}
[Trait("Gate","Mock"),Trait("Requirement","FR-BUILD-005"),Trait("Requirement","TEST-BUILD-005")]
public sealed class RepositoryDocumentationTreeMockTests
{
    [Theory,MemberData(nameof(RepositoryTreeCases.Cases),MemberType=typeof(RepositoryTreeCases))]
    public Task CorrectRootTreeScopeAndAdmissionMocksPass(string scenario)
    {var e=new RepositoryTreeEnvironment(scenario);return RepositoryTreeCases.AssertContract(e,new ScriptedRepositoryTreeCheck(e));}
    [Theory,InlineData("scope"),InlineData("nonrecursive"),InlineData("token"),InlineData("drop-directory"),InlineData("drop-untracked"),InlineData("drop-git-metadata"),InlineData("result")]
    public async Task EveryFullRepositoryScopeAndForwardingAssertionRejectsWrongMocks(string mutation)
    {var e=new RepositoryTreeEnvironment("FULL");Assert.NotNull(await Record.ExceptionAsync(()=>RepositoryTreeCases.AssertContract(e,new ScriptedRepositoryTreeCheck(e,mutation))));}
    [Fact]
    public async Task CancellationCannotReturnAnUnexecutedSuccess()
    {var e=new RepositoryTreeEnvironment("CANCELLED");Assert.NotNull(await Record.ExceptionAsync(()=>RepositoryTreeCases.AssertContract(e,new ScriptedRepositoryTreeCheck(e,"cancel"))));}
}
