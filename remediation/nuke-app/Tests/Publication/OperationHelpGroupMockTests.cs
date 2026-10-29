using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Publication;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Publication;
public sealed record HelpGroupCase(string Id,IReadOnlyList<IReadOnlyList<string>>? Groups)
{public override string ToString()=>Id;}
public static class OperationHelpGroupCases
{
    public static IEnumerable<object[]> Cases=>new[]{new HelpGroupCase("old-null",null),new HelpGroupCase("explicit-empty",[]),new HelpGroupCase("one-declared-group",[new[]{"a","b","c"}]),new HelpGroupCase("two-groups",[new[]{"b","a"},new[]{"d","c"}])}.Select(c=>new object[]{c});
    public static OperationHelpRequest Request(HelpGroupCase c)
    {
        var input=OperationHelpContractCases.Request();return input with{Definitions=[input.Definitions[0] with{OptionalParameters=["a","b","c","d"],AllOrNoneParameterGroups=c.Groups},input.Definitions[1]]};
    }
    public static OperationHelpResult Correct(HelpGroupCase c,string? mutation=null)
    {
        var result=OperationHelpContractCases.Correct(new(c.Id,Request(c),null));var root=JsonNode.Parse(result.Document!.Bytes.Span)!.AsObject();var operation=root["operations"]!.AsArray().Single(p=>p!["operation"]!.GetValue<string>()=="Stage")!.AsObject();
        var groups=c.Groups??[];if(groups.Count>0)operation["all_or_none"]=JsonSerializer.SerializeToNode(groups);
        var markdown=Encoding.UTF8.GetString(result.Markdown!.Bytes.Span);foreach(var group in groups)markdown+="Supply together: "+string.Join(", ",group.Select(p=>"--"+p))+".\n";
        if(mutation=="presence"){if(groups.Count>0)operation.Remove("all_or_none");else operation["all_or_none"]=new JsonArray();}
        if(mutation=="members")operation["all_or_none"]=new JsonArray(new JsonArray("wrong"));
        if(mutation=="markdown")markdown=groups.Count==0?markdown+"Supply together: wrong":markdown.Replace("Supply together:","Wrong:",StringComparison.Ordinal);
        return result with{Document=result.Document with{Bytes=JsonSerializer.SerializeToUtf8Bytes(root)},Markdown=result.Markdown with{Bytes=Encoding.UTF8.GetBytes(markdown)}};
    }
    public static void AssertCase(HelpGroupCase c,IOperationHelpBuilder provider)
    {
        var request=Request(c);var before=JsonSerializer.SerializeToUtf8Bytes(request.Definitions);var result=provider.Build(request);Assert.Equal(before,JsonSerializer.SerializeToUtf8Bytes(request.Definitions));Assert.True(result.IsValid);Assert.Empty(result.Issues);
        using var document=JsonDocument.Parse(result.Document!.Bytes);var operations=document.RootElement.GetProperty("operations").EnumerateArray().ToArray();var stage=operations.Single(p=>p.GetProperty("operation").GetString()=="Stage");var check=operations.Single(p=>p.GetProperty("operation").GetString()=="Check");Assert.False(check.TryGetProperty("all_or_none",out _));
        var groups=c.Groups??[];Assert.Equal(groups.Count>0,stage.TryGetProperty("all_or_none",out var actual));if(groups.Count>0){Assert.Equal(groups.Count,actual.GetArrayLength());for(var i=0;i<groups.Count;i++)Assert.Equal(groups[i],actual[i].EnumerateArray().Select(p=>p.GetString()));}
        var markdown=Encoding.UTF8.GetString(result.Markdown!.Bytes.Span);if(groups.Count==0)Assert.DoesNotContain("Supply together:",markdown);else foreach(var group in groups)Assert.Contains("Supply together: "+string.Join(", ",group.Select(p=>"--"+p))+".",markdown);
        OperationHelpContractCases.AssertCase(new(c.Id,request,null),new ScriptedOperationHelp(result));
    }
}
[Trait("Gate","Mock")]
public sealed class OperationHelpGroupMockTests
{
    [Theory,MemberData(nameof(OperationHelpGroupCases.Cases),MemberType=typeof(OperationHelpGroupCases))]
    public void CorrectDeclaredGroupAndOmission(HelpGroupCase c)=>OperationHelpGroupCases.AssertCase(c,new ScriptedOperationHelp(OperationHelpGroupCases.Correct(c)));
    [Theory,MemberData(nameof(OperationHelpGroupCases.Cases),MemberType=typeof(OperationHelpGroupCases))]
    public void WrongPresenceMembersAndMarkdownDiscriminate(HelpGroupCase c)
    {foreach(var mutation in new[]{"presence","members","markdown"})Assert.NotNull(Record.Exception(()=>OperationHelpGroupCases.AssertCase(c,new ScriptedOperationHelp(OperationHelpGroupCases.Correct(c,mutation)))));}
}
