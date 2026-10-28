using AndroidMcp.BuildTool.Core;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;

namespace AndroidMcp.BuildTool;

// Infrastructure-only host for the first green mock gate. Functional services
// and the authoritative registry are implemented only after their tests prove
// correct and incorrect mocked responses. No legacy executable is invoked.
public sealed class Build : NukeBuild
{
    [Parameter("Operation within a functional category; initial infrastructure supports Validate/MockGate")]
    public Operation Operation { get; set; }

    [Parameter("Approved native .NET host path (no mise/package provisioning shim)")]
    public string? DotNetHost { get; set; }

    public static int Main() => Execute<Build>(x => x.Validate);

    public Target Validate => _ => _
        .Description("Run the independent assertion/mock gate before implementing ported behavior")
        .Requires(() => DotNetHost)
        .Executes(() =>
        {
            if (Operation != Operation.MockGate)
                throw new InvalidOperationException("Infrastructure bootstrap only: select Validate --operation MockGate. Other operations are not yet implemented.");
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
                .SetFilter("Gate=Mock")
                .SetResultsDirectory(RootDirectory / "artifacts" / "tests" / "mock")
                .SetLoggers("trx;LogFileName=mock-gate.trx"));
        });
}
