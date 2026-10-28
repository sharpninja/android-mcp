using AndroidMcp.BuildTool.Publication;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Publication;

public sealed record WorkspaceReadCase(string Id,string Path,string? Error=null,bool Policy=false)
{public override string ToString()=>Id;}
public sealed record WorkspaceReadScope(IPublicationWorkspaceReader Reader,string Root,Func<string> Fingerprint,IDisposable? Cleanup=null);

public static class PublicationWorkspaceCases
{
    public static IEnumerable<object[]> Cases=>new WorkspaceReadCase[]
    {
        new("POLICY","deliverables.json",Policy:true),new("POLICY-MISSING","missing.json","PUBLICATION_POLICY",true),new("POLICY-LINK","policy-link.json","PUBLICATION_POLICY",true),
        new("REGULAR","a.md"),new("ZERO","empty.md"),new("MISSING","missing.md"),new("DIRECTORY","dir"),
        new("LEAF-LINK","linked.md","PUBLICATION_PATH"),new("DANGLING-LINK","dangling.md","PUBLICATION_PATH"),new("ANCESTOR-LINK","alias/a.md","PUBLICATION_PATH"),
        new("UNSAFE","../outside.md","PUBLICATION_PATH")
    }.Select(c=>new object[]{c});
    public static PublicationFile Expected(WorkspaceReadCase c)=>new(c.Path,c.Id=="REGULAR"?3:0,c.Id switch{"MISSING"=>PublicationFileKind.Missing,"DIRECTORY"=>PublicationFileKind.Directory,_=>PublicationFileKind.Regular});
    public static async Task AssertCase(WorkspaceReadCase c,Func<WorkspaceReadCase,WorkspaceReadScope> factory)
    {
        var scope=factory(c);
        try
        {
            var before=scope.Fingerprint();ReadOnlyMemory<byte> policy=default;IReadOnlyList<PublicationFile>? metadata=null;
            var error=await Record.ExceptionAsync(async()=>
            {if(c.Policy)policy=await scope.Reader.ReadPolicyAsync(scope.Root,c.Path);else metadata=await scope.Reader.ObserveAsync(scope.Root,[c.Path]);});
            if(c.Error is not null)Assert.Equal(c.Error,Assert.IsType<PublicationGitCheckException>(error).Code);
            else{Assert.Null(error);if(c.Policy)Assert.Equal(PublicationGitCheckCases.Policy,policy.ToArray());else Assert.Equal(Expected(c),Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<PublicationFile>>(metadata)));}
            Assert.Equal(before,scope.Fingerprint());
        }
        finally{scope.Cleanup?.Dispose();}
    }
    public static WorkspaceReadScope Mock(WorkspaceReadCase c,string? mutation=null)
    {
        var state="original fixture retained";
        var reader=new ScriptedPublicationWorkspace(c,mutation,()=>state="changed fixture");
        return new(reader,Path.Combine(Path.GetTempPath(),"publication metadata fixture"),()=>state);
    }
}
public sealed class ScriptedPublicationWorkspace(WorkspaceReadCase c,string? mutation,Action change):IPublicationWorkspaceReader
{
    private void Before(){if(mutation=="inputs")change();if(c.Error is not null)throw new PublicationGitCheckException(mutation=="error"?"WRONG":c.Error,"Fixture boundary failure");}
    public Task<ReadOnlyMemory<byte>> ReadPolicyAsync(string root,string path,CancellationToken cancellationToken=default)
    {Before();return Task.FromResult<ReadOnlyMemory<byte>>(mutation=="bytes"?new byte[]{1}:PublicationGitCheckCases.Policy);}
    public Task<IReadOnlyList<PublicationFile>> ObserveAsync(string root,IReadOnlyList<string> paths,CancellationToken cancellationToken=default)
    {
        Before();var expected=PublicationWorkspaceCases.Expected(c);
        var observed=mutation switch{"path"=>expected with{Path="wrong"},"kind"=>expected with{Kind=(PublicationFileKind)99},"size"=>expected with{Length=99},_=>expected};
        return Task.FromResult<IReadOnlyList<PublicationFile>>(mutation=="omit"?[]:[observed]);
    }
}
[Trait("Gate","Mock")]
[Trait("Requirement","TEST-BUILD-007")]
public sealed class PublicationWorkspaceMockTests
{
    [Theory,MemberData(nameof(PublicationWorkspaceCases.Cases),MemberType=typeof(PublicationWorkspaceCases))]
    public Task CorrectReadOnlyWorkspaceMocksPass(WorkspaceReadCase c)=>PublicationWorkspaceCases.AssertCase(c,x=>PublicationWorkspaceCases.Mock(x));
    [Theory,MemberData(nameof(PublicationWorkspaceCases.Cases),MemberType=typeof(PublicationWorkspaceCases))]
    public async Task IndividualWrongMetadataPolicyErrorsOrMutationAreRejected(WorkspaceReadCase c)
    {
        var mutations=c.Error is not null?new[]{"error","inputs"}:c.Policy?new[]{"bytes","inputs"}:new[]{"path","kind","size","omit","inputs"};
        foreach(var mutation in mutations)Assert.NotNull(await Record.ExceptionAsync(()=>PublicationWorkspaceCases.AssertCase(c,x=>PublicationWorkspaceCases.Mock(x,mutation))));
    }
}
