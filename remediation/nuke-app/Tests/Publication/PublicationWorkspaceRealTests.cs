using System.Text;
using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Publication;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Publication;

[Trait("Requirement","TEST-BUILD-007")]
public sealed class PublicationWorkspaceRealTests
{
    [Theory,MemberData(nameof(PublicationWorkspaceCases.Cases),MemberType=typeof(PublicationWorkspaceCases))]
    public Task SameWorkspaceCasesBindNativeReader(WorkspaceReadCase c)=>
        PublicationWorkspaceCases.AssertCase(c,PublicationWorkspaceSandbox.Create);
}

// Test-owned miniature files only. Production has no fixture/write operation.
public sealed class PublicationWorkspaceSandbox:IDisposable
{
    private readonly string directory=Path.Combine(Path.GetTempPath(),"publication-reader-"+Guid.NewGuid().ToString("N"));
    private string Root=>Path.Combine(directory,"checkout");
    private PublicationWorkspaceSandbox(WorkspaceReadCase c)
    {
        Directory.CreateDirectory(Root);Directory.CreateDirectory(Path.Combine(Root,"dir"));
        File.WriteAllBytes(Path.Combine(Root,"deliverables.json"),PublicationGitCheckCases.Policy);
        File.WriteAllBytes(Path.Combine(Root,"a.md"),Encoding.UTF8.GetBytes("abc"));
        File.WriteAllBytes(Path.Combine(Root,"empty.md"),[]);
        File.WriteAllBytes(Path.Combine(directory,"outside.md"),Encoding.UTF8.GetBytes("retained outside fixture"));
        File.WriteAllBytes(Path.Combine(directory,"outside-policy.json"),PublicationGitCheckCases.Policy);
        Directory.CreateDirectory(Path.Combine(directory,"ancestor"));
        File.WriteAllBytes(Path.Combine(directory,"ancestor","a.md"),Encoding.UTF8.GetBytes("retained ancestor fixture"));
        switch(c.Id)
        {
            case "LEAF-LINK":File.CreateSymbolicLink(Path.Combine(Root,"linked.md"),Path.Combine(directory,"outside.md"));break;
            case "DANGLING-LINK":File.CreateSymbolicLink(Path.Combine(Root,"dangling.md"),Path.Combine(directory,"absent.md"));break;
            case "ANCESTOR-LINK":Directory.CreateSymbolicLink(Path.Combine(Root,"alias"),Path.Combine(directory,"ancestor"));break;
            case "POLICY-LINK":File.CreateSymbolicLink(Path.Combine(Root,"policy-link.json"),Path.Combine(directory,"outside-policy.json"));break;
        }
    }
    public static WorkspaceReadScope Create(WorkspaceReadCase c)
    {
        var fixture=new PublicationWorkspaceSandbox(c);
        return new(new PublicationWorkspaceReader(),fixture.Root,fixture.Fingerprint,fixture);
    }
    private string Fingerprint()
    {
        var entries=new List<(string Path,string Kind,string Value)>();
        void Visit(string path)
        {
            foreach(var item in new DirectoryInfo(path).EnumerateFileSystemInfos().OrderBy(x=>x.Name,StringComparer.Ordinal))
            {
                var relative=Path.GetRelativePath(directory,item.FullName);
                if((item.Attributes&FileAttributes.ReparsePoint)!=0)entries.Add((relative,"link",item.LinkTarget??""));
                else if((item.Attributes&FileAttributes.Directory)!=0){entries.Add((relative,"directory",""));Visit(item.FullName);}
                else entries.Add((relative,"file",ContentHash.Sha256(File.ReadAllBytes(item.FullName))));
            }
        }
        Visit(directory);
        return ContentHash.Sha256(JsonSerializer.SerializeToUtf8Bytes(entries.Select(e=>new{path=e.Path,kind=e.Kind,value=e.Value})));
    }
    public void Dispose(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
}
