using AndroidMcp.BuildTool.Core;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using AndroidMcp.BuildTool.Documentation;
using AndroidMcp.BuildTool.Sources.Java;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;

namespace AndroidMcp.BuildTool;

public sealed partial class Build : NukeBuild
{
    [Parameter("Operation within the selected functional category")]
    public Operation Operation { get; set; }

    [Parameter("Approved native .NET host path (no mise/package provisioning shim)")]
    public string? DotNetHost { get; set; }

    [Parameter("Optional VSTest filter; MockGate always retains Gate=Mock")]
    public string? TestFilter { get; set; }

    [Parameter("Source manifest for Catalog/Build")]
    public string? SourceManifest { get; set; }
    [Parameter("Locked recipe identity file, resolved relative to --project-root")]
    public string? RecipeLock { get; set; }
    [Parameter("Explicit immutable source bundle directory")]
    public string? BundleRoot { get; set; }
    [Parameter("Optional expected recipe release")]
    public string? Release { get; set; }
    [Parameter("Optional selected physical source limit; logical rows remain retained")]
    public int? Limit { get; set; }
    [Parameter("Explicit acquisition compatibility profile; default CanonicalRestore")]
    public AcquisitionCompatibilityProfile? AcquisitionProfile { get; set; }
    [Parameter("Historical cache handling; default preserves alias metadata without claiming import")]
    public SourceBatchCachePolicy? CachePolicy { get; set; }
    [Parameter("Canonical project input or catalog-contract directory")]
    public string? ProjectRoot { get; set; }
    [Parameter("Output directory, separate from retained inputs")]
    public string? OutputRoot { get; set; }
    [Parameter("Private catalog staging directory, separate from final output")]
    public string? StageRoot { get; set; }
    [Parameter("Explicit source extraction profile: release-011 or current-candidate")]
    public string? ExtractionProfile { get; set; }
    [Parameter("Private architecture renderer work directory")]
    public string? WorkRoot { get; set; }
    [Parameter("Content-pinned native Docker ToolIdentity JSON file")]
    public string? DockerIdentity { get; set; }
    [Parameter("Renderer container user uid:gid")]
    public string? RendererUser { get; set; }
    [Parameter("Explicit Online or Offline pinned tool access")]
    public ToolArtifactAccess? ToolAccess { get; set; }
    [Parameter("Retained published Gradle checksum proof, relative to the tool cache")]
    public string? OfflineGradleChecksumProof { get; set; }
    [Parameter("Immutable review snapshot")]
    public string? Snapshot { get; set; }
    [Parameter("Publication checkout for read-only deliverable checks")]
    public string? GitCheckout { get; set; }
    [Parameter("Content-pinned native Git ToolIdentity JSON file")]
    public string? GitIdentity { get; set; }
    [Parameter("Optional root-relative publication policy path; default deliverables.json")]
    public string? PolicyRelativePath { get; set; }
    [Parameter("Stable generation identifier for reproducible generated documentation")]
    public string? Generation { get; set; }
    [Parameter("JSON ToolIdentity for the already installed, content-pinned native compiler")]
    public string? CompilerIdentity { get; set; }
    [Parameter("Optional native-source cache directory")]
    public string? CacheRoot { get; set; }
    [Parameter("Optional immutable native process receipt directory")]
    public string? ReceiptRoot { get; set; }
    [Parameter("Exact pinned Java native source-lock manifest required by native integration tests")]
    public string? JavaNativeManifest { get; set; }

    public static int Main(string[] args) => args.FirstOrDefault()=="--internal-process-probe"
        ? ProcessContractProbe.Run(args.Skip(1).ToArray())
        : Execute<Build>(x => x.Validate);

    public Target Sources => CategoryTarget(Category.Sources);
    public Target Catalog => CategoryTarget(Category.Catalog);
    public Target Design => CategoryTarget(Category.Design);
    public Target Validate => CategoryTarget(Category.Validate);
    public Target Review => CategoryTarget(Category.Review);
    public Target Publish => CategoryTarget(Category.Publish);

    private Target CategoryTarget(Category category) => _ => _
        .Description($"Run a registered {category} operation selected by --operation")
        .Requires(() => DotNetHost)
        .Executes(() => Dispatch(category));

    private void Dispatch(Category category)
    {
        var parameters=new Dictionary<string,string>(StringComparer.Ordinal);
        void Add(string key,string? value) { if(value is not null) parameters.Add(key,value); }
        Add("source-manifest",SourceManifest); Add("project-root",ProjectRoot);
        Add("recipe-lock",RecipeLock);Add("bundle-root",BundleRoot);Add("release",Release);
        Add("limit",Limit?.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Add("acquisition-profile",AcquisitionProfile?.ToString());Add("cache-policy",CachePolicy?.ToString());
        Add("output-root",OutputRoot); Add("snapshot",Snapshot);
        Add("stage-root",StageRoot);Add("extraction-profile",ExtractionProfile);Add("work-root",WorkRoot);
        Add("docker-identity",DockerIdentity);Add("renderer-user",RendererUser);Add("tool-access",ToolAccess?.ToString());
        Add("offline-gradle-checksum-proof",OfflineGradleChecksumProof);
        Add("git-checkout",GitCheckout); Add("test-filter",TestFilter);
        Add("git-identity",GitIdentity);Add("policy-relative-path",PolicyRelativePath);
        Add("generation",Generation);
        Add("compiler-identity",CompilerIdentity);Add("cache-root",CacheRoot);Add("receipt-root",ReceiptRoot);
        Add("java-native-manifest",JavaNativeManifest);
        var registry=new OperationRegistry();
        var result=new OperationPlanner(registry).Plan(new(category.ToString(),Operation.ToString(),parameters));
        if(!result.IsValid) throw new InvalidOperationException(string.Join(Environment.NewLine,result.Errors.Select(x=>$"{x.Code}: {x.Parameter}: {x.Message}")));
        switch(registry.Find(result.Plan!.Key)!.Executor)
        {
            case OperationExecutor.MockTests: RunTests(true);break;
            case OperationExecutor.Tests: RunTests(false);break;
            case OperationExecutor.DocumentationAssets: GenerateAssets().GetAwaiter().GetResult();break;
            case OperationExecutor.NativeParser: BuildNativeParser().GetAwaiter().GetResult();break;
            case OperationExecutor.SourcesFetch: FetchSources().GetAwaiter().GetResult();break;
            case OperationExecutor.PublicationCheck: CheckPublication().GetAwaiter().GetResult();break;
            case OperationExecutor.SourcesPlan: PlanSources().GetAwaiter().GetResult();break;
            case OperationExecutor.Architecture: GenerateArchitecture().GetAwaiter().GetResult();break;
            default: throw new InvalidOperationException($"{category}/{Operation} has a valid plan but its execution adapter is not yet ported. No action executed.");
        }
    }

    private async Task GenerateAssets()
    {
        var input=Path.GetFullPath(ProjectRoot!);var output=Path.GetFullPath(OutputRoot!);
        if(input==output)throw new InvalidOperationException("Generated documentation requires a separate output root.");
        var hashes=new NativeContentHash();
        var source=new FileArtifactStore(input,bytes=>hashes.Sha256(bytes.Span));
        var destination=new FileArtifactStore(output,bytes=>hashes.Sha256(bytes.Span));
        var operation=new DocumentationAssetOperation(source,destination,
            new FileArtifactLeaseProvider(Path.Combine(output,".leases")),new DocumentationProjector(),hashes,output);
        var result=await operation.GenerateAsync(Generation??"assets");
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new{result.GenerationId,InputCount=result.Inputs.Count,OutputCount=result.Outputs.Count,result.Manifest,OutputRoot=output}));
    }

    private async Task BuildNativeParser()
    {
        var compiler=await ReadToolIdentity(CompilerIdentity!,"Compiler");
        var clock=new SystemClock();
        var operation=new JavaNativeWorkflow(HttpByteTransport.Create(),new NativeArchiveExtractor(),new NativeProcessRunner(clock),clock);
        var result=await operation.RunAsync(new(OutputRoot!,compiler,CacheRoot,ReceiptRoot,ProjectRoot));
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new{result.Manifest,Native=result.Build.Artifact,OutputRoot=Path.GetFullPath(OutputRoot!)}));
    }

    private void RunTests(bool mockOnly)
    {
            var runId=DateTime.Now.ToString("yyyyMMddTHHmmssfffffff")+"-"+Guid.NewGuid().ToString("N");
            var results=RootDirectory / "artifacts" / "tests" / runId;
            var filter=mockOnly ? "Gate=Mock" : null;
            if(!string.IsNullOrWhiteSpace(TestFilter)) filter=filter is null ? TestFilter : $"({filter})&({TestFilter})";
            var project = RootDirectory / "Tests" / "AndroidMcp.Build.Tests.csproj";
            DotNetTasks.DotNetBuild(s => s
                .SetProcessToolPath(DotNetHost!)
                .SetProjectFile(project)
                .SetNoRestore(true)
                .SetProperty("BuildProjectReferences", "false"));
            var oldManifest=Environment.GetEnvironmentVariable("ANDROIDMCP_JAVA_NATIVE_MANIFEST");
            if(JavaNativeManifest is not null)
            {
                if(!File.Exists(JavaNativeManifest))throw new InvalidOperationException("The selected Java native manifest does not exist.");
                Environment.SetEnvironmentVariable("ANDROIDMCP_JAVA_NATIVE_MANIFEST",Path.GetFullPath(JavaNativeManifest));
            }
            try { DotNetTasks.DotNetTest(s => s
                .SetProcessToolPath(DotNetHost!)
                .SetProjectFile(project)
                .SetNoBuild(true)
                .SetNoRestore(true)
                .SetFilter(filter)
                .SetResultsDirectory(results)
                .SetLoggers("trx;LogFileName=tests.trx")); }
            finally { if(JavaNativeManifest is not null)Environment.SetEnvironmentVariable("ANDROIDMCP_JAVA_NATIVE_MANIFEST",oldManifest); }
    }
}
