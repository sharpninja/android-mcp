using System.Text.Json.Nodes;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources.Java;

[Trait("Gate","Mock"),Trait("Requirement","TEST-BUILD-004")]
public sealed class JvmProducerOwnerMappingMockTests
{
    public static IEnumerable<object[]> Cases=>JvmProducerOwnerMappingCases.Cases;
    [Theory,MemberData(nameof(Cases))]
    public void OriginalFalseyOwnerLookupCasesPassCorrectExistingLinkerMocks(string id)
    {
        var test=JvmProducerOwnerMappingCases.Get(id);var provider=new JavaScriptedLinker((type,row)=>
        {Assert.Equal(test.Type,type);if(test.Declaration is not null)Assert.True(JsonNode.DeepEquals(test.Declaration,row));return null;},test.KnownOwners);
        JvmProducerOwnerMappingCases.AssertContract(test,provider);Assert.Equal(1,provider.Calls);
    }
    [Theory,MemberData(nameof(Cases))]
    public void PresentButEmptyOwnerDescriptorsCannotPassAsProvenIdentity(string id)
    {
        var test=JvmProducerOwnerMappingCases.Get(id);
        foreach(var wrong in new[]{"L;","[L;","a/C#go()L;","a/C#go([L;)V"})
            Assert.NotNull(Record.Exception(()=>JvmProducerOwnerMappingCases.AssertContract(test,new JavaScriptedLinker((_,_)=>wrong))));
    }
}
