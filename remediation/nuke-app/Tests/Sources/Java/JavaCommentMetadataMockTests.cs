using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources.Java;

[Trait("Gate","Mock"),Trait("Requirement","TEST-BUILD-004")]
public sealed class JavaCommentMetadataMockTests
{
    [Theory,MemberData(nameof(JavaCommentMetadataCases.Cases),MemberType=typeof(JavaCommentMetadataCases))]
    public void OriginalCommentScalarMetadataAcceptsIndependentMocks(string id)
    {
        var test=JavaCommentMetadataCases.Find(id);
        var provider=new ScriptedSourceExtractor("java-source",source=>
        {Assert.Equal(test.Input().Bytes.ToArray(),source.Bytes.ToArray());return test.Correct();});
        JavaCommentMetadataCases.AssertContract(test,provider);Assert.Single(provider.Received);
    }
    [Theory,MemberData(nameof(JavaCommentMetadataCases.Cases),MemberType=typeof(JavaCommentMetadataCases))]
    public void EveryAnnotationLeafRejectsIndependentMutation(string id)
    {
        var test=JavaCommentMetadataCases.Find(id);
        var adapter=new SourceExtractionCase(id,"java-source",test.Text,StructuralParseStatus.Clean,test.Rows,[],"java_source.mask_comments/qualify_anns");
        foreach(var values in SourceMutationCases.For(adapter))
        {
            var wrong=SourceMutationCases.Apply(adapter,(SourceMutation)values[1]);
            Assert.NotNull(Record.Exception(()=>JavaCommentMetadataCases.AssertContract(test,new ScriptedSourceExtractor("java-source",_=>test.Correct() with{Records=wrong.Records}))));
        }
    }
}
