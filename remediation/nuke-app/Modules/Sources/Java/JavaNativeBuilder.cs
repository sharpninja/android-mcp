using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Sources.Java;

public sealed class JavaNativeBuilder(IProcessRunner processes,IArtifactStore artifacts) : IJavaNativeBuilder
{
    public const string RuntimeCommit="6e0618704ad758ba2ea5822faa80bcd36fbeba3d";
    public const string RuntimeTagObject="695d1530e59804279d9c739335b73b882d9f1caf";
    public const string GrammarCommit="94703d5a6bed02b98e438d7cad1136c01a60ba2c";
    public async Task<JavaNativeBuildResult> BuildAsync(JavaNativeBuildRequest request,CancellationToken cancellationToken=default)
    {
        if(!OperatingSystem.IsLinux() || System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture!=System.Runtime.InteropServices.Architecture.X64)
            throw new PlatformNotSupportedException("Native Java compiler adapter currently supports linux-x64 only.");
        var root=Path.GetFullPath(request.SourceRoot);
        foreach(var required in new[]{"runtime/lib/src/lib.c","runtime/lib/include/tree_sitter/api.h","runtime/LICENSE","java/src/parser.c","java/src/tree_sitter/parser.h","java/bindings/c/tree-sitter-java.h","java/LICENSE"})
            if(!request.SourceHashes.ContainsKey(required))throw new InvalidDataException("Missing native input pin: "+required);
        var selected=new HashSet<string>(StringComparer.Ordinal);
        foreach(var prefix in new[]{"runtime/lib/src","runtime/lib/include","java/src/tree_sitter","java/bindings/c"})
        {
            var directory=Path.Combine(root,prefix);
            if(!Directory.Exists(directory))throw new InvalidDataException("Missing native source directory: "+prefix);
            foreach(var file in Directory.EnumerateFiles(directory,"*",SearchOption.AllDirectories).Where(x=>Path.GetExtension(x) is ".c" or ".h"))selected.Add(Path.GetRelativePath(root,file).Replace('\\','/'));
        }
        selected.UnionWith(["runtime/LICENSE","java/LICENSE","java/src/parser.c"]);
        if(!selected.SetEquals(request.SourceHashes.Keys))throw new InvalidDataException("Native selected source set differs from its complete content-pin manifest.");
        foreach(var input in request.SourceHashes)
        {
            if(input.Key.Contains('\\') || input.Key.Split('/').Any(x=>x is "" or "." or "..") || Path.IsPathRooted(input.Key))throw new InvalidDataException("Unsafe native source path.");
            var path=Path.Combine(root,input.Key);
            var probe=path;
            while(probe.Length>=root.Length)
            {
                if((File.GetAttributes(probe)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Native inputs cannot traverse symbolic links.");
                if(probe==root)break;probe=Path.GetDirectoryName(probe)!;
            }
            if(ContentHash.Sha256(await File.ReadAllBytesAsync(path,cancellationToken))!=input.Value)throw new InvalidDataException("Native input content pin mismatch: "+input.Key);
        }
        var working=Path.GetFullPath(request.WorkingDirectory);
        if(Directory.Exists(working)&&Directory.EnumerateFileSystemEntries(working).Any())throw new InvalidDataException("Native build output directory must be new or empty.");
        Directory.CreateDirectory(working);var output=Path.Combine(working,"libandroidmcp_java_parser.so");
        string[] arguments=["-std=c11","-D_DEFAULT_SOURCE","-fPIC","-shared","-O2","-I"+Path.Combine(root,"runtime/lib/include"),"-I"+Path.Combine(root,"runtime/lib/src"),
            "-I"+Path.Combine(root,"java/src"),Path.Combine(root,"runtime/lib/src/lib.c"),Path.Combine(root,"java/src/parser.c"),"-o",output];
        var compilation=new ProcessRequest(request.Compiler,arguments,working,new Dictionary<string,string>(),request.ReceiptDirectory);
        var process=await processes.RunAsync(compilation,cancellationToken);
        if(process.Cancelled)throw new OperationCanceledException("Native Java compilation cancelled; receipts retained.",cancellationToken);
        if(process.ExitCode!=0 || !File.Exists(output))throw new InvalidDataException("Native Java compilation failed; complete compiler stdout/stderr retained at "+request.ReceiptDirectory);
        var bytes=await File.ReadAllBytesAsync(output,cancellationToken);var digest=ContentHash.Sha256(bytes);
        using var validate=new JavaNativeParser(new(output,digest));
        var stored=await artifacts.WriteAsync(new(request.ArtifactRelativePath,bytes,"application/x-sharedlib"),cancellationToken);
        return new(stored,compilation,process,RuntimeCommit,GrammarCommit,validate.LanguageVersion);
    }
}
