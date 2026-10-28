using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public static class DocumentationProjectCatalogCases
{
    public static IReadOnlyList<DocumentationProjectInput> Expected()=>Array.AsReadOnly(DocumentationModelSourceCases.Expected().Sources.Take(9).Select((source,index)=>new DocumentationProjectInput((DocumentationProjectInputKind)index,source)).ToArray());
    public static void AssertContract(IDocumentationProjectSourceCatalog provider)
    {
        var actual=provider.DescribeProjectSources();Assert.Equal(Expected(),actual);Assert.Equal(9,actual.Count);
        var mutable=Assert.IsAssignableFrom<IList<DocumentationProjectInput>>(actual);
        Assert.Throws<NotSupportedException>(()=>mutable[0]=mutable[0]);
        Assert.Equal(Expected(),provider.DescribeProjectSources());
    }
    public static IEnumerable<object[]> Fields=>Enumerable.Range(0,9).SelectMany(index=>new[]{"kind","id","role","contract","path","media","hash"}.Select(memberName=>new object[]{index,memberName}));
}
public sealed class ScriptedDocumentationProjectCatalog(IReadOnlyList<DocumentationProjectInput> rows):IDocumentationProjectSourceCatalog
{
    public IReadOnlyList<DocumentationProjectInput> DescribeProjectSources()=>rows;
}
[Trait("Gate","Mock")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class DocumentationProjectSourceCatalogMockTests
{
    [Fact]
    public void CorrectExistingNineDescriptorMetadataMockPasses()=>DocumentationProjectCatalogCases.AssertContract(new ScriptedDocumentationProjectCatalog(DocumentationProjectCatalogCases.Expected()));
    [Theory,MemberData(nameof(DocumentationProjectCatalogCases.Fields),MemberType=typeof(DocumentationProjectCatalogCases))]
    public void EveryTypedKindAndExistingDescriptorFieldRejectsDrift(int index,string memberName)
    {
        var rows=DocumentationProjectCatalogCases.Expected().ToArray();var row=rows[index];var source=row.Source;
        var changed=memberName switch
        {
            "id"=>source with{ArtifactId="wrong"},"role"=>source with{Role=DocumentationInputRole.CanonicalContractOptional},
            "contract"=>source with{ContractId=DocumentationContractId.OpenApi},"path"=>source with{RelativePath="../wrong"},
            "media"=>source with{MediaType="wrong"},"hash"=>source with{ExpectedSha256=new string('1',64)},_=>source
        };
        rows[index]=new(memberName=="kind"?(DocumentationProjectInputKind)(((int)row.Kind+1)%9):row.Kind,changed);
        Assert.NotNull(Record.Exception(()=>DocumentationProjectCatalogCases.AssertContract(new ScriptedDocumentationProjectCatalog(Array.AsReadOnly(rows)))));
    }
    [Theory]
    [InlineData("missing")][InlineData("duplicate")][InlineData("order")][InlineData("mutable")]
    public void EveryMetadataSetOrMutabilityDriftIsRejected(string mutation)
    {
        var rows=DocumentationProjectCatalogCases.Expected().ToArray();
        if(mutation=="missing")rows=rows.Skip(1).ToArray();if(mutation=="duplicate")rows=rows.Append(rows[0]).ToArray();if(mutation=="order")Array.Reverse(rows);
        IReadOnlyList<DocumentationProjectInput> response=mutation=="mutable"?rows:Array.AsReadOnly(rows);
        Assert.NotNull(Record.Exception(()=>DocumentationProjectCatalogCases.AssertContract(new ScriptedDocumentationProjectCatalog(response))));
    }
}
