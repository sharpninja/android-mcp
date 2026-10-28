using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Documentation.Validation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public static class DocumentationDiagnosticCases
{
    public static IEnumerable<object[]> Cases=>new[]{DocumentationValidationPhase.BeforeManifest,DocumentationValidationPhase.AfterManifest}
        .Select(phase=>new object[]{phase});
    public static GeneratedDesignValidationResult Input(DocumentationValidationPhase phase)=>new(phase,new(4,5,6,7),
        ["docs/a.svg"],["docs/b.svg"],[new("DOCS_LINK_MISSING","README.md","missing target")],[new("docs/b.svg",2,7,"invalid <xml>")]);
    private static JsonObject Expected(DocumentationValidationPhase phase)=>new()
    {
        ["Phase"]=(int)phase,["Counters"]=new JsonObject{["ChecksumsChecked"]=4,["MarkdownLinksChecked"]=5,["RenderedSvgCount"]=6,["EditableUiCopies"]=7},
        ["ExpectedSvgPaths"]=new JsonArray("docs/a.svg"),["ActualSvgPaths"]=new JsonArray("docs/b.svg"),
        ["Issues"]=new JsonArray(new JsonObject{["Code"]="DOCS_LINK_MISSING",["ArtifactPath"]="README.md",["Subject"]="missing target"}),
        ["ParseDiagnostics"]=new JsonArray(new JsonObject{["ArtifactPath"]="docs/b.svg",["Line"]=2,["Column"]=7,["Message"]="invalid <xml>"}),
        ["Scope"]="generated-design",["IsValid"]=false
    };
    public static void AssertDiagnostic(DocumentationValidationPhase phase,Func<GeneratedDesignValidationResult,string> provider)
    {
        var input=Input(phase);var before=JsonSerializer.Serialize(input);var text=provider(input);
        Assert.Contains("DESIGN_DOCUMENTATION_INVALID: "+phase,text,StringComparison.Ordinal);
        var marker=Assert.Single(text.Split('\n'),line=>line.StartsWith("validationResult=",StringComparison.Ordinal));
        var json=JsonNode.Parse(marker["validationResult=".Length..]);
        Assert.True(JsonNode.DeepEquals(Expected(phase),json));
        Assert.Equal(before,JsonSerializer.Serialize(input));
    }
    public static string Reply(DocumentationValidationPhase phase,string mutation="")
    {
        var value=Expected(phase);
        if(mutation=="phase")value["Phase"]=(int)(phase==DocumentationValidationPhase.BeforeManifest?DocumentationValidationPhase.AfterManifest:DocumentationValidationPhase.BeforeManifest);
        if(mutation=="counter")value["Counters"]!["ChecksumsChecked"]=99;
        if(mutation=="expected")value["ExpectedSvgPaths"]!.AsArray().Clear();
        if(mutation=="actual")value["ActualSvgPaths"]!.AsArray().Clear();
        if(mutation=="issue-code")value["Issues"]![0]!["Code"]="wrong";
        if(mutation=="issue-path")value["Issues"]![0]!["ArtifactPath"]="wrong";
        if(mutation=="issue-message")value["Issues"]![0]!["Subject"]="wrong";
        if(mutation=="parse-path")value["ParseDiagnostics"]![0]!["ArtifactPath"]="wrong";
        if(mutation=="parse-line")value["ParseDiagnostics"]![0]!["Line"]=0;
        if(mutation=="parse-column")value["ParseDiagnostics"]![0]!["Column"]=0;
        if(mutation=="parse-message")value["ParseDiagnostics"]![0]!["Message"]="wrong";
        if(mutation=="scope")value["Scope"]="wrong";
        if(mutation=="validity")value["IsValid"]=true;
        var header="IOException: DESIGN_DOCUMENTATION_INVALID: "+phase;
        if(mutation=="omit")return header;
        var line="validationResult="+value.ToJsonString();
        return header+"\n"+line+(mutation=="duplicate"?"\n"+line:"");
    }
}

[Trait("Gate","Mock")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class DocumentationDiagnosticMockTests
{
    [Theory,MemberData(nameof(DocumentationDiagnosticCases.Cases),MemberType=typeof(DocumentationDiagnosticCases))]
    public void CorrectCompleteDiagnosticsPass(DocumentationValidationPhase phase)
        =>DocumentationDiagnosticCases.AssertDiagnostic(phase,_=>DocumentationDiagnosticCases.Reply(phase));
    [Theory]
    [InlineData("phase")][InlineData("counter")][InlineData("expected")][InlineData("actual")]
    [InlineData("issue-code")][InlineData("issue-path")][InlineData("issue-message")]
    [InlineData("parse-path")][InlineData("parse-line")][InlineData("parse-column")][InlineData("parse-message")]
    [InlineData("scope")][InlineData("validity")][InlineData("omit")][InlineData("duplicate")]
    public void EveryOmissionAndChangedDiagnosticIsRejected(string mutation)
        =>Assert.NotNull(Record.Exception(()=>DocumentationDiagnosticCases.AssertDiagnostic(DocumentationValidationPhase.BeforeManifest,_=>DocumentationDiagnosticCases.Reply(DocumentationValidationPhase.BeforeManifest,mutation))));
}
