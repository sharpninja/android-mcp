using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public static class DocumentationSelectedProjectSourceCases
{
    public sealed record PinnedInput(DocumentationProjectInputKind Kind,string Path,string Media,string Sha256,int Length);
    public static IReadOnlyList<PinnedInput> AdditionalSourcePins {get;}=Array.AsReadOnly(new[]
    {
        new PinnedInput(DocumentationProjectInputKind.Readme,"README.md","text/markdown","78d47af9952baecff756426fefd52a069e62aca070166eff807f47e8c7795516",4836),
        new PinnedInput(DocumentationProjectInputKind.NativeIdentities,"architecture/native-identities.json","application/json","4c603d499e96d8d64e0936068c2da9b57e89e866f5469f5d16e2d20b5f9a13ae",9590),
        new PinnedInput(DocumentationProjectInputKind.BindingReasons,"contracts/binding-reasons.json","application/json","e04588551e0a91258cbde4a2e654a4976256bd7c56c776dd82a4513de371c0f2",2078),
        new PinnedInput(DocumentationProjectInputKind.ScreenStateResultSchema,"contracts/ScreenStateResult.schema.json","application/json","3ce88eec3a7aff9cb59046ad8f8fbd099d45cb761f2aee2fec398abd21c79a3c",1402),
        new PinnedInput(DocumentationProjectInputKind.Review023Dispositions,"docs/review023-dispositions.md","text/markdown","f15d12ff6e1d42461df3617a70ba009f6a20dd75bf69d22bf64eab5b5971c58e",13979),
        new PinnedInput(DocumentationProjectInputKind.Review024Dispositions,"docs/review024-dispositions.md","text/markdown","b0bc0b1aeefe48d352a4b08d3084ea8d031450c00f0245569401fe3a202aad8c",7507)
    });
    public static IReadOnlyList<DocumentationProjectInput> Expected(DocumentationProjectSourceScope scope)
    {
        var original=DocumentationProjectCatalogCases.Expected();
        return scope switch
        {
            DocumentationProjectSourceScope.Model=>original,
            DocumentationProjectSourceScope.GeneratedDesign=>Array.AsReadOnly(original.Concat(AdditionalSourcePins.Select(pin=>new DocumentationProjectInput(pin.Kind,
                new("project:"+pin.Path,DocumentationInputRole.ProjectRequired,null,pin.Path,pin.Media,null)))).ToArray()),
            _=>throw new ArgumentOutOfRangeException(nameof(scope))
        };
    }
    public static IEnumerable<object[]> Scopes=>new[]{DocumentationProjectSourceScope.Model,DocumentationProjectSourceScope.GeneratedDesign}.Select(scope=>new object[]{scope});
    public static IEnumerable<object[]> Fields=>Enumerable.Range(9,6).SelectMany(index=>new[]{"kind","id","role","contract","path","media","hash"}.Select(memberName=>new object[]{index,memberName}));
    public static IEnumerable<object[]> PlanFields=>Fields.Where(row=>(string)row[1]!="kind");
    public static void AssertCatalog(DocumentationProjectSourceScope scope,IDocumentationSelectedProjectSourceCatalog provider)
    {
        var actual=provider.DescribeProjectSources(scope);Assert.Equal(Expected(scope),actual);Assert.Equal(scope==DocumentationProjectSourceScope.Model?9:15,actual.Count);
        var mutable=Assert.IsAssignableFrom<IList<DocumentationProjectInput>>(actual);Assert.Throws<NotSupportedException>(()=>mutable[0]=mutable[0]);
        Assert.Equal(DocumentationProjectCatalogCases.Expected(),provider.DescribeProjectSources());
        Assert.Equal(Expected(scope),provider.DescribeProjectSources(scope));
    }
    public static DocumentationModelSourcePlan ExpectedPlan(DocumentationProjectSourceScope scope)=>new(
        Expected(scope).Select(input=>input.Source).Concat(DocumentationModelSourceCases.Expected().Sources.Skip(9)).ToArray());
    public static IEnumerable<object[]> PlanCases=>Scopes.SelectMany(scope=>DocumentationModelSourceCases.Cases.Select(row=>new object[]{scope[0],row[0]}));
    public static void AssertPlan(DocumentationProjectSourceScope scope,DocumentationSourcePlanCase test,IDocumentationModelSourcePlanner provider)
    {
        var before=test.Pins.Bytes.ToArray();
        if(test.Error is not null)Assert.Equal(test.Error,Assert.Throws<DocumentationModelSourceException>(()=>provider.Plan(test.Pins)).Code);
        else
        {
            var actual=provider.Plan(test.Pins);Assert.Equal(ExpectedPlan(scope).Sources,actual.Sources);
            Assert.Equal(scope==DocumentationProjectSourceScope.Model?12:18,actual.Sources.Count(source=>source.Role!=DocumentationInputRole.CanonicalContractOptional));
            Assert.Equal(3,actual.Sources.Count(source=>source.Role==DocumentationInputRole.CanonicalContractOptional));
        }
        Assert.Equal(before,test.Pins.Bytes.ToArray());
    }
}
public sealed class ScriptedSelectedProjectSourceCatalog(IReadOnlyList<DocumentationProjectInput> scoped,IReadOnlyList<DocumentationProjectInput>? original=null,bool admitUnknown=false):IDocumentationSelectedProjectSourceCatalog
{
    public IReadOnlyList<DocumentationProjectInput> DescribeProjectSources()=>original??DocumentationProjectCatalogCases.Expected();
    public IReadOnlyList<DocumentationProjectInput> DescribeProjectSources(DocumentationProjectSourceScope scope)
        =>Enum.IsDefined(scope)||admitUnknown?scoped:throw new ArgumentOutOfRangeException(nameof(scope));
}
[Trait("Gate","Mock")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class DocumentationSelectedProjectSourceMockTests
{
    [Theory,MemberData(nameof(DocumentationSelectedProjectSourceCases.Scopes),MemberType=typeof(DocumentationSelectedProjectSourceCases))]
    public void CorrectSelectedSourceAndUnchangedDefaultMocksPass(DocumentationProjectSourceScope scope)
        =>DocumentationSelectedProjectSourceCases.AssertCatalog(scope,new ScriptedSelectedProjectSourceCatalog(DocumentationSelectedProjectSourceCases.Expected(scope)));
    [Theory,MemberData(nameof(DocumentationSelectedProjectSourceCases.Scopes),MemberType=typeof(DocumentationSelectedProjectSourceCases))]
    public void EveryWrongScopeOutcomeIsRejected(DocumentationProjectSourceScope scope)
    {var opposite=scope==DocumentationProjectSourceScope.Model?DocumentationProjectSourceScope.GeneratedDesign:DocumentationProjectSourceScope.Model;Assert.NotNull(Record.Exception(()=>DocumentationSelectedProjectSourceCases.AssertCatalog(scope,new ScriptedSelectedProjectSourceCatalog(DocumentationSelectedProjectSourceCases.Expected(opposite)))));}
    [Theory,MemberData(nameof(DocumentationSelectedProjectSourceCases.Fields),MemberType=typeof(DocumentationSelectedProjectSourceCases))]
    public void EachAdditionalTypedDescriptorFieldRejectsDrift(int index,string memberName)
    {
        var rows=DocumentationSelectedProjectSourceCases.Expected(DocumentationProjectSourceScope.GeneratedDesign).ToArray();var row=rows[index];var source=row.Source;
        var changed=memberName switch
        {
            "id"=>source with{ArtifactId="unknown"},"role"=>source with{Role=DocumentationInputRole.CanonicalContractOptional},"contract"=>source with{ContractId=DocumentationContractId.OpenApi},
            "path"=>source with{RelativePath="../outside"},"media"=>source with{MediaType="wrong"},"hash"=>source with{ExpectedSha256=new string('1',64)},_=>source
        };
        rows[index]=new(memberName=="kind"?DocumentationProjectInputKind.WorkspaceDsl:row.Kind,changed);
        Assert.NotNull(Record.Exception(()=>DocumentationSelectedProjectSourceCases.AssertCatalog(DocumentationProjectSourceScope.GeneratedDesign,new ScriptedSelectedProjectSourceCatalog(Array.AsReadOnly(rows)))));
    }
    [Theory]
    [InlineData(9)][InlineData(10)][InlineData(11)][InlineData(12)][InlineData(13)][InlineData(14)]
    public void EveryNeededLinkedSourceCannotBeDropped(int index)
    {var rows=DocumentationSelectedProjectSourceCases.Expected(DocumentationProjectSourceScope.GeneratedDesign).Where((_,position)=>position!=index).ToArray();Assert.NotNull(Record.Exception(()=>DocumentationSelectedProjectSourceCases.AssertCatalog(DocumentationProjectSourceScope.GeneratedDesign,new ScriptedSelectedProjectSourceCatalog(Array.AsReadOnly(rows)))));}
    [Theory]
    [InlineData("duplicate")][InlineData("order")][InlineData("mutable")]
    public void DescriptorSetOrderAndReadOnlyContractCannotDrift(string mutation)
    {
        var rows=DocumentationSelectedProjectSourceCases.Expected(DocumentationProjectSourceScope.GeneratedDesign).ToArray();if(mutation=="duplicate")rows=rows.Append(rows[0]).ToArray();if(mutation=="order")Array.Reverse(rows);
        Assert.NotNull(Record.Exception(()=>DocumentationSelectedProjectSourceCases.AssertCatalog(DocumentationProjectSourceScope.GeneratedDesign,new ScriptedSelectedProjectSourceCatalog(mutation=="mutable"?rows:Array.AsReadOnly(rows)))));
    }
    [Fact]
    public void GeneratedScopeMustNotBroadenOriginalModelDefault()
    {var rows=DocumentationSelectedProjectSourceCases.Expected(DocumentationProjectSourceScope.GeneratedDesign);Assert.NotNull(Record.Exception(()=>DocumentationSelectedProjectSourceCases.AssertCatalog(DocumentationProjectSourceScope.GeneratedDesign,new ScriptedSelectedProjectSourceCatalog(rows,rows))));}
    [Fact]
    public void UnknownScopeFailsWithoutMetadataFallback()=>Assert.Throws<ArgumentOutOfRangeException>(()=>new ScriptedSelectedProjectSourceCatalog([]).DescribeProjectSources((DocumentationProjectSourceScope)999));
    [Fact]
    public void UnknownScopeFallbackIsIndividuallyRejected()=>Assert.NotNull(Record.Exception(()=>Assert.Throws<ArgumentOutOfRangeException>(()=>new ScriptedSelectedProjectSourceCatalog([],admitUnknown:true).DescribeProjectSources((DocumentationProjectSourceScope)999))));
    [Fact]
    public void EachAdditionalFullCorpusSourceIsPinnedActualData()
    {
        foreach(var pin in DocumentationSelectedProjectSourceCases.AdditionalSourcePins)
        {var bytes=File.ReadAllBytes(Path.Combine(DocumentationFixture.Current.Workspace,"remediation/design-c4",pin.Path));Assert.Equal(pin.Length,bytes.Length);Assert.Equal(pin.Sha256,DocumentationFixture.Hash(bytes));}
    }
    [Theory,MemberData(nameof(DocumentationSelectedProjectSourceCases.PlanCases),MemberType=typeof(DocumentationSelectedProjectSourceCases))]
    public void CorrectSelectedPlanReusesAllOriginalPinRejectionCases(DocumentationProjectSourceScope scope,DocumentationSourcePlanCase test)
        =>DocumentationSelectedProjectSourceCases.AssertPlan(scope,test,new ScriptedDocumentationModelSourcePlanner(DocumentationSelectedProjectSourceCases.ExpectedPlan(scope),test.Error));
    [Theory,MemberData(nameof(DocumentationSelectedProjectSourceCases.PlanCases),MemberType=typeof(DocumentationSelectedProjectSourceCases))]
    public void EveryWrongSelectedPlanOutcomeIsRejected(DocumentationProjectSourceScope scope,DocumentationSourcePlanCase test)
        =>Assert.NotNull(Record.Exception(()=>DocumentationSelectedProjectSourceCases.AssertPlan(scope,test,new ScriptedDocumentationModelSourcePlanner(DocumentationSelectedProjectSourceCases.ExpectedPlan(scope),test.Error is null?"MODEL_SOURCE_PINS":null))));
    [Theory,MemberData(nameof(DocumentationSelectedProjectSourceCases.PlanFields),MemberType=typeof(DocumentationSelectedProjectSourceCases))]
    public void EveryAdditionalPlanFieldHasItsOwnIncorrectControl(int index,string memberName)
    {
        var scope=DocumentationProjectSourceScope.GeneratedDesign;var expected=DocumentationSelectedProjectSourceCases.ExpectedPlan(scope);var rows=expected.Sources.ToArray();var source=rows[index];
        rows[index]=memberName switch
        {
            "id"=>source with{ArtifactId="unknown"},"role"=>source with{Role=DocumentationInputRole.CanonicalContractOptional},"contract"=>source with{ContractId=DocumentationContractId.OpenApi},
            "path"=>source with{RelativePath="../outside"},"media"=>source with{MediaType="wrong"},"hash"=>source with{ExpectedSha256=new string('1',64)},_=>throw new InvalidOperationException()
        };
        var test=new DocumentationSourcePlanCase("SELECTED-FIELD",DocumentationModelSourceCases.Original(),expected);
        Assert.NotNull(Record.Exception(()=>DocumentationSelectedProjectSourceCases.AssertPlan(scope,test,new ScriptedDocumentationModelSourcePlanner(new(rows)))));
    }
}
