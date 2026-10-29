using System.Text.Json.Nodes;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources.Java;

[Trait("Gate","Mock"),Trait("Requirement","TEST-BUILD-004")]
public sealed class JavaHelperBodyMockTests
{
    [Theory,MemberData(nameof(JavaHelperBodyCases.ContextCases),MemberType=typeof(JavaHelperBodyCases))]
    public void ExactOriginalContextBodyAcceptsIndependentScriptedState(string id)
    {
        var test=JavaHelperBodyCases.ContextCase(id);var calls=0;
        JavaHelperBodyCases.AssertContext(test,new JavaScriptedContext((sources,reports)=>
        {calls++;Assert.True(JsonNode.DeepEquals(test.Sources,sources));Assert.True(JsonNode.DeepEquals(test.Reports,reports));JavaHelperBodyCases.CopyExpected(test,sources);JavaHelperBodyCases.ThrowExpected(test);}));
        Assert.Equal(1,calls);
    }
    [Theory,MemberData(nameof(JavaHelperBodyCases.ContextCases),MemberType=typeof(JavaHelperBodyCases))]
    public void ContextProjectionErrorsOrderAndRetainedEffectsRejectWrongMocks(string id)
    {
        var test=JavaHelperBodyCases.ContextCase(id);
        if(test.Failure is not null||!JsonNode.DeepEquals(test.Sources,test.ExpectedSources))Reject(new JavaScriptedContext((_,_)=>{}));
        Reject(new JavaScriptedContext((_,_)=>throw new IOException("wrong error")));
        Reject(new JavaScriptedContext((sources,_)=>{JavaHelperBodyCases.CopyExpected(test,sources);sources.Add(new JsonObject());JavaHelperBodyCases.ThrowExpected(test);}));
        Reject(new JavaScriptedContext((sources,reports)=>{JavaHelperBodyCases.CopyExpected(test,sources);reports.Add(new JsonObject());JavaHelperBodyCases.ThrowExpected(test);}));
        foreach(var field in JavaHelperBodyCases.ContextFields)
        {
            var index=test.ExpectedSources.Select((row,index)=>(row,index)).Where(item=>item.row?["java_file_context"] is JsonObject context&&context.ContainsKey(field)).Select(item=>item.index).DefaultIfEmpty(-1).First();
            if(index<0)continue;
            Reject(new JavaScriptedContext((sources,_)=>{JavaHelperBodyCases.CopyExpected(test,sources);sources[index]!["java_file_context"]!.AsObject().Remove(field);JavaHelperBodyCases.ThrowExpected(test);}));
        }
        void Reject(JavaScriptedContext provider)=>Assert.NotNull(Record.Exception(()=>JavaHelperBodyCases.AssertContext(test,provider)));
    }
    [Theory,MemberData(nameof(JavaHelperBodyCases.WhitespaceCases),MemberType=typeof(JavaHelperBodyCases))]
    public void OriginalStripBoundariesPassCorrectAndRejectWrongExistingLinkerMocks(string id)
    {
        var test=JavaHelperBodyCases.WhitespaceCase(id);var correct=new JavaScriptedLinker((type,_)=>{Assert.Equal(test.Type,type);return test.Expected;},test.KnownOwners);
        JvmContractCases.AssertContract(test,correct);Assert.Equal(1,correct.Calls);
        foreach(var wrong in new string?[]{null,"Lwrong;"})Assert.NotNull(Record.Exception(()=>JvmContractCases.AssertContract(test,new JavaScriptedLinker((_,_)=>wrong))));
    }
}
