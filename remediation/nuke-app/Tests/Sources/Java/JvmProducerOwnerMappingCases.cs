using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Sources.Java;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources.Java;

// Original encode checks the truthiness of owners.get(outer), not merely key
// presence. Reuse the existing linker/case schema; no owner inference occurs.
public static class JvmProducerOwnerMappingCases
{
    private static IReadOnlyDictionary<string,string> Owners(string? mapped)=>new Dictionary<string,string>(StringComparer.Ordinal)
    { ["a.C"]="a/C",["a.Bad"]=mapped! };
    public static readonly IReadOnlyList<JvmCase> All=[
        new("JVM-PRODUCER-01-empty","a.Bad",null,null,Owners("")),
        new("JVM-PRODUCER-02-null","a.Bad",null,null,Owners(null)),
        new("JVM-PRODUCER-03-array","a.Bad[]",null,null,Owners("")),
        new("JVM-PRODUCER-04-generic","a.Bad<java.lang.String>",null,null,Owners("")),
        new("JVM-PRODUCER-05-return",null,JvmContractCases.Declaration("method",result:"a.Bad"),null,Owners("")),
        new("JVM-PRODUCER-06-parameter",null,JvmContractCases.Declaration("method",parameters:["a.Bad[]"]),null,Owners(""))
    ];
    public static IEnumerable<object[]> Cases=>All.Select(test=>new object[]{test.Id});
    public static JvmCase Get(string id)=>All.Single(test=>test.Id==id);
    public static void AssertContract(JvmCase test,IJvmSourceLinker provider)
    {
        var declaration=test.Declaration?.ToJsonString();var owners=test.KnownOwners.ToArray();
        JvmContractCases.AssertContract(test,provider);
        Assert.Equal(declaration,test.Declaration?.ToJsonString());Assert.Equal(owners,test.KnownOwners.ToArray());
    }
}
