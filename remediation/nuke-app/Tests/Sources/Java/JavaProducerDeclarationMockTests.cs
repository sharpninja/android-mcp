using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources.Java;

[Trait("Gate","Mock"),Trait("Requirement","TEST-BUILD-004")]
public sealed class JavaProducerDeclarationMockTests
{
    public static IEnumerable<object[]> Cases=>JavaProducerDeclarationCases.Cases;
    [Theory,MemberData(nameof(Cases))]
    public void OriginalProducerDeclarationClausesPassCorrectMocks(string id)
    {
        var test=JavaProducerDeclarationCases.Find(id);
        var provider=new ScriptedSourceExtractor("java-source",source=>
        {Assert.Equal(test.Input().RelativePath,source.RelativePath);Assert.Equal(test.Input().Context,source.Context);return test.Correct();});
        JavaProducerDeclarationCases.AssertContract(test,provider);Assert.Single(provider.Received);
    }
    [Theory,MemberData(nameof(Cases))]
    public void ComponentConstructorAndContainingTypeMetadataRejectOmittedAndWrongValues(string id)
    {
        var test=JavaProducerDeclarationCases.Find(id);
        var adapter=new SourceExtractionCase(id,"java-source",test.Text,StructuralParseStatus.Clean,test.Rows,[],"java_source.py producer type_decl/row/child");
        foreach(var values in SourceMutationCases.For(adapter))
        {
            var wrong=SourceMutationCases.Apply(adapter,(SourceMutation)values[1]);
            Assert.NotNull(Record.Exception(()=>JavaProducerDeclarationCases.AssertContract(test,
                new ScriptedSourceExtractor("java-source",_=>test.Correct() with{Records=wrong.Records}))));
        }
        foreach(var rows in new[]{test.Correct().Records.Skip(1).ToArray(),test.Correct().Records.Reverse().ToArray()})
            Assert.NotNull(Record.Exception(()=>JavaProducerDeclarationCases.AssertContract(test,new ScriptedSourceExtractor("java-source",_=>test.Correct() with{Records=rows}))));
    }
}
