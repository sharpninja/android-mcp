using AndroidMcp.BuildTool.Core;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;

namespace AndroidMcp.BuildTool;

public sealed class Build : NukeBuild
{
    [Parameter("Operation within the selected functional category")]
    public Operation Operation { get; set; }

    [Parameter("Approved native .NET host path (no mise/package provisioning shim)")]
    public string? DotNetHost { get; set; }

    [Parameter("Optional VSTest filter; MockGate always retains Gate=Mock")]
    public string? TestFilter { get; set; }

    [Parameter("Source manifest for Catalog/Build")]
    public string? SourceManifest { get; set; }
    [Parameter("Canonical design project directory")]
    public string? ProjectRoot { get; set; }
    [Parameter("Output directory, separate from retained inputs")]
    public string? OutputRoot { get; set; }
    [Parameter("Immutable review snapshot")]
    public string? Snapshot { get; set; }
    [Parameter("Publication checkout for read-only deliverable checks")]
    public string? GitCheckout { get; set; }

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
        Add("output-root",OutputRoot); Add("snapshot",Snapshot);
        Add("git-checkout",GitCheckout); Add("test-filter",TestFilter);
        var result=new OperationPlanner(new OperationRegistry()).Plan(new(category.ToString(),Operation.ToString(),parameters));
        if(!result.IsValid) throw new InvalidOperationException(string.Join(Environment.NewLine,result.Errors.Select(x=>$"{x.Code}: {x.Parameter}: {x.Message}")));
        if(category!=Category.Validate || Operation is not (Operation.MockGate or Operation.Tests))
            throw new InvalidOperationException($"{category}/{Operation} has a valid plan but its execution adapter is not yet ported. No action executed.");
        RunTests();
    }

    private void RunTests()
    {
            var runId=DateTime.Now.ToString("yyyyMMddTHHmmssfffffff")+"-"+Guid.NewGuid().ToString("N");
            var results=RootDirectory / "artifacts" / "tests" / runId;
            var filter=Operation==Operation.MockGate ? "Gate=Mock" : null;
            if(!string.IsNullOrWhiteSpace(TestFilter)) filter=filter is null ? TestFilter : $"({filter})&({TestFilter})";
            var project = RootDirectory / "Tests" / "AndroidMcp.Build.Tests.csproj";
            DotNetTasks.DotNetBuild(s => s
                .SetProcessToolPath(DotNetHost!)
                .SetProjectFile(project)
                .SetNoRestore(true)
                .SetProperty("BuildProjectReferences", "false"));
            DotNetTasks.DotNetTest(s => s
                .SetProcessToolPath(DotNetHost!)
                .SetProjectFile(project)
                .SetNoBuild(true)
                .SetNoRestore(true)
                .SetFilter(filter)
                .SetResultsDirectory(results)
                .SetLoggers("trx;LogFileName=tests.trx"));
    }
}
