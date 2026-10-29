using Microsoft.Win32.SafeHandles;

namespace AndroidMcp.BuildTool.Core;

/// <summary>Only tree metadata and descriptor-bound executable modes. Existing
/// Core authorities own path admission, native open, identity mapping and hashing.</summary>
public sealed class NativeArtifactWorkspace : IArtifactTreeReader,IArtifactPermissionEditor
{
    private readonly string root;
    private readonly INativeArtifactKernel kernel;
    private readonly INativeArtifactIdentityReader identity;
    private readonly IArtifactWorkspaceCalls calls;
    public NativeArtifactWorkspace(string rootDirectory,INativeArtifactKernel? kernel=null,INativeArtifactIdentityReader? identity=null,IArtifactWorkspaceCalls? calls=null)
    {
        root=Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootDirectory));
        this.kernel=kernel??new NativeArtifactKernel();
        this.identity=identity??new NativeArtifactIdentityReader(new NativeArtifactCalls(this.kernel));
        this.calls=calls??new NativeArtifactWorkspaceCalls();
    }
    private void RequireLinux()
    {
        if(kernel.Abi is not(NativeArtifactAbi.LinuxX64 or NativeArtifactAbi.LinuxArm64))
            throw new PlatformNotSupportedException("Artifact workspace requires admitted Linux x64/arm64; no fallback is used.");
    }
    private string ResolveRoot()
    {
        var parent=Path.GetDirectoryName(root);var leaf=Path.GetFileName(root);
        if(parent is null||leaf.Length==0)throw new ArgumentException("Select a workspace directory below the filesystem root.");
        return ArtifactPaths.Resolve(parent,leaf);
    }
    private string Resolve(string? relative)=>relative is null?ResolveRoot():ArtifactPaths.Resolve(root,relative);
    private SafeFileHandle Open(string path)
    {
        var handle=kernel.OpenLinux(path,0xa0800);
        if(handle is null||handle.IsInvalid||handle.IsClosed){handle?.Dispose();throw new IOException("Native workspace open did not return an owned valid handle.");}
        return handle;
    }
    private NativeArtifactObservation Observe(SafeFileHandle handle)
    {
        var value=identity.Observe(handle);
        if(value.Platform!=NativeArtifactPlatform.Linux)throw new IOException("Workspace observation is not Linux.");
        return value;
    }
    public Task<IReadOnlyList<ArtifactTreeEntry>> EnumerateAsync(string? relativeDirectory=null,bool recursive=true,CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();RequireLinux();var selected=Resolve(relativeDirectory);
        var result=new List<ArtifactTreeEntry>();var seen=new HashSet<string>(StringComparer.Ordinal);
        Visit(selected);
        return Task.FromResult<IReadOnlyList<ArtifactTreeEntry>>(result.OrderBy(entry=>entry.RelativePath,Comparer<string>.Create(StrictJsonInspector.CompareScalars)).ToArray());

        void Visit(string directory)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relative=Path.GetRelativePath(root,directory).Replace(Path.DirectorySeparatorChar,'/');
            directory=relative=="."?ResolveRoot():ArtifactPaths.Resolve(root,relative);
            using var opened=Open(directory);var before=Observe(opened);
            if(before.Kind!=NativeArtifactKind.Directory)throw new IOException("Tree scope is not an existing directory.");
            foreach(var path in calls.Enumerate(directory).Order(StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var absolute=Path.GetFullPath(path);
                if(Path.GetDirectoryName(absolute)!=directory)throw new ArgumentException("Tree enumeration returned a nonchild path.");
                var child=Path.GetRelativePath(root,absolute).Replace(Path.DirectorySeparatorChar,'/');
                _=ArtifactPaths.Segments(child);
                if(!seen.Add(child))throw new IOException("Tree enumeration repeated an entry.");
                var attributes=calls.Attributes(absolute);
                if((attributes&FileAttributes.ReparsePoint)!=0){result.Add(new(child,NativeArtifactKind.Other,0,null));continue;}
                var admitted=ArtifactPaths.Resolve(root,child);
                NativeArtifactObservation observation;
                using(var handle=Open(admitted))observation=Observe(handle);
                var normal=observation.Kind is NativeArtifactKind.RegularFile or NativeArtifactKind.Directory;
                result.Add(new(child,observation.Kind,normal?observation.Length:0,normal?observation:null));
                if(recursive&&observation.Kind==NativeArtifactKind.Directory&&Path.GetFileName(admitted)!=".git")Visit(admitted);
            }
            cancellationToken.ThrowIfCancellationRequested();
            using var current=Open(relative=="."?ResolveRoot():ArtifactPaths.Resolve(root,relative));
            if(Observe(current)!=before||Observe(opened)!=before)throw new IOException("Tree directory identity changed during enumeration.");
        }
    }
    public Task<ArtifactPermissionResult> ApplyAsync(ArtifactPermissionRequest request,CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();RequireLinux();ArgumentNullException.ThrowIfNull(request);ArgumentNullException.ThrowIfNull(request.Expected);
        if(request.Operation is not(ArtifactPermissionOperation.AddExecuteBits or ArtifactPermissionOperation.SetMode0755))throw new ArgumentException("Unsupported executable permission operation.",nameof(request));
        var path=ArtifactPaths.Resolve(root,request.Expected.RelativePath);
        using var handle=Open(path);var before=Observe(handle);
        if(before!=request.Expected.Native||before.Kind is not(NativeArtifactKind.RegularFile or NativeArtifactKind.Directory))
            throw new IOException("Permission input identity or kind differs from the admitted request.");
        if(request.ExpectedArtifact is{} pin)
        {
            if(before.Kind!=NativeArtifactKind.RegularFile)throw new ArgumentException("Directory permission requests cannot carry byte pins.",nameof(request));
            if(pin.RelativePath!=request.Expected.RelativePath)throw new PinnedArtifactException("ARTIFACT_PIN_METADATA");
            if(pin.Length!=before.Length)throw new PinnedArtifactException("ARTIFACT_PIN_LENGTH");
            cancellationToken.ThrowIfCancellationRequested();
            if(calls.HashBorrowed(handle)!=pin.Sha256)throw new PinnedArtifactException("ARTIFACT_PIN_HASH");
        }
        var previous=calls.GetMode(handle);
        var applied=request.Operation==ArtifactPermissionOperation.AddExecuteBits?previous|UnixFileMode.UserExecute|UnixFileMode.GroupExecute|UnixFileMode.OtherExecute:(UnixFileMode)493;
        cancellationToken.ThrowIfCancellationRequested();
        if(Observe(handle)!=before)throw new IOException("Permission input identity changed before mode write.");
        cancellationToken.ThrowIfCancellationRequested();
        calls.SetMode(handle,applied);
        if(calls.GetMode(handle)!=applied||Observe(handle)!=before)throw new IOException("Permission mode or input identity changed after mode write.");
        return Task.FromResult(new ArtifactPermissionResult(new(request.Expected.RelativePath,before),previous,applied));
    }
}

/// <summary>Thin BCL calls only. Borrowing never transfers the kernel-owned
/// handle; ContentHash remains the sole hash authority.</summary>
public sealed class NativeArtifactWorkspaceCalls : IArtifactWorkspaceCalls
{
    public IReadOnlyList<string> Enumerate(string admittedDirectory)=>Directory.EnumerateFileSystemEntries(admittedDirectory).ToArray();
    public FileAttributes Attributes(string admittedPath)=>File.GetAttributes(admittedPath);
    public UnixFileMode GetMode(SafeFileHandle borrowedHandle)
    {
        if(OperatingSystem.IsWindows())throw new PlatformNotSupportedException("Unix descriptor modes are unsupported on Windows.");
        return File.GetUnixFileMode(borrowedHandle);
    }
    public void SetMode(SafeFileHandle borrowedHandle,UnixFileMode mode)
    {
        if(OperatingSystem.IsWindows())throw new PlatformNotSupportedException("Unix descriptor modes are unsupported on Windows.");
        File.SetUnixFileMode(borrowedHandle,mode);
    }
    public string HashBorrowed(SafeFileHandle borrowedHandle)
    {
        ArgumentNullException.ThrowIfNull(borrowedHandle);
        var retained=false;
        try
        {
            borrowedHandle.DangerousAddRef(ref retained);
            using var borrowed=new SafeFileHandle(borrowedHandle.DangerousGetHandle(),ownsHandle:false);
            using var stream=new FileStream(borrowed,FileAccess.Read,65536,isAsync:false);
            return ContentHash.Sha256(stream);
        }
        finally{if(retained)borrowedHandle.DangerousRelease();}
    }
}
