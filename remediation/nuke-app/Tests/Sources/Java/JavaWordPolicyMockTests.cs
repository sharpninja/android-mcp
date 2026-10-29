using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources.Java;

[Trait("Gate","Mock"),Trait("Requirement","TEST-BUILD-004")]
public sealed class JavaWordPolicyMockTests
{
    [Theory,MemberData(nameof(JavaWordPolicyCases.Cases),MemberType=typeof(JavaWordPolicyCases))]
    public void OriginalUnicodeWordPolicyAcceptsIndependentMocks(string id)
    {
        var test=JavaWordPolicyCases.Find(id);var provider=new ScriptedSourceExtractor("java-source",source=>
        {Assert.Equal(test.Input().Bytes.ToArray(),source.Bytes.ToArray());return test.Correct();});
        JavaWordPolicyCases.AssertContract(test,provider);Assert.Single(provider.Received);
    }
    [Theory,MemberData(nameof(JavaWordPolicyCases.Cases),MemberType=typeof(JavaWordPolicyCases))]
    public void EveryOriginalUnicodeWordPolicyLeafRejectsIndependentMutation(string id)
    {
        var test=JavaWordPolicyCases.Find(id);
        var adapter=new SourceExtractionCase(id,"java-source",test.Text,StructuralParseStatus.Clean,test.Rows,[],"java_source.qualify_anns/type_text");
        foreach(var values in SourceMutationCases.For(adapter))
        {
            var wrong=SourceMutationCases.Apply(adapter,(SourceMutation)values[1]);
            Assert.NotNull(Record.Exception(()=>JavaWordPolicyCases.AssertContract(test,new ScriptedSourceExtractor("java-source",_=>test.Correct() with{Records=wrong.Records}))));
        }
    }
}
