using AndroidMcp.BuildTool.Documentation.Validation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public sealed record BootstrapRuntimeObservation(string? Error,int Acquisitions,int ResultCount,bool InputsUnchanged,IReadOnlyDictionary<string,UnixFileMode> Modes);
public static class BootstrapRuntimeCases
{
    public static IReadOnlyDictionary<string,UnixFileMode> ExpectedModes=>new Dictionary<string,UnixFileMode>(StringComparer.Ordinal){
        [".tools/gradle-8.14.3/bin/gradle"]=(UnixFileMode)489,[".tools/gradle-8.14.3/bin/existing-extra"]=(UnixFileMode)457,
        [".tools/gradle-8.14.3/bin/existing-directory"]=(UnixFileMode)457,["gradlew"]=(UnixFileMode)493,["unrelated"]=(UnixFileMode)438};
    public static BootstrapRuntimeObservation Correct(bool rejectProperties)=>new(rejectProperties?"WRAPPER_PROPERTIES":null,rejectProperties?4:5,rejectProperties?0:5,true,ExpectedModes);
    public static void AssertObservation(bool rejectProperties,BootstrapRuntimeObservation observed)
    {
        var expected=Correct(rejectProperties);Assert.Equal(expected.Error,observed.Error);Assert.Equal(expected.Acquisitions,observed.Acquisitions);Assert.Equal(expected.ResultCount,observed.ResultCount);Assert.True(observed.InputsUnchanged);
        Assert.Equal(expected.Modes.Keys.Order(StringComparer.Ordinal),observed.Modes.Keys.Order(StringComparer.Ordinal));foreach(var pair in expected.Modes)Assert.Equal(pair.Value,observed.Modes[pair.Key]);
    }
}
public sealed record RepositoryRuntimeObservation(RepositoryDocumentationCheckResult Result,bool InputsUnchanged,IReadOnlyList<string> Reads);
public static class RepositoryRuntimeCases
{
    public static RepositoryRuntimeObservation Correct(string id){var original=new Validation.RepositoryDocumentationCase(id);return new(original.Expected!,true,original.Reads);}
    public static void AssertObservation(string id,RepositoryRuntimeObservation observed)
    {
        var expected=Correct(id);Assert.Equal(expected.Result.ChecksumsChecked,observed.Result.ChecksumsChecked);Assert.Equal(expected.Result.MarkdownLinksChecked,observed.Result.MarkdownLinksChecked);
        Assert.Equal(expected.Result.RenderedSvgCount,observed.Result.RenderedSvgCount);Assert.Equal(expected.Result.EditableUiCopies,observed.Result.EditableUiCopies);Assert.Equal(expected.Result.Errors,observed.Result.Errors);Assert.Equal(expected.Result.ExitCode,observed.Result.ExitCode);Assert.True(observed.InputsUnchanged);
        Assert.DoesNotContain(observed.Reads,path=>path.Split('/').Contains(".git",StringComparer.Ordinal));
        // Filesystem enumeration order is unspecified by the original rglob.
        // Preserve the exact read multiset, including the checksum/Markdown
        // repeated design read and the untracked Markdown content read.
        var wanted=expected.Reads.GroupBy(path=>path,StringComparer.Ordinal).ToDictionary(group=>group.Key,group=>group.Count(),StringComparer.Ordinal);
        var actual=observed.Reads.GroupBy(path=>path,StringComparer.Ordinal).ToDictionary(group=>group.Key,group=>group.Count(),StringComparer.Ordinal);
        Assert.Equal(wanted.Keys.Order(StringComparer.Ordinal),actual.Keys.Order(StringComparer.Ordinal));foreach(var pair in wanted)Assert.Equal(pair.Value,actual[pair.Key]);
    }
}
[Trait("Gate","Mock"),Trait("Requirement","FR-BUILD-005"),Trait("Requirement","TEST-BUILD-005")]
public sealed class DocumentationRuntimeMockTests
{
    [Theory,InlineData(false),InlineData(true)]
    public void OriginalModeEffectsAndPrePlantUmlRejectionPassIndependentObservations(bool rejectProperties)=>BootstrapRuntimeCases.AssertObservation(rejectProperties,BootstrapRuntimeCases.Correct(rejectProperties));
    [Theory,InlineData(false),InlineData(true)]
    public void EachPhysicalModeByteCounterAndFailureAssertionRejectsWrongObservation(bool rejectProperties)
    {
        var correct=BootstrapRuntimeCases.Correct(rejectProperties);
        foreach(var path in correct.Modes.Keys){var modes=correct.Modes.ToDictionary(pair=>pair.Key,pair=>pair.Value,StringComparer.Ordinal);modes[path]=0;Assert.NotNull(Record.Exception(()=>BootstrapRuntimeCases.AssertObservation(rejectProperties,correct with{Modes=modes})));}
        foreach(var wrong in new[]{correct with{Error="wrong"},correct with{Acquisitions=99},correct with{ResultCount=99},correct with{InputsUnchanged=false},correct with{Modes=new Dictionary<string,UnixFileMode>()}})
            Assert.NotNull(Record.Exception(()=>BootstrapRuntimeCases.AssertObservation(rejectProperties,wrong)));
    }
    [Theory,InlineData("GOOD"),InlineData("MULTIPLE-ERRORS")]
    public void OriginalFullRootCheckerObservationsPassIndependentMocks(string id)=>RepositoryRuntimeCases.AssertObservation(id,RepositoryRuntimeCases.Correct(id));
    [Theory,InlineData("GOOD"),InlineData("MULTIPLE-ERRORS")]
    public void EveryRootCounterErrorByteAndContentScopeAssertionRejectsWrongObservation(string id)
    {
        var value=RepositoryRuntimeCases.Correct(id);
        foreach(var wrong in new[]{value with{Result=value.Result with{ChecksumsChecked=99}},value with{Result=value.Result with{MarkdownLinksChecked=99}},value with{Result=value.Result with{RenderedSvgCount=99}},value with{Result=value.Result with{EditableUiCopies=99}},
            value with{Result=value.Result with{Errors=["wrong"]}},value with{InputsUnchanged=false},value with{Reads=value.Reads.Skip(1).ToArray()},value with{Reads=value.Reads.Append(".git/private.md").ToArray()}})
            Assert.NotNull(Record.Exception(()=>RepositoryRuntimeCases.AssertObservation(id,wrong)));
    }
}
