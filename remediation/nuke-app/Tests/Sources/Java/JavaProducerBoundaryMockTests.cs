using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources.Java;

[Trait("Gate","Mock"),Trait("Requirement","TEST-BUILD-004")]
public sealed class JavaProducerBoundaryMockTests
{
    [Theory,MemberData(nameof(JavaProducerBoundaryCases.Cases),MemberType=typeof(JavaProducerBoundaryCases))]
    public void OriginalBoundaryContractsAcceptIndependentCorrectMocks(string id)
    {
        var test=JavaProducerBoundaryCases.Find(id);var provider=new ScriptedSourceExtractor("java-source",source=>
        {Assert.Equal(test.Input().RelativePath,source.RelativePath);Assert.Equal(test.Input().Context,source.Context);Assert.Equal(test.Input().Bytes.ToArray(),source.Bytes.ToArray());return test.Correct();});
        JavaProducerBoundaryCases.AssertContract(test,provider);Assert.Single(provider.Received);
    }
    [Theory,MemberData(nameof(JavaProducerBoundaryCases.Cases),MemberType=typeof(JavaProducerBoundaryCases))]
    public void EveryRequiredLeafAndIssueDiscriminatesIndependentWrongMocks(string id)
    {
        var test=JavaProducerBoundaryCases.Find(id);var correct=test.Correct();
        foreach(var wrong in JavaProducerBoundaryCases.MissingExpectedLeaves(new JsonArray(test.Rows.Select(row=>(JsonNode)row.DeepClone()).ToArray())))
            Reject(correct with{Records=wrong.AsArray().Select(row=>row!.AsObject()).ToArray()});
        foreach(var wrong in JavaProducerBoundaryCases.MissingExpectedLeaves(test.Metadata))Reject(correct with{Metadata=wrong.AsObject()});
        Reject(correct with{StructuralStatus=correct.StructuralStatus==StructuralParseStatus.Clean?StructuralParseStatus.Malformed:StructuralParseStatus.Clean});
        if(test.Issues.Length>0){Reject(correct with{Issues=[]});Reject(correct with{Issues=[test.Issues[0] with{StartByte=0}]});}
        else Reject(correct with{Issues=[new("unexpected","unexpected",0,0)]});
        void Reject(ExtractionResult wrong)=>Assert.NotNull(Record.Exception(()=>JavaProducerBoundaryCases.AssertContract(test,new ScriptedSourceExtractor("java-source",_=>wrong))));
    }
}
