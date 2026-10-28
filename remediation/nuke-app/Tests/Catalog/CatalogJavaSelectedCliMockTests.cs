using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

[Trait("Gate","Mock"),Trait("Requirement","TEST-BUILD-004")]
public sealed class CatalogJavaSelectedCliMockTests
{
    public static IEnumerable<object[]> Cases=>CatalogJavaSelectedCliCases.Cases;
    private static async Task<CatalogBuildCliObservation> Correct(CatalogJavaSelectedCliCase test)
    {
        var value=await new ScriptedCatalogBuildCli().RunAsync(test.BuildCase);
        return test.JavaManifest is null?value:value with{Arguments=value.Arguments.Concat(new[]{"--java-native-manifest",test.JavaManifest}).ToArray()};
    }
    [Theory,MemberData(nameof(Cases))]
    public async Task ExplicitSelectedManifestAndUnchangedDefaultCliPass(string id)
    {
        var test=CatalogJavaSelectedCliCases.Get(id);await CatalogJavaSelectedCliCases.AssertCase(test,await Correct(test));
    }
    [Theory,MemberData(nameof(Cases))]
    public async Task DroppedWrongDuplicatedOrUnrequestedJavaManifestCannotPass(string id)
    {
        var test=CatalogJavaSelectedCliCases.Get(id);var value=await Correct(test);
        var baseArguments=(await new ScriptedCatalogBuildCli().RunAsync(test.BuildCase)).Arguments;
        var wrongArguments=test.JavaManifest is null
            ?new[]{baseArguments.Concat(new[]{"--java-native-manifest","/unrequested.json"}).ToArray(),baseArguments.Concat(new[]{"--other-java-flag","/wrong.json"}).ToArray()}
            :new[]{baseArguments.ToArray(),baseArguments.Concat(new[]{"--java-native-manifest","/wrong.json"}).ToArray(),value.Arguments.Concat(new[]{"--java-native-manifest",test.JavaManifest}).ToArray()};
        foreach(var arguments in wrongArguments)
            Assert.NotNull(await Record.ExceptionAsync(()=>CatalogJavaSelectedCliCases.AssertCase(test,value with{Arguments=arguments})));
    }
}
