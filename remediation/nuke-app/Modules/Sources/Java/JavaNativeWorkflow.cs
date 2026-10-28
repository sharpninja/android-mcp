using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;

namespace AndroidMcp.BuildTool.Sources.Java;

// Sources/NativeParser is one operation in the monolith. Network, archive,
// compiler, byte hashing, canonical serialization and storage use shared seams.
public sealed class JavaNativeWorkflow(IByteTransport transport,INativeArchiveExtractor archives,IProcessRunner processes,IClock clock) : IJavaNativeWorkflow
{
    public async Task<JavaNativeWorkflowResult> RunAsync(JavaNativeWorkflowRequest request,CancellationToken cancellationToken=default)
    {
        var output=Path.GetFullPath(request.OutputRoot);var cache=Path.GetFullPath(request.CacheRoot??Path.Combine(output,"native-sources"));
        var receipts=Path.GetFullPath(request.ReceiptRoot??Path.Combine(output,"receipts"));
        if(Directory.Exists(output)&&Directory.EnumerateFileSystemEntries(output).Any())throw new InvalidDataException("Native parser output root must be a fresh generation.");
        if(!OperatingSystem.IsLinux() || System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture!=System.Runtime.InteropServices.Architecture.X64)
            throw new PlatformNotSupportedException("Native parser workflow currently supports linux-x64 only.");
        if(!Path.IsPathFullyQualified(request.Compiler.ExecutablePath) || !File.Exists(request.Compiler.ExecutablePath) || string.IsNullOrWhiteSpace(request.Compiler.Version) ||
            ContentHash.Sha256(await File.ReadAllBytesAsync(request.Compiler.ExecutablePath,cancellationToken))!=request.Compiler.Sha256)
            throw new InvalidDataException("Compiler identity must name an existing content-pinned native compiler and version.");
        if(request.ProjectRoot is not null)
        {
            var pins=JsonNode.Parse(await File.ReadAllBytesAsync(Path.Combine(request.ProjectRoot,"native-source-pins.json"),cancellationToken))!;
            if(pins["runtime"]?["sourceCommit"]?.GetValue<string>()!=JavaNativeBuilder.RuntimeCommit || pins["runtime"]?["tagObject"]?.GetValue<string>()!=JavaNativeBuilder.RuntimeTagObject ||
                pins["grammar"]?["sourceCommit"]?.GetValue<string>()!=JavaNativeBuilder.GrammarCommit)throw new InvalidDataException("Native source pin file differs from the approved immutable versions.");
        }
        Directory.CreateDirectory(output);
        var versionRequest=new ProcessRequest(request.Compiler,["--version"],output,new Dictionary<string,string>(),Path.Combine(receipts,"compiler-version"));
        var versionResult=await processes.RunAsync(versionRequest,cancellationToken);
        if(versionResult.Cancelled)throw new OperationCanceledException(cancellationToken);
        if(versionResult.ExitCode!=0)throw new InvalidDataException("Native compiler version probe failed; full receipts retained.");
        var captures=new ProcessCaptureReader(receipts);var versionBytes=await captures.ReadAsync(versionResult,false,cancellationToken);
        var version=Encoding.UTF8.GetString(versionBytes.Span).Split('\n').Select(x=>x.TrimEnd('\r')).FirstOrDefault(x=>x.Length>0);
        if(version!=request.Compiler.Version)throw new InvalidDataException("Native compiler version differs from pinned ToolIdentity.");
        var sourceStore=new FileArtifactStore(cache,bytes=>ContentHash.Sha256(bytes.Span));var outputStore=new FileArtifactStore(output,bytes=>ContentHash.Sha256(bytes.Span));
        async Task<NativeArchiveEvidence> Acquire(string repository,string commit,string name)
        {
            var url=new Uri("https://codeload.github.com/tree-sitter/"+repository+"/tar.gz/"+commit);
            var response=await transport.SendAsync(new(url,commit),cancellationToken);
            await sourceStore.WriteAsync(new("archives/"+name+".tar.gz",response.Bytes,"application/gzip"),cancellationToken);
            return await archives.ExtractTarGZipAsync(response.Bytes,sourceStore,name,cancellationToken);
        }
        var runtime=await Acquire("tree-sitter",JavaNativeBuilder.RuntimeCommit,"runtime");var grammar=await Acquire("tree-sitter-java",JavaNativeBuilder.GrammarCommit,"java");
        var selected=runtime.Files.Concat(grammar.Files).Where(file=>Select(file.RelativePath)).OrderBy(x=>x.RelativePath,StringComparer.Ordinal).ToArray();
        var hashes=selected.ToDictionary(x=>x.RelativePath,x=>x.Sha256,StringComparer.Ordinal);
        var build=await new JavaNativeBuilder(processes,outputStore).BuildAsync(new(cache,hashes,request.Compiler,Path.Combine(output,"work","native"),Path.Combine(receipts,"compile"),"native/linux-x64/libandroidmcp_java_parser.so"),cancellationToken);
        var sourceLock=new JsonObject{
            ["schema"]="androidmcp-java-native-source-lock-v1",["created_at"]=clock.Now.ToString("o"),["workflow_request"]=JsonSerializer.SerializeToNode(request),["runtime_version"]="0.25.2",["runtime_tag_object"]=JavaNativeBuilder.RuntimeTagObject,["runtime_commit"]=JavaNativeBuilder.RuntimeCommit,
            ["grammar_version"]="0.23.5",["grammar_commit"]=JavaNativeBuilder.GrammarCommit,["runtime_archive_sha256"]=runtime.ArchiveSha256,["grammar_archive_sha256"]=grammar.ArchiveSha256,
            ["runtime_archive_metadata"]=JsonSerializer.SerializeToNode(runtime.Metadata??[]),["grammar_archive_metadata"]=JsonSerializer.SerializeToNode(grammar.Metadata??[]),
            ["rid"]="linux-x64",["compiler"]=JsonSerializer.SerializeToNode(request.Compiler),["compiler_version_receipt"]=JsonSerializer.SerializeToNode(versionResult),
            ["native_artifact"]=new JsonObject{["relative_path"]=build.Artifact.RelativePath,["sha256"]=build.Artifact.Sha256,["length"]=build.Artifact.Length,["language_abi"]=build.LanguageAbi},
            ["source_files"]=new JsonArray(selected.Select(x=>(JsonNode)new JsonObject{["path"]=x.RelativePath,["sha256"]=x.Sha256,["length"]=x.Length}).ToArray()),
            ["compilation"]=JsonSerializer.SerializeToNode(build.Compilation),["process"]=JsonSerializer.SerializeToNode(build.Process),["required_exports"]=SourceSyntax.Strings(JavaNativeParser.RequiredSymbols)};
        var manifest=await outputStore.WriteAsync(new("native-source-lock.json",new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(sourceLock)),"application/json"),cancellationToken);
        return new(manifest,build,sourceLock);
    }
    private static bool Select(string path)=>path is "runtime/LICENSE" or "java/LICENSE" or "java/src/parser.c" ||
        (path.StartsWith("runtime/lib/src/",StringComparison.Ordinal)||path.StartsWith("runtime/lib/include/",StringComparison.Ordinal)||path.StartsWith("java/src/tree_sitter/",StringComparison.Ordinal)||path.StartsWith("java/bindings/c/",StringComparison.Ordinal))&&Path.GetExtension(path) is ".c" or ".h";
}
