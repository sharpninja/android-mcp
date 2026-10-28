using System.Text.Json;

namespace AndroidMcp.BuildTool.Core;

/// <summary>One fixed native SDK metadata purpose over the shared process/capture/store engine.</summary>
public sealed class NativeTestGateResolvedReferenceQuery(IProcessRunner runner,IProcessCaptureReader captures,
    IArtifactStore store):ITestGateResolvedReferenceQuery
{
    public const string Target="ReferenceCopyLocalPathsOutputGroup";
    private readonly CanonicalJson canonical=new();
    public async Task<TestGateResolvedReferenceQueryResult> QueryAsync(TestGateResolvedReferenceQueryRequest request,CancellationToken cancellationToken=default)
    {
        var receipts=new List<StoredArtifact>();var copies=new List<TestGateResolvedCopy>();
        TestGateResolvedReferenceQueryResult Result(string code)=>new(code,copies.ToArray(),receipts.ToArray());
        if(!PropertyToken(request.Configuration)||!PropertyToken(request.TargetFramework))return Result("RESOLUTION_ARGUMENT_INVALID");
        string prefix;
        try
        {
            var relative=Relative(request.Run.WorkspaceRoot,request.Run.ReceiptDirectory);
            var parts=ArtifactPaths.Segments(relative);
            if(parts.Length!=3||parts[0]!="artifacts"||parts[1]!="tests")return Result("INPUT_SCOPE_VIOLATION");
            Relative(request.Run.WorkspaceRoot,request.Run.TestsProject);Relative(request.Run.WorkspaceRoot,request.Run.HostProject);
            Relative(request.Run.WorkspaceRoot,request.HostOutputPath);prefix=relative+"/native-reference-query";
        }
        catch(ArgumentException){return Result("INPUT_SCOPE_VIOLATION");}
        receipts.Add(await store.WriteAsync(new(prefix+"/query.json",Bytes(request),"application/json"),cancellationToken));
        var processRequest=new ProcessRequest(request.Run.DotNet,["msbuild",request.Run.TestsProject,
            "-getTargetResult:"+Target,"-getProperty:OutDir,MSBuildProjectFullPath,Configuration,TargetFramework,MSBuildToolsPath",
            "-property:BuildProjectReferences=false","-property:Configuration="+request.Configuration,
            "-property:TargetFramework="+request.TargetFramework,"-nologo","-verbosity:quiet"],request.Run.WorkspaceRoot,
            new Dictionary<string,string>(),Path.Combine(request.Run.WorkspaceRoot,prefix,"process"));
        ProcessResult process;
        try{process=await runner.RunAsync(processRequest,cancellationToken);}
        catch(IOException){return Result("RESOLUTION_OUTCOME_UNCERTAIN");} // No retry or acceptance after uncertain execution.
        var output=await captures.ReadAsync(process,false,cancellationToken);
        var error=await captures.ReadAsync(process,true,cancellationToken);
        receipts.Add(new(Relative(request.Run.WorkspaceRoot,process.StandardOutputPath),ContentHash.Sha256(output.Span),output.Length,"application/octet-stream"));
        receipts.Add(new(Relative(request.Run.WorkspaceRoot,process.StandardErrorPath),ContentHash.Sha256(error.Span),error.Length,"application/octet-stream"));
        if(process.ExitCode!=0||process.Cancelled)return Result("RESOLUTION_PROCESS_FAILED");
        string destination;
        try
        {
            using var document=JsonDocument.Parse(canonical.Serialize(output));
            var properties=document.RootElement.GetProperty("Properties");
            if(Normalize(Text(properties,"MSBuildToolsPath"))!=Normalize(request.Sdk.SdkDirectory))return Result("SDK_PIN_MISMATCH");
            if(Normalize(Text(properties,"MSBuildProjectFullPath"))!=Normalize(request.Run.TestsProject)||
                Text(properties,"Configuration")!=request.Configuration||Text(properties,"TargetFramework")!=request.TargetFramework)
                return Result("RESOLUTION_IDENTITY_MISMATCH");
            var target=document.RootElement.GetProperty("TargetResults").GetProperty(Target);
            if(Text(target,"Result")!="Success")return Result("RESOLUTION_TARGET_FAILED");
            var items=target.GetProperty("Items").EnumerateArray().Where(item=>Normalize(Text(item,"FullPath"))==Normalize(request.HostOutputPath)).ToArray();
            if(items.Length==0)return Result("RESOLVED_REFERENCE_MISSING");
            if(items.Length!=1)return Result("RESOLVED_REFERENCE_CONFLICT");
            if(Normalize(Text(items[0],"MSBuildSourceProjectFile"))!=Normalize(request.Run.HostProject))return Result("RESOLUTION_IDENTITY_MISMATCH");
            var targetPath=Text(items[0],"TargetPath").Replace('\\','/');
            try
            {
                ArtifactPaths.Segments(targetPath);
                var outDir=Text(properties,"OutDir");
                if(string.IsNullOrWhiteSpace(outDir))return Result("RESOLUTION_METADATA_INVALID");
                var outputRoot=Path.IsPathRooted(outDir) ? outDir : Path.Combine(Path.GetDirectoryName(request.Run.TestsProject)!,outDir);
                destination=Path.GetFullPath(Path.Combine(outputRoot,targetPath));Relative(request.Run.WorkspaceRoot,destination);
            }
            catch(ArgumentException){return Result("INPUT_SCOPE_VIOLATION");}
        }
        catch(Exception exception)when(exception is JsonException or FormatException or KeyNotFoundException or InvalidOperationException)
        {return Result("RESOLUTION_METADATA_INVALID");}
        var proof=new TestGateResolvedCopyProof(request.Run.ReceiptDirectory,request.Run.TestsProject,request.HostOutputPath,destination,
            request.Run.HostProject,request.Run.DotNet,request.Configuration,request.TargetFramework);
        var identity=await store.WriteAsync(new(prefix+"/reference-0.json",Bytes(proof),"application/json"),cancellationToken);
        receipts.Add(identity);copies.Add(new(request.HostOutputPath,destination,request.Run.HostProject,identity));return Result("OK");
    }
    private byte[] Bytes<T>(T value)=>canonical.Serialize(JsonSerializer.SerializeToElement(value));
    private static string Text(JsonElement element,string property)=>element.GetProperty(property).GetString()??throw new FormatException("Native metadata string is absent.");
    private static bool PropertyToken(string value)=>!string.IsNullOrEmpty(value)&&value.All(character=>
        character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_' or '-' or '.');
    private static string Normalize(string value)=>Path.TrimEndingDirectorySeparator(Path.GetFullPath(value));
    private static string Relative(string root,string path)
    {
        var relative=Path.GetRelativePath(Path.GetFullPath(root),Path.GetFullPath(path)).Replace('\\','/');
        ArtifactPaths.Resolve(root,relative);return relative;
    }
}
