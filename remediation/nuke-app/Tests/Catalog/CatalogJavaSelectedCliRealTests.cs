using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using AndroidMcp.BuildTool.Sources.Java;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

[Trait("Gate","Real"),Trait("Requirement","TEST-BUILD-004")]
public sealed class CatalogJavaSelectedCliRealTests
{
    public static IEnumerable<object[]> Cases=>CatalogJavaSelectedCliCases.Cases;
    private sealed class Observed(CatalogSourceIntegrationReply value):ICatalogSourceIntegrationCaseProvider
    {public CatalogSourceIntegrationReply Import(CatalogSourceIntegrationCall call)=>value;}
    [Theory,MemberData(nameof(Cases))]
    public async Task SameDefaultAndExplicitJavaManifestCasesUseRegisteredCatalogBuild(string id)
    {
        var test=CatalogJavaSelectedCliCases.Get(id);
        if(test.JavaManifest is null)
        {
            await CatalogJavaSelectedCliCases.AssertCase(test,await new NativeCatalogBuildCliProbe().RunAsync(test.BuildCase));return;
        }
        var manifest=Environment.GetEnvironmentVariable("ANDROIDMCP_JAVA_NATIVE_MANIFEST")??throw new InvalidOperationException("Select the exact pinned Java native manifest for the registered Java caller case");
        manifest=Path.GetFullPath(manifest);test=test with{JavaManifest=manifest};
        var sourceCase=CatalogSourceIntegrationCases.All.Select(row=>(CatalogSourceIntegrationCase)row[0]).Single(row=>row.Call.Variant=="java-jvm-link");
        using var runtime=new SourcesRuntimeFactory(new JavaSourceRuntimeLoader()).Create(SourceExtractionProfile.CurrentCandidate,new SourceJavaAdmission(manifest));
        NativeCatalogBuildProvider? fixture=null;
        var probe=new NativeCatalogBuildCliProbe(execute=>fixture=NativeCatalogSourceIntegrationProvider.CreateProvider(sourceCase.Call,runtime.Dispatcher,execute),manifest);
        var value=await probe.RunAsync(test.BuildCase);await CatalogJavaSelectedCliCases.AssertCase(test,value);Assert.NotNull(fixture);
        CatalogSourceIntegrationCases.AssertCase(sourceCase,new Observed(NativeCatalogSourceIntegrationProvider.Observe(sourceCase.Call,fixture,value.Build)));
    }
}
