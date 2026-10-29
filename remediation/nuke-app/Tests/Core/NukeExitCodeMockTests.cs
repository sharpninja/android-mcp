using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

public static class NukeExitCodeCases
{
    public static IEnumerable<object[]> Cases => new[] { 0, 1, -1, 2, 255, -255, int.MinValue, int.MaxValue }
        .Select(code => new object[] { code, code == 0 ? 0 : 1 });
    public static void AssertCase(int engineCode, int expected, Func<int,int> policy)
    {
        Assert.Equal(expected, policy(engineCode));
    }
}

[Trait("Gate","Mock"), Trait("Requirement","TEST-BUILD-002")]
public sealed class NukeExitCodeMockTests
{
    [Theory, MemberData(nameof(NukeExitCodeCases.Cases), MemberType=typeof(NukeExitCodeCases))]
    public void PortablePublicResultCorrectMock(int engineCode, int expected) =>
        NukeExitCodeCases.AssertCase(engineCode,expected,_=>expected);

    [Theory, MemberData(nameof(NukeExitCodeCases.Cases), MemberType=typeof(NukeExitCodeCases))]
    public void WrongSuccessFailureAndRawEngineValuesAreRejected(int engineCode, int expected)
    {
        Assert.NotNull(Record.Exception(()=>NukeExitCodeCases.AssertCase(engineCode,expected,_=>expected==0?1:0)));
        Assert.NotNull(Record.Exception(()=>NukeExitCodeCases.AssertCase(engineCode,expected,_=>expected+2)));
    }
}
