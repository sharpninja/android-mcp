using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation.Browser;
using AndroidMcp.BuildTool.Tests.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation.Browser;

public sealed class NativeBrowserFixture:IAsyncLifetime
{
    public ToolIdentity Browser {get;private set;}=null!;
    internal static BrowserQaOperation Operation(){var clock=new SystemClock();var processes=new NativeProcessRunner(clock);return new(processes,processes,clock);}
    public async Task InitializeAsync()
    {
        var root=ProcessSessionFixturePaths.NewRoot("browser-tool-probe");var pinned=new ToolIdentity("/usr/lib/chromium/chromium","27c398e1738a5bf578e4414654b959b8fdb63446dfb309c114a95e97c5d243ff","");
        Browser=await Operation().ProbeAsync(pinned,root,root);
    }
    public Task DisposeAsync()=>Task.CompletedTask;
}
[Trait("Gate","Real"),Trait("Requirement","TEST-BUILD-005")]
public sealed class BrowserQaRealTests(NativeBrowserFixture fixture):IClassFixture<NativeBrowserFixture>
{
    public static IEnumerable<object[]> Cases=>BrowserQaCases.Cases;
    [Theory,MemberData(nameof(Cases))]
    public async Task SameSevenOfflineInputsAndEveryAssertionBindToNativeChromium(string id)
    {
        var test=BrowserQaCases.Get(id);var root=ProcessSessionFixturePaths.NewRoot("browser-qa-tests");var request=BrowserQaCases.CreateInput(root,test,fixture.Browser);var store=new FileArtifactStore(request.OutputRoot,bytes=>ContentHash.Sha256(bytes.Span));
        await BrowserQaCases.AssertContract(test,NativeBrowserFixture.Operation(),store,request);
    }
}
[Trait("Gate","Real"),Trait("Requirement","TEST-BUILD-005")]
public sealed class BrowserQaDenominatorRealTests(NativeBrowserFixture fixture):IClassFixture<NativeBrowserFixture>
{
    [Theory,InlineData(21),InlineData(0)]
    public async Task SameExplicitFullAndUiDenominatorsBindToNativeChromium(int expected)
    {
        var root=ProcessSessionFixturePaths.NewRoot("browser-qa-denominator-tests");var request=BrowserQaDenominatorCases.Input(root,expected,fixture.Browser);var store=new FileArtifactStore(request.OutputRoot,bytes=>ContentHash.Sha256(bytes.Span));
        await BrowserQaDenominatorCases.AssertContract(expected,NativeBrowserFixture.Operation(),store,request);
    }
}
