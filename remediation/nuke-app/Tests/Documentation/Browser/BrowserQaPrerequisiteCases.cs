using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation.Browser;

public sealed record BrowserQaPrerequisiteCall(string Variant,int ExpectedArchitectureDiagrams=1);
public sealed record BrowserQaPrerequisiteReply(string Outcome,int BrowserInvocations,bool OutputCreated,bool InputsUnchanged,
    int SvgFiles,int ArchitectureDiagrams,string Scope,bool UiManifestPinRetained,bool ArchitectureManifestPinRetained);
public sealed record BrowserQaPrerequisiteCase(string Id,BrowserQaPrerequisiteCall Call,BrowserQaPrerequisiteReply Expected);
public interface IBrowserQaPrerequisiteCaseProvider
{BrowserQaPrerequisiteReply Run(BrowserQaPrerequisiteCall call);}
public static class BrowserQaPrerequisiteCases
{
    private static BrowserQaPrerequisiteReply Refusal(string code)=>new(code,0,false,true,0,0,"refused",false,false);
    public static readonly IReadOnlyList<BrowserQaPrerequisiteCase> All=
    [
        new("BROWSER-PREQ-01-split-immutable-generations",new("split"),new("ok",1,true,true,5,1,"ui-and-architecture",true,true)),
        new("BROWSER-PREQ-02-explicit-ui-only",new("ui-only",0),new("ok",1,true,true,4,0,"ui-only",true,false)),
        new("BROWSER-PREQ-03-missing-generation-manifest",new("missing-manifest"),Refusal("generation-manifest-missing")),
        new("BROWSER-PREQ-04-wrong-selected-pin",new("manifest-pin"),Refusal("generation-manifest-pin")),
        new("BROWSER-PREQ-05-mutated-declared-artifact",new("artifact-pin"),Refusal("generation-artifact-pin")),
        new("BROWSER-PREQ-06-missing-storyboard-html",new("missing-html"),Refusal("generation-required-artifact")),
        new("BROWSER-PREQ-07-malformed-manifest",new("malformed-manifest"),Refusal("generation-manifest-invalid")),
        new("BROWSER-PREQ-08-no-architecture-default-full",new("missing-architecture",21),Refusal("generation-architecture-required")),
        new("BROWSER-PREQ-09-output-inside-architecture",new("output-overlap"),Refusal("generation-output-overlap")),
        new("BROWSER-PREQ-10-populated-output",new("populated-output"),Refusal("generation-output-not-fresh")),
        new("BROWSER-PREQ-11-undeclared-svg",new("undeclared-svg"),Refusal("generation-undeclared-artifact")),
        new("BROWSER-PREQ-12-wrong-diagram-count",new("diagram-count",21),Refusal("generation-diagram-count")),
        new("BROWSER-PREQ-13-unsafe-manifest-path",new("unsafe-path"),Refusal("generation-artifact-path")),
        new("BROWSER-PREQ-14-both-roots-same-svg-label",new("duplicate-label"),Refusal("generation-artifact-collision"))
    ];
    public static IEnumerable<object[]> Cases=>All.Select(test=>new object[]{test.Id});
    public static BrowserQaPrerequisiteCase Get(string id)=>All.Single(test=>test.Id==id);
    public static void AssertContract(BrowserQaPrerequisiteCase test,IBrowserQaPrerequisiteCaseProvider provider)=>Assert.Equal(test.Expected,provider.Run(test.Call));
}
public sealed class ScriptedBrowserQaPrerequisites(BrowserQaPrerequisiteCase test,int wrong=-1):IBrowserQaPrerequisiteCaseProvider
{
    public BrowserQaPrerequisiteReply Run(BrowserQaPrerequisiteCall call)
    {
        Assert.Equal(test.Call,call);var expected=test.Expected;
        return wrong switch
        {
            0=>expected with{Outcome="wrong"},1=>expected with{BrowserInvocations=expected.BrowserInvocations+1},
            2=>expected with{OutputCreated=!expected.OutputCreated},3=>expected with{InputsUnchanged=false},
            4=>expected with{SvgFiles=expected.SvgFiles+1},5=>expected with{ArchitectureDiagrams=expected.ArchitectureDiagrams+1},
            6=>expected with{Scope="wrong"},7=>expected with{UiManifestPinRetained=!expected.UiManifestPinRetained},
            8=>expected with{ArchitectureManifestPinRetained=!expected.ArchitectureManifestPinRetained},_=>expected
        };
    }
}
[Trait("Gate","Mock")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class BrowserQaPrerequisiteMockTests
{
    [Theory,MemberData(nameof(BrowserQaPrerequisiteCases.Cases),MemberType=typeof(BrowserQaPrerequisiteCases))]
    public void CorrectGenerationPreflightRepliesPass(string id)
    {var test=BrowserQaPrerequisiteCases.Get(id);BrowserQaPrerequisiteCases.AssertContract(test,new ScriptedBrowserQaPrerequisites(test));}
    [Theory,MemberData(nameof(BrowserQaPrerequisiteCases.Cases),MemberType=typeof(BrowserQaPrerequisiteCases))]
    public void EveryPrerequisiteOutcomeInteractionAndEvidenceMutationRejected(string id)
    {var test=BrowserQaPrerequisiteCases.Get(id);for(var wrong=0;wrong<9;wrong++)Assert.NotNull(Record.Exception(()=>BrowserQaPrerequisiteCases.AssertContract(test,new ScriptedBrowserQaPrerequisites(test,wrong))));}
}
