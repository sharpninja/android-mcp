using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources.Java;

[Trait("Gate","Mock"),Trait("Requirement","TEST-BUILD-004")]
public sealed class JavaProducerCompletionMockTests
{
    [Theory,MemberData(nameof(JavaProducerCompletionCases.Cases),MemberType=typeof(JavaProducerCompletionCases))]
    public void CompleteOriginalRowsAndErrorReportsAcceptIndependentMocks(string id)
    {
        var test=JavaProducerCompletionCases.Find(id);var provider=new ScriptedSourceExtractor("java-source",source=>
        {Assert.Equal(test.Input().Bytes.ToArray(),source.Bytes.ToArray());Assert.Equal(test.Input().RelativePath,source.RelativePath);return test.Correct();});
        JavaProducerCompletionCases.AssertContract(test,provider);Assert.Single(provider.Received);
    }
    [Theory,MemberData(nameof(JavaProducerCompletionCases.Cases),MemberType=typeof(JavaProducerCompletionCases))]
    public void EveryOriginalRowAndReportLeafRejectsOmissionsWrongValuesAndOrder(string id)
    {
        var test=JavaProducerCompletionCases.Find(id);var correct=test.Correct();
        var adapter=new SourceExtractionCase(id,"java-source",test.Text,correct.StructuralStatus,test.Rows,[],"java_source.row/errors/type_decl/child");
        foreach(var values in SourceMutationCases.For(adapter))Reject(correct with{Records=SourceMutationCases.Apply(adapter,(SourceMutation)values[1]).Records});
        foreach(var wrong in JavaProducerBoundaryCases.MissingExpectedLeaves(test.Metadata))Reject(correct with{Metadata=wrong.AsObject()});
        var extra=(JsonObject)test.Metadata.DeepClone();extra["unexpected"]=true;Reject(correct with{Metadata=extra});
        var extraRows=test.Correct().Records;extraRows[0]["extras"]!["unexpected"]=true;Reject(correct with{Records=extraRows});
        if(test.Issues.Length>0){Reject(correct with{Issues=[]});Reject(correct with{Issues=[test.Issues[0] with{StartByte=-1}]});}
        else Reject(correct with{Issues=[new("unexpected","unexpected",0,0)]});
        if(test.Rows.Length>1)Reject(correct with{Records=correct.Records.Reverse().ToArray()});
        if(test.Issues.Length>1)Reject(correct with{Issues=correct.Issues.Reverse().ToArray()});
        void Reject(ExtractionResult wrong)=>Assert.NotNull(Record.Exception(()=>JavaProducerCompletionCases.AssertContract(test,new ScriptedSourceExtractor("java-source",_=>wrong))));
    }
}
