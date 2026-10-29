using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources.Java;

[Trait("Gate","Mock"),Trait("Requirement","TEST-BUILD-004")]
public sealed class JavaAnnotationOffsetMockTests
{
    [Theory,MemberData(nameof(JavaAnnotationOffsetCases.Cases),MemberType=typeof(JavaAnnotationOffsetCases))]
    public void ExactOriginalCodePointOffsetsAcceptCorrectMocks(string id)
    {
        var test=JavaAnnotationOffsetCases.Find(id);var provider=new ScriptedSourceExtractor("java-source",source=>
        {Assert.Equal(test.Input().Bytes.ToArray(),source.Bytes.ToArray());Assert.Equal(test.Input().RelativePath,source.RelativePath);return test.Correct();});
        JavaAnnotationOffsetCases.AssertContract(test,provider);Assert.Single(provider.Received);
    }
    [Theory,MemberData(nameof(JavaAnnotationOffsetCases.Cases),MemberType=typeof(JavaAnnotationOffsetCases))]
    public void AnnotationSourceMetadataRejectsIndependentWrongAndOmittedValues(string id)
    {
        var test=JavaAnnotationOffsetCases.Find(id);var adapter=new SourceExtractionCase(id,"java-source",test.Text,StructuralParseStatus.Clean,test.Rows,[],"java_source.qualify_anns");
        foreach(var mutation in SourceMutationCases.For(adapter))
        {
            var wrong=SourceMutationCases.Apply(adapter,(SourceMutation)mutation[1]);
            Assert.NotNull(Record.Exception(()=>JavaAnnotationOffsetCases.AssertContract(test,new ScriptedSourceExtractor("java-source",_=>test.Correct() with{Records=wrong.Records}))));
        }
        foreach(var offset in new[]{0,16})
        {
            var wrong=test.Correct();wrong.Records[1]["annotations"]![0]!["start"]=offset;
            Assert.NotNull(Record.Exception(()=>JavaAnnotationOffsetCases.AssertContract(test,new ScriptedSourceExtractor("java-source",_=>wrong))));
        }
    }
}
