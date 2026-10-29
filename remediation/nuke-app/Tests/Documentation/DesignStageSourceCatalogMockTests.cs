using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public sealed record DesignStageSourceCase(string Id,DesignStagePlan Selection,
    IReadOnlyList<DocumentationProjectInputKind> ExpectedKinds,bool Reject=false)
{public override string ToString()=>Id;}
public static class DesignStageSourceCases
{
    private static readonly DocumentationProjectInputKind[] Tools=[DocumentationProjectInputKind.ToolchainLock,DocumentationProjectInputKind.RendererLock];
    private static readonly DocumentationProjectInputKind[] Code=[DocumentationProjectInputKind.WorkspaceDsl,DocumentationProjectInputKind.CodeMarkdown];
    private static readonly DocumentationProjectInputKind[] Module=[DocumentationProjectInputKind.ModuleContract,DocumentationProjectInputKind.CodeMarkdown];
    private static readonly DocumentationProjectInputKind[] Ui=[DocumentationProjectInputKind.BlueprintMarkdown,DocumentationProjectInputKind.DesignMarkdown];
    // Independent finite acceptance table; production derives requirements from
    // the admitted Core stage union, never from a second alias table.
    public static IEnumerable<object[]> Cases
    {
        get
        {
            var model=DocumentationProjectCatalogCases.Expected().Select(input=>input.Kind).ToArray();
            var generated=DocumentationSelectedProjectSourceCases.Expected(DocumentationProjectSourceScope.GeneratedDesign).Select(input=>input.Kind).ToArray();
            var expected=new (string Alias,DocumentationProjectInputKind[] Kinds)[]{
                ("verifyToolchain",Tools),("validateModuleGraph",Tools.Concat(Module).ToArray()),("validateModelContracts",model),
                ("extractCodeViews",Code),("renderCodeViews",Tools.Concat(Code).ToArray()),("validateArchitecture",model),
                ("exportArchitecture",model),("generateArchitectureDiagrams",model),("generateUiAssets",Tools.Concat(Ui).ToArray()),
                ("renderStoryboards",Tools.Concat(Ui).ToArray()),("verifyDesignDocs",generated),("generateArtifactManifest",generated),("packageDesignDocs",generated)};
            var planner=new DesignStagePlanner();Assert.Equal(expected.Select(row=>row.Alias).Order(StringComparer.Ordinal),planner.Describe().Metadata.Aliases.Select(alias=>alias.Name).Order(StringComparer.Ordinal));
            DesignStageSourceCase Case(string id,string[] aliases,IReadOnlyList<DocumentationProjectInputKind> kinds)=>new(id,planner.Select(new(aliases,planner.Describe().Sha256)),kinds);
            var result=expected.Select(row=>Case(row.Alias,[row.Alias],row.Kinds)).ToList();
            result.Add(Case("UNION-CODE-MODULE",["extractCodeViews","validateModuleGraph"],Tools.Concat(Code).Concat(Module).Distinct().ToArray()));
            result.Add(Case("UNION-CODE-UI",["extractCodeViews","generateUiAssets"],Tools.Concat(Code).Concat(Ui).Distinct().ToArray()));
            result.Add(Case("UNION-EXPORT-PACKAGE",["exportArchitecture","packageDesignDocs"],generated));
            result.Add(Case("UNION-DUPLICATE",["extractCodeViews","extractCodeViews"],Code));
            var original=result[0];
            result.Add(original with{Id="INVALID-ERRORS",Selection=original.Selection with{Errors=[new("UNKNOWN_ALIAS","wrong")]},Reject=true});
            result.Add(original with{Id="INVALID-REGISTRY",Selection=original.Selection with{RegistrySha256=new string('f',64)},Reject=true});
            result.Add(original with{Id="INVALID-STAGE-MISSING",Selection=original.Selection with{Stages=[]},Reject=true});
            result.Add(original with{Id="INVALID-STAGE-EXTRA",Selection=original.Selection with{Stages=original.Selection.Stages.Append(DesignStage.Package).ToArray()},Reject=true});
            result.Add(original with{Id="INVALID-ALIAS",Selection=original.Selection with{Aliases=["unknown"]},Reject=true});
            var multiple=result.Single(row=>row.Id=="UNION-CODE-UI");
            result.Add(multiple with{Id="INVALID-CANONICAL-ALIAS-ORDER",Selection=multiple.Selection with{Aliases=multiple.Selection.Aliases.Reverse().ToArray()},Reject=true});
            return result.Select(test=>new object[]{test});
        }
    }
    public static IReadOnlyList<DocumentationProjectInput> Expected(DesignStageSourceCase test)=>Array.AsReadOnly(
        DocumentationSelectedProjectSourceCases.Expected(DocumentationProjectSourceScope.GeneratedDesign).Where(input=>test.ExpectedKinds.Contains(input.Kind)).ToArray());
    public static void AssertSelection(DesignStageSourceCase test,Func<DesignStagePlan,IReadOnlyList<DocumentationProjectInput>> provider)
    {
        var beforeStages=test.Selection.Stages.ToArray();var beforeAliases=test.Selection.Aliases.ToArray();
        if(test.Reject)Assert.Equal("DESIGN_STAGE_SELECTION",Assert.Throws<ArchitectureAssetException>(()=>provider(test.Selection)).Code);
        else
        {
            var expected=Expected(test);var actual=provider(test.Selection);Assert.Equal(expected,actual);
            var mutable=Assert.IsAssignableFrom<IList<DocumentationProjectInput>>(actual);Assert.Throws<NotSupportedException>(()=>mutable[0]=mutable[0]);
            Assert.Equal(expected,provider(test.Selection));
        }
        Assert.Equal(beforeStages,test.Selection.Stages);Assert.Equal(beforeAliases,test.Selection.Aliases);
    }
    public static IReadOnlyList<DocumentationProjectInput> Reply(DesignStageSourceCase test,string mutation="")
    {
        if(test.Reject){if(mutation=="outcome")return Expected(test);throw new ArchitectureAssetException("DESIGN_STAGE_SELECTION","fixture");}
        if(mutation=="outcome")throw new ArchitectureAssetException("WRONG","fixture");
        var rows=Expected(test).ToArray();
        if(mutation=="omit")rows=rows.Skip(1).ToArray();
        if(mutation=="extra")rows=rows.Append(new(DocumentationProjectInputKind.Readme,new("project:extra",DocumentationInputRole.ProjectRequired,null,"extra.md","text/markdown",null))).ToArray();
        if(mutation=="order")Array.Reverse(rows);
        if(mutation=="kind")rows[0]=rows[0] with{Kind=(DocumentationProjectInputKind)999};
        if(mutation=="id")rows[0]=rows[0] with{Source=rows[0].Source with{ArtifactId="wrong"}};
        if(mutation=="role")rows[0]=rows[0] with{Source=rows[0].Source with{Role=DocumentationInputRole.CanonicalContractOptional}};
        if(mutation=="contract")rows[0]=rows[0] with{Source=rows[0].Source with{ContractId=DocumentationContractId.OpenApi}};
        if(mutation=="path")rows[0]=rows[0] with{Source=rows[0].Source with{RelativePath="../outside"}};
        if(mutation=="media")rows[0]=rows[0] with{Source=rows[0].Source with{MediaType="wrong"}};
        if(mutation=="hash")rows[0]=rows[0] with{Source=rows[0].Source with{ExpectedSha256="wrong"}};
        return Array.AsReadOnly(rows);
    }
    public static IEnumerable<object[]> ValidCases=>Cases.Where(row=>!((DesignStageSourceCase)row[0]).Reject);
}
[Trait("Gate","Mock")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class DesignStageSourceCatalogMockTests
{
    [Theory,MemberData(nameof(DesignStageSourceCases.Cases),MemberType=typeof(DesignStageSourceCases))]
    public void CorrectExactThirteenAliasAndUnionScopesPass(DesignStageSourceCase test)=>DesignStageSourceCases.AssertSelection(test,_=>DesignStageSourceCases.Reply(test));
    [Theory,MemberData(nameof(DesignStageSourceCases.Cases),MemberType=typeof(DesignStageSourceCases))]
    public void EveryIncorrectAdmissionOutcomeIsRejected(DesignStageSourceCase test)=>Assert.NotNull(Record.Exception(()=>DesignStageSourceCases.AssertSelection(test,_=>DesignStageSourceCases.Reply(test,"outcome"))));
    [Theory,MemberData(nameof(DesignStageSourceCases.ValidCases),MemberType=typeof(DesignStageSourceCases))]
    public void EachOmittedRequiredInputIsRejected(DesignStageSourceCase test)=>Assert.NotNull(Record.Exception(()=>DesignStageSourceCases.AssertSelection(test,_=>DesignStageSourceCases.Reply(test,"omit"))));
    [Theory,MemberData(nameof(DesignStageSourceCases.ValidCases),MemberType=typeof(DesignStageSourceCases))]
    public void EachBroadenedScopeIsRejected(DesignStageSourceCase test)=>Assert.NotNull(Record.Exception(()=>DesignStageSourceCases.AssertSelection(test,_=>DesignStageSourceCases.Reply(test,"extra"))));
    [Theory,MemberData(nameof(DesignStageSourceCases.ValidCases),MemberType=typeof(DesignStageSourceCases))]
    public void EachNoncanonicalInputOrderIsRejected(DesignStageSourceCase test)=>Assert.NotNull(Record.Exception(()=>DesignStageSourceCases.AssertSelection(test,_=>DesignStageSourceCases.Reply(test,"order"))));
    [Theory]
    [InlineData("kind")][InlineData("id")][InlineData("role")][InlineData("contract")][InlineData("path")][InlineData("media")][InlineData("hash")]
    public void ExistingDescriptorFieldsCannotBeRewrittenByStageSelection(string mutation)
    {var test=(DesignStageSourceCase)DesignStageSourceCases.ValidCases.First()[0];Assert.NotNull(Record.Exception(()=>DesignStageSourceCases.AssertSelection(test,_=>DesignStageSourceCases.Reply(test,mutation))));}
}
