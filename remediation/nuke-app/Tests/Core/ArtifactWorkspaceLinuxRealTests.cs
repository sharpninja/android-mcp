using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using Microsoft.Win32.SafeHandles;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

public sealed class ArtifactWorkspaceLinuxCaseProvider : IArtifactWorkspaceCaseProvider
{
    public async Task<JsonObject> ExecuteAsync(string scenario)
    {
        if(!OperatingSystem.IsLinux())throw new PlatformNotSupportedException("This retained fixture executes only on Linux.");
        var root=Path.Combine(Path.GetTempPath(),"artifact-workspace-"+scenario+"-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        var tree=scenario.StartsWith("tree-",StringComparison.Ordinal);var directory=scenario is "add-directory" or "pin-directory";
        var payload=new byte[]{0,1,128,255};var file=Path.Combine(root,"tool.bin");File.WriteAllBytes(file,payload);
        var target=directory?Path.Combine(root,"bin"):file;if(directory)Directory.CreateDirectory(target);
        var initial=(UnixFileMode)(directory?448:scenario=="set-file"?384:420);File.SetUnixFileMode(target,initial);
        var realKernel=new NativeArtifactKernel();var realIdentity=new NativeArtifactIdentityReader(new NativeArtifactCalls(realKernel));
        NativeArtifactObservation Inspect(string path){using var handle=realKernel.OpenLinux(path,0xa0800);return realIdentity.Observe(handle);}
        var paths=new Dictionary<string,NativeArtifactObservation>(StringComparer.Ordinal);
        if(tree)
        {
            Directory.CreateDirectory(Path.Combine(root,".git"));File.WriteAllText(Path.Combine(root,".git/private.md"),"retained private fixture; never read by adapter");
            Directory.CreateDirectory(Path.Combine(root,"sub/nested"));File.WriteAllBytes(Path.Combine(root,"sub/tool.txt"),payload);
            File.WriteAllText(Path.Combine(root,"sub/nested/deep.md"),"deep");File.WriteAllText(Path.Combine(root,"untracked.md"),"new");File.WriteAllText(Path.Combine(root,"private.txt"),"opaque fixture");
            File.CreateSymbolicLink(Path.Combine(root,"link"),Path.Combine(root,"sub"));
            // The permission-only fixture is outside the specified tree cohort.
            // A distinct selected scope keeps all originals without deleting it.
            var treeRoot=Path.Combine(root,"selected");Directory.CreateDirectory(treeRoot);
            foreach(var name in new[]{".git","sub"})CopyDirectory(Path.Combine(root,name),Path.Combine(treeRoot,name));
            foreach(var name in new[]{"untracked.md","private.txt"})File.Copy(Path.Combine(root,name),Path.Combine(treeRoot,name));
            File.CreateSymbolicLink(Path.Combine(treeRoot,"link"),Path.Combine(treeRoot,"sub"));root=treeRoot;
            foreach(var relative in new[]{".git","private.txt","sub","sub/nested","sub/nested/deep.md","sub/tool.txt","untracked.md"})paths[relative]=Inspect(Path.Combine(root,relative));
        }
        using var cancellation=new CancellationTokenSource();var trace=new List<string>();
        var kernel=new RecordingKernel(realKernel,trace,!tree,scenario=="unsupported");
        var identities=new RecordingIdentity(realIdentity,kernel,trace,!tree,scenario,root);
        var calls=new RecordingCalls(root,new NativeArtifactWorkspaceCalls(),trace,kernel,cancellation,scenario);
        var workspace=new NativeArtifactWorkspace(root,kernel,identities,calls);
        if(tree)
        {
            IReadOnlyList<ArtifactTreeEntry> entries=[];Exception? failure=null;
            try{entries=await workspace.EnumerateAsync(scenario=="tree-immediate"?"sub":null,scenario!="tree-immediate");}catch(Exception error){failure=error;}
            var rows=new JsonArray(entries.Select(entry=>
            {
                var hasIdentity=entry.Kind==NativeArtifactKind.Other?entry.Native is null:paths.TryGetValue(entry.RelativePath,out var expected)&&entry.Native==expected;
                return (JsonNode)new JsonObject{["path"]=entry.RelativePath,["kind"]=entry.Kind.ToString(),["identity_matches"]=hasIdentity,
                    ["length_matches"]=entry.Kind==NativeArtifactKind.Other?entry.Length==0:entry.Native is{} identity&&entry.Length==identity.Length};
            }).ToArray());
            return new(){["error"]=failure?.GetType().Name,["entries"]=rows,["set_calls"]=calls.Sets,["hash_calls"]=calls.Hashes,["all_handles_closed"]=kernel.Handles.All(handle=>handle.IsClosed),
                ["original_retained"]=File.Exists(Path.Combine(root,".git/private.md"))&&File.Exists(Path.Combine(root,"untracked.md"))&&new FileInfo(Path.Combine(root,"link")).LinkTarget is not null,["trace"]=new JsonArray(trace.Select(value=>(JsonNode?)JsonValue.Create(value)).ToArray())};
        }
        var original=Inspect(target);var relativeTarget=Path.GetRelativePath(root,target).Replace(Path.DirectorySeparatorChar,'/');
        var expected=scenario=="stale-open"?original with{FileIdLow=original.FileIdLow+1}:original;
        StoredArtifact? pin=scenario is "pin-file" or "pin-bad-hash" or "pin-directory"?new(relativeTarget,scenario=="pin-bad-hash"?new string('0',64):ContentHash.Sha256(payload),original.Length,"application/octet-stream"):null;
        var operation=scenario=="invalid-operation"?(ArtifactPermissionOperation)99:scenario=="set-file"?ArtifactPermissionOperation.SetMode0755:ArtifactPermissionOperation.AddExecuteBits;
        ArtifactPermissionResult? result=null;Exception? problem=null;
        if(scenario=="cancelled")cancellation.Cancel();
        try{result=await workspace.ApplyAsync(new(new(relativeTarget,expected),operation,pin),cancellation.Token);}catch(Exception error){problem=error;}
        return new()
        {
            ["error"]=problem is PinnedArtifactException pinned?pinned.Code:problem?.GetType().Name,
            ["previous_mode"]=(int)(result?.PreviousMode??initial),["applied_mode"]=(int)File.GetUnixFileMode(target),["set_calls"]=calls.Sets,["hash_calls"]=calls.Hashes,
            ["same_owned_handle"]=calls.SameHandle,["all_handles_closed"]=kernel.Handles.All(handle=>handle.IsClosed),["identity_matches"]=Inspect(target)==original&&(result is null||result.Artifact==new ArtifactReadSnapshot(relativeTarget,original)),
            ["original_retained"]=File.Exists(file)&&(directory?Directory.Exists(target):File.Exists(target)),["content_unchanged"]=payload.SequenceEqual(File.ReadAllBytes(file)),
            ["trace"]=new JsonArray(trace.Select(value=>(JsonNode?)JsonValue.Create(value)).ToArray())
        };
    }
    private static void CopyDirectory(string source,string destination)
    {
        Directory.CreateDirectory(destination);
        foreach(var entry in Directory.EnumerateFileSystemEntries(source))
        {var target=Path.Combine(destination,Path.GetFileName(entry));if(Directory.Exists(entry))CopyDirectory(entry,target);else File.Copy(entry,target);}
    }
    private sealed class RecordingKernel(INativeArtifactKernel inner,List<string> trace,bool permission,bool unsupported):INativeArtifactKernel
    {
        public List<SafeFileHandle> Handles{get;}=[];public Dictionary<SafeFileHandle,string> Paths{get;}=[];
        public NativeArtifactAbi? Abi=>unsupported?NativeArtifactAbi.Windows:inner.Abi;
        public SafeFileHandle OpenLinux(string path,int flags)
        {Assert.Equal(0xa0800,flags);if(permission)trace.Add("open");var handle=inner.OpenLinux(path,flags);Handles.Add(handle);Paths[handle]=path;return handle;}
        public SafeFileHandle OpenWindows(WindowsArtifactOpenRequest request)=>throw new InvalidOperationException("No Windows execution is allowed by this fixture.");
        public LinuxStatxIdentity Statx(SafeFileHandle handle,string path,int flags,uint mask)=>inner.Statx(handle,path,flags,mask);
        public WindowsFileIdentity ReadWindowsIdentity(SafeFileHandle handle)=>throw new InvalidOperationException("No Windows identity execution.");
    }
    private sealed class RecordingIdentity(INativeArtifactIdentityReader inner,RecordingKernel kernel,List<string> trace,bool permission,string scenario,string root):INativeArtifactIdentityReader
    {
        private int observes;
        public NativeArtifactObservation Observe(SafeFileHandle handle)
        {
            if(permission)trace.Add("observe");observes++;var value=inner.Observe(handle);
            if(scenario=="changed-before-write"&&observes==2||scenario=="tree-changed"&&kernel.Paths[handle]==root&&kernel.Paths.Count(item=>item.Value==root)>1)
                return value with{FileIdLow=value.FileIdLow+1};
            return value;
        }
    }
    private sealed class RecordingCalls(string root,IArtifactWorkspaceCalls inner,List<string> trace,RecordingKernel kernel,CancellationTokenSource cancellation,string scenario):IArtifactWorkspaceCalls
    {
        public int Sets{get;private set;}public int Hashes{get;private set;}public bool SameHandle{get;private set;}=true;
        public IReadOnlyList<string> Enumerate(string path){var relative=Path.GetRelativePath(root,path).Replace(Path.DirectorySeparatorChar,'/');trace.Add("enumerate:"+(relative=="."?"":relative));return inner.Enumerate(path);}
        public FileAttributes Attributes(string path)=>inner.Attributes(path);
        private void Borrow(SafeFileHandle handle){SameHandle&=kernel.Handles.Count==1&&ReferenceEquals(handle,kernel.Handles[0])&&!handle.IsClosed;}
        public UnixFileMode GetMode(SafeFileHandle handle){Borrow(handle);trace.Add("get-mode");var result=inner.GetMode(handle);if(scenario=="cancel-before-write")cancellation.Cancel();return result;}
        public void SetMode(SafeFileHandle handle,UnixFileMode mode){Borrow(handle);trace.Add("set-mode");Sets++;inner.SetMode(handle,mode);}
        public string HashBorrowed(SafeFileHandle handle){Borrow(handle);trace.Add("hash");Hashes++;var result=inner.HashBorrowed(handle);SameHandle&=!handle.IsClosed;return result;}
    }
}
[Trait("Gate","Real"),Trait("Platform","Linux"),Trait("Requirement","TEST-BUILD-004"),Trait("Requirement","TEST-BUILD-007")]
public sealed class ArtifactWorkspaceLinuxRealTests
{
    [Theory,MemberData(nameof(ArtifactWorkspaceContractCases.Cases),MemberType=typeof(ArtifactWorkspaceContractCases))]
    public Task SameFiniteCasesUseActualRetainedLinuxFilesAndDescriptorModes(string id)=>ArtifactWorkspaceContractCases.AssertContract(ArtifactWorkspaceContractCases.Get(id),new ArtifactWorkspaceLinuxCaseProvider());
}
