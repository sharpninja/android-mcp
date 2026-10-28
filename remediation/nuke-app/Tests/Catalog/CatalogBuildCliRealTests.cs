using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Catalog;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed class NativeCatalogBuildCliProbe:ICatalogBuildCliProbe
{
    public Task<CatalogBuildCliObservation> RunAsync(CatalogBuildCase test)=>Task.Run(()=>Run(test));
    private static CatalogBuildCliObservation Run(CatalogBuildCase test)
    {
        // The shared fixture provider creates the exact original source inputs
        // and independently validates every DB/export field after this delegate.
        ProcessResult? process=null;string[]? actualArguments=null;string? runtime=null;string? manifest=null;string? output=null;string? stage=null;
        var stdout="";var stderr="";var registered=false;var hostPinned=false;var dllPinned=false;
        var host=Environment.ProcessPath ?? throw new InvalidOperationException("Native dotnet host is unavailable");
        Assert.Equal("dotnet",Path.GetFileNameWithoutExtension(host));var dll=typeof(AndroidMcp.BuildTool.Build).Assembly.Location;
        var tool=new ToolIdentity(host,ContentHash.Sha256(File.ReadAllBytes(host)),Environment.Version.ToString());var dllHash=ContentHash.Sha256(File.ReadAllBytes(dll));
        using var provider=new NativeCatalogBuildProvider
        {
            Execution=async invocation=>
            {
                manifest=invocation.InventoryPath;output=invocation.OutputDirectory;stage=invocation.StageDirectory;
                var root=Path.GetDirectoryName(manifest)!;runtime=Path.Combine(root,"isolated nuke runtime");Directory.CreateDirectory(Path.Combine(runtime,".nuke"));
                var arguments=CatalogBuildCliCases.Arguments(host,dll,runtime,NativeSchemaProvider.Root(),manifest,output,stage).ToList();
                arguments.InsertRange(1,["--runtimeconfig",Path.Combine(AppContext.BaseDirectory,"AndroidMcp.Build.Tests.runtimeconfig.json"),"--depsfile",Path.Combine(AppContext.BaseDirectory,"AndroidMcp.Build.Tests.deps.json")]);
                actualArguments=arguments.ToArray();
                // This invokes the current compiled monolith. It never invokes
                // build, restore, run, an old producer, a shell or a second CLI.
                process=await new NativeProcessRunner(new SystemClock()).RunAsync(new(tool,actualArguments,runtime,new Dictionary<string,string>(),Path.Combine(root,"cli process receipts")));
                stdout=await File.ReadAllTextAsync(process.StandardOutputPath);stderr=await File.ReadAllTextAsync(process.StandardErrorPath);
                hostPinned=ContentHash.Sha256(File.ReadAllBytes(host))==tool.Sha256;
                dllPinned=ContentHash.Sha256(File.ReadAllBytes(dll))==dllHash && actualArguments[5]==dll;
                if(process.ExitCode==0)
                {
                    registered=stdout.Split('\n').Any(line=>line.Trim().StartsWith("{\"RegistryId\":",StringComparison.Ordinal));return null;
                }
                var failurePath=Path.Combine(output,"validation-failure.json");
                if(File.Exists(failurePath))
                {
                    var receipt=JsonNode.Parse(await File.ReadAllTextAsync(failurePath))!;
                    registered=receipt["published"]!.GetValue<bool>()==false && receipt["native_producer"]!["id"]!.GetValue<string>()==CatalogBuild.ProducerId;
                    return new CatalogBuildException(receipt["code"]!.GetValue<string>(),receipt["error"]!.GetValue<string>());
                }
                if((stdout+stderr).Contains("Output must be empty; preserve immutable earlier publication",StringComparison.Ordinal))
                {registered=true;return new CatalogBuildException("nonempty-output","Observed registered build's populated-output refusal");}
                throw new InvalidOperationException("Catalog CLI did not reach a recorded domain outcome. Fixture retained at "+root+Environment.NewLine+stdout+Environment.NewLine+stderr);
            }
        };
        var build=provider.Build(test.Call);Assert.NotNull(process);Assert.NotNull(actualArguments);
        return new(build,process.ExitCode,File.Exists(process.StandardOutputPath),File.Exists(process.StandardErrorPath),dllPinned,hostPinned,registered,
            actualArguments,host,dll,runtime!,NativeSchemaProvider.Root(),manifest!,output!,stage!);
    }
}
[Trait("Gate","Real")]
[Trait("Requirement","TEST-BUILD-004")]
public sealed class CatalogBuildCliRealTests
{
    [Theory,MemberData(nameof(CatalogBuildCases.All),MemberType=typeof(CatalogBuildCases))]
    public Task CurrentRegisteredCatalogCliUsesSameBuildCasesAndAssertions(CatalogBuildCase test)=>CatalogBuildCliCases.AssertCase(test,new NativeCatalogBuildCliProbe());
}
