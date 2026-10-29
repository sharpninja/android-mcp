using System.Text;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public static class DesignGradleNativeHostForwardingCases
{
    public static Artifact CorrectedScript()
    {
        var path=Path.Combine(DocumentationFixture.Current.Workspace,"remediation/nuke-planning/docs/fixtures/gradle-native-delegate.with-dotnet-host.txt");
        var bytes=File.ReadAllBytes(path);Assert.Equal("27fd8e364d815c471f79101c2707bf11e7e492359dbaf56b9c8a7aa097fb08a8",DocumentationFixture.Hash(bytes));
        return new("build.gradle.kts",bytes,"text/plain");
    }
    public static Artifact RetainedOriginal()
    {
        var path=Path.Combine(DocumentationFixture.Current.Workspace,"remediation/nuke-planning/docs/fixtures/gradle-native-delegate.txt");
        var bytes=File.ReadAllBytes(path);Assert.Equal("cf251efbbed05d0f54626a6f01063705735d1fe0447d654ddfa7653c6e95ae65",DocumentationFixture.Hash(bytes));
        return new("build.gradle.kts",bytes,"text/plain");
    }
    public static void AssertProjection(DesignGradleProjectionCase test,IDesignGradleProjection provider)
    {
        var actual=provider.Render(test.Stages,test.Operation);var descriptor=DesignGradleProjectionCases.Descriptor(test);var script=CorrectedScript();
        Assert.Equal(descriptor.RelativePath,actual.Descriptor.RelativePath);Assert.Equal(descriptor.MediaType,actual.Descriptor.MediaType);Assert.Equal(descriptor.Bytes.ToArray(),actual.Descriptor.Bytes.ToArray());
        Assert.Equal(script.RelativePath,actual.BuildScript.RelativePath);Assert.Equal(script.MediaType,actual.BuildScript.MediaType);Assert.Equal(script.Bytes.ToArray(),actual.BuildScript.Bytes.ToArray());
        var text=Encoding.UTF8.GetString(actual.BuildScript.Bytes.Span);
        Assert.Contains("\"--dotnet-host\", nativeHost.absolutePath,",text,StringComparison.Ordinal);
        Assert.Equal(1,text.Split("\"--dotnet-host\"",StringSplitOptions.None).Length-1);
        Assert.Equal(1,text.Split("commandLine(arguments)",StringSplitOptions.None).Length-1);
    }
    public static DesignGradleProjection Reply(DesignGradleProjectionCase test,string mutation="")
    {
        var script=CorrectedScript();
        if(mutation=="missing")script=RetainedOriginal();
        if(mutation=="wrong-host")script=script with{Bytes=Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(script.Bytes.Span).Replace("\"--dotnet-host\", nativeHost.absolutePath,","\"--dotnet-host\", \"dotnet\",",StringComparison.Ordinal))};
        if(mutation=="duplicate")script=script with{Bytes=Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(script.Bytes.Span).Replace("\"--dotnet-host\", nativeHost.absolutePath,","\"--dotnet-host\", nativeHost.absolutePath, \"--dotnet-host\", nativeHost.absolutePath,",StringComparison.Ordinal))};
        return new(DesignGradleProjectionCases.Descriptor(test),script);
    }
}
[Trait("Gate","Mock")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class DesignGradleNativeHostForwardingMockTests
{
    [Fact]
    public void IndependentFixtureChangesOnlyTheApprovedGlobalHostArgument()
    {
        var before=Encoding.UTF8.GetString(DesignGradleNativeHostForwardingCases.RetainedOriginal().Bytes.Span);
        var after=Encoding.UTF8.GetString(DesignGradleNativeHostForwardingCases.CorrectedScript().Bytes.Span);
        const string anchor="            \"--\", \"--root\", buildProject.parentFile.absolutePath,\n";
        const string insertion="            \"--dotnet-host\", nativeHost.absolutePath,\n";
        Assert.Equal(1,before.Split(anchor,StringSplitOptions.None).Length-1);Assert.Equal(before.Replace(anchor,anchor+insertion,StringComparison.Ordinal),after);
    }
    [Theory,MemberData(nameof(DesignGradleProjectionCases.Cases),MemberType=typeof(DesignGradleProjectionCases))]
    public void CorrectSingleInvocationMocksForwardTheSelectedNativeHost(DesignGradleProjectionCase test)=>
        DesignGradleNativeHostForwardingCases.AssertProjection(test,new ScriptedDesignGradleProjection(DesignGradleNativeHostForwardingCases.Reply(test)));
    [Theory,MemberData(nameof(DesignGradleProjectionCases.Cases),MemberType=typeof(DesignGradleProjectionCases))]
    public void EveryMissingNativeHostFlagIsRejected(DesignGradleProjectionCase test)=>Assert.NotNull(Record.Exception(()=>
        DesignGradleNativeHostForwardingCases.AssertProjection(test,new ScriptedDesignGradleProjection(DesignGradleNativeHostForwardingCases.Reply(test,"missing")))));
    [Theory]
    [InlineData("wrong-host")][InlineData("duplicate")]
    public void SubstitutionAndDuplicateHostArgumentsAreRejected(string mutation)
    {var test=DesignGradleProjectionCases.Original();Assert.NotNull(Record.Exception(()=>DesignGradleNativeHostForwardingCases.AssertProjection(test,new ScriptedDesignGradleProjection(DesignGradleNativeHostForwardingCases.Reply(test,mutation)))));}
}
