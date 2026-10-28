using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool;

// Root-owned Main integration handoff. Invoke only for the exact internal mode
// before Execute<Build>/NUKE initialization. No new public test operation.
public sealed partial class Build
{
    internal static async Task<int> RunSdkEvaluationOnceAsync(string[] args)
    {
        try
        {
            if(args.Length!=7||args[0]!="--sdk-evaluation-once"||args[1]!="--pipeline-request"||
                args[3]!="--pipeline-request-sha256"||args[5]!="--pipeline-request-length"||
                !long.TryParse(args[6],NumberStyles.None,CultureInfo.InvariantCulture,out var length)||length<=0)
                throw new InvalidOperationException("SDK_CHILD_ARGUMENTS_INVALID");
            var root=Path.TrimEndingDirectorySeparator(Path.GetFullPath(Directory.GetCurrentDirectory()));
            var relative=Path.GetRelativePath(root,Path.GetFullPath(args[2])).Replace('\\','/');
            ArtifactPaths.Resolve(root,relative);
            var segments=ArtifactPaths.Segments(relative);
            if(segments.Length!=5||segments[0]!="artifacts"||segments[1]!="tests"||
                !segments[3].StartsWith("sdk-evaluation-",StringComparison.Ordinal)||segments[4]!="request.json")
                throw new InvalidOperationException("SDK_CHILD_REQUEST_SCOPE_INVALID");
            // Working-directory admission precedes the first payload read. The
            // payload cannot grant its own initial root or arbitrary file read.
            var store=new FileArtifactStore(root,bytes=>ContentHash.Sha256(bytes.Span));
            var pins=new PinnedArtifactReader(store);
            var requestPin=new StoredArtifact(relative,args[4],length,"application/json");
            var bytes=await pins.ReadAsync(requestPin);
            var canonical=new CanonicalJson();
            var request=JsonSerializer.Deserialize<TestGateSdkChildRequest>(canonical.Serialize(bytes),
                new JsonSerializerOptions{UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow})
                ??throw new InvalidOperationException("SDK_CHILD_REQUEST_INVALID");
            var evaluation=request.Native.Evaluation;
            var run=evaluation.Run;
            var parent=relative[..relative.LastIndexOf('/')];
            var expectedRun=Path.GetFullPath(Path.Combine(root,segments[0],segments[1],segments[2]));
            if(Path.TrimEndingDirectorySeparator(Path.GetFullPath(run.WorkspaceRoot))!=root||
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(run.ReceiptDirectory))!=expectedRun||
                request.EvaluationRelativePath!=parent+"/evaluation.json"||request.RuntimeFiles.Count!=2)
                throw new InvalidOperationException("SDK_CHILD_REQUEST_SCOPE_INVALID");
            ArtifactPaths.Resolve(root,request.EvaluationRelativePath);
            var assembly=typeof(Build).Assembly;
            var imagePath=Path.GetFullPath(assembly.Location);
            var imageRelative=Path.GetRelativePath(root,imagePath).Replace('\\','/');
            ArtifactPaths.Resolve(root,imageRelative);
            var imageBytes=await store.ReadAsync(imageRelative);
            var image=TestGateManagedPeIdentity.Observe(imagePath,imageBytes);
            if(image!=request.Monolith||image.ModuleId!=assembly.ManifestModule.ModuleVersionId)
                throw new InvalidOperationException("SDK_CHILD_CODE_IDENTITY_MISMATCH");
            foreach(var runtime in request.RuntimeFiles)await pins.ReadAsync(runtime);
            var sdkStore=new FileArtifactStore(evaluation.Sdk.SdkDirectory,value=>ContentHash.Sha256(value.Span));
            var configuration=new TestGateNativeFactsConfiguration(evaluation.Sdk,request.Native.ApprovedReadRoots,
                evaluation.Configuration,evaluation.TargetFramework);
            var sessions=NativeTestGateSdkSessionFactory.CreateForDedicatedChild(evaluation.Sdk.SdkDirectory,new PinnedArtifactReader(sdkStore));
            var query=new NativeTestGateResolvedReferenceQuery(new NativeProcessRunner(new SystemClock()),new ProcessCaptureReader(root),store);
            var evaluated=await new NativeTestGateSdkEvaluator(configuration,sessions,query).EvaluateAsync(evaluation);
            var result=await store.WriteNewAsync(new(request.EvaluationRelativePath,
                canonical.Serialize(JsonSerializer.SerializeToElement(evaluated)),"application/json"));
            var receipt=new TestGateSdkChildReceipt(requestPin.Sha256,image,result);
            Console.WriteLine(System.Text.Encoding.UTF8.GetString(canonical.Serialize(JsonSerializer.SerializeToElement(receipt))));
            return 0;
        }
        catch(Exception error)
        {
            Console.Error.WriteLine(error.ToString());
            return 1;
        }
    }
}
