using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed record CatalogJavaSelectedCliCase(string Id,string? JavaManifest,CatalogBuildCase BuildCase);
public static class CatalogJavaSelectedCliCases
{
    public const string SelectedManifest="/selected native Java/native-source-lock.json";
    public static readonly IReadOnlyList<CatalogJavaSelectedCliCase> All=[
        new("JAVA-CLI-default",null,new("JAVA-CLI-default",new(),CatalogBuildCases.Base)),
        // SOURCE-IMPORT-02 adds three Java rows (package/type/method) and three
        // index rows (type/method/field) to the unchanged ten-row base fixture.
        new("JAVA-CLI-selected",SelectedManifest,new("JAVA-CLI-selected",new("source-integration"),
            CatalogBuildCases.Base with{Sources=5,Bindings=16,Occurrences=16,Declarations=16,Entries=16,Inventory=16}))
    ];
    public static IEnumerable<object[]> Cases=>All.Select(test=>new object[]{test.Id});
    public static CatalogJavaSelectedCliCase Get(string id)=>All.Single(test=>test.Id==id);
    private sealed class Observed(CatalogBuildCliObservation value):ICatalogBuildCliProbe
    {public Task<CatalogBuildCliObservation> RunAsync(CatalogBuildCase test)=>Task.FromResult(value);}
    public static Task AssertCase(CatalogJavaSelectedCliCase test,CatalogBuildCliObservation value)
    {
        var arguments=value.Arguments.ToList();
        if(test.JavaManifest is not null)
        {
            Assert.Equal(1,arguments.Count(argument=>argument=="--java-native-manifest"));
            var at=arguments.IndexOf("--java-native-manifest");Assert.Equal(arguments.Count-2,at);
            Assert.Equal(test.JavaManifest,arguments[at+1]);arguments.RemoveRange(at,2);
        }
        // The existing authority still checks exact registered argv, current
        // host/DLL pins, captures, operation and every independent Build field.
        return CatalogBuildCliCases.AssertCase(test.BuildCase,new Observed(value with{Arguments=arguments}));
    }
}
