using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public sealed record ModuleGraphCase(string Id, ModuleValidationInput Input, int ClassCount, string[] Edges, string[] Issues)
{
    public override string ToString()=>Id;
}

public static class ModuleGraphContractCases
{
    public static IEnumerable<object[]> Cases=>Create().Select(c=>new object[]{c});
    public static IEnumerable<object[]> DiagnosticCases=>Create().Where(c=>c.Issues.Length>0).Select(c=>new object[]{c});

    public static IReadOnlyList<ModuleGraphCase> Create()
    {
        static JsonObject Map(params (string Name,string Owner)[] values)=>new(values.Select(v=>new KeyValuePair<string,JsonNode?>(v.Name,JsonValue.Create(v.Owner))));
        static JsonObject Dependencies(params (string Name,string[] Targets)[] values)=>new(values.Select(v=>new KeyValuePair<string,JsonNode?>(v.Name,new JsonArray(v.Targets.Select(t=>(JsonNode?)JsonValue.Create(t)).ToArray()))));
        static ModuleValidationInput Input(JsonObject owners,JsonObject dependencies,string source,bool domainPorts=false)=>new(new JsonObject{{"classes",owners},{"dependencies",dependencies},{"interfaces_are_domain_ports",domainPorts}},source);
        var fixture=DocumentationFixture.Current;
        var codePath=Path.Combine(fixture.Workspace,"remediation/design-c4/architecture/code-diagrams.md");
        var modelPath=Path.Combine(fixture.Workspace,"remediation/design-c4/architecture/module-contract.json");
        var codeBytes=File.ReadAllBytes(codePath);var modelBytes=File.ReadAllBytes(modelPath);
        if(DocumentationFixture.Hash(codeBytes)!=fixture.ExpectedHashes["remediation/design-c4/architecture/code-diagrams.md"]||DocumentationFixture.Hash(modelBytes)!=fixture.ExpectedHashes["remediation/design-c4/architecture/module-contract.json"])
            throw new InvalidOperationException("Pinned module-graph inputs changed");
        return
        [
            new("MOD-VALID-PORT",Input(Map(("Repository","storage"),("CatalogPort","domain")),Dependencies(("storage",["domain"]),("domain",[])),"class Repository\ninterface CatalogPort\nRepository ..> CatalogPort"),2,["storage -> domain"],[]),
            new("MOD-ORIGINAL-CYCLE",Input(Map(("DeviceViewResolver","bindings"),("CallbackBroker","application")),Dependencies(("bindings",["application"]),("application",["bindings"])),"class DeviceViewResolver\nclass CallbackBroker\nDeviceViewResolver ..> CallbackBroker"),2,["bindings -> application"],["MODULE_CYCLE:bindings"]),
            new("MOD-ORIGINAL-FORBIDDEN",Input(Map(("Repository","storage"),("OwnerAuthenticationService","application")),Dependencies(("storage",["domain"]),("application",["domain"]),("domain",[])),"class Repository\nclass OwnerAuthenticationService\nRepository ..> OwnerAuthenticationService"),2,["storage -> application"],["FORBIDDEN_DEPENDENCY:Repository->OwnerAuthenticationService"]),
            new("MOD-ORIGINAL-DOMAIN-PORT",Input(Map(("LoopbackHost","bridge.android")),Dependencies(("bridge.android",[])),"interface LoopbackHost",true),1,[],["PORT_OUTSIDE_DOMAIN:LoopbackHost"]),
            new("MOD-MISSING-OWNER",Input(Map(),Dependencies(),"class Unknown"),1,[],["CLASS_NO_OWNER:Unknown"]),
            new("MOD-PER-VIEW-ENDPOINT",Input(Map(("A","domain"),("B","domain")),Dependencies(("domain",[])),"## test-view\n```plantuml\nclass A\nA ..> B\n```"),1,[],["IMPLICIT_ENDPOINT:test-view:B"]),
            new("MOD-DEPENDENCY-OWNER",Input(Map(("A","domain")),Dependencies(("domain",[])),"class A\nclass B\nA ..> B"),2,[],["CLASS_NO_OWNER:B","DEPENDENCY_OWNER_MISSING:A->B"]),
            new("MOD-UNUSED-CYCLE",Input(Map(),Dependencies(("a",["b"]),("b",["a"])),""),0,[],["MODULE_CYCLE:a"]),
            new("MOD-EXTERNAL-EDGE",Input(Map(("App","application"),("Remote","external")),Dependencies(("application",[])),"class App\nclass Remote\nApp --> Remote"),2,[],[]),
            new("MOD-ORIGINAL-FULL-GRAPH",new(JsonNode.Parse(modelBytes)!.AsObject(),System.Text.Encoding.UTF8.GetString(codeBytes)),96,
            ["bridge.android -> bridge.application","bridge.android -> bridge.bindings","bridge.android -> bridge.domain","bridge.android -> bridge.protocol","bridge.android -> bridge.storage.sqlite","bridge.application -> bridge.domain","bridge.bindings -> bridge.domain","bridge.protocol -> bridge.application","bridge.protocol -> bridge.domain","bridge.storage.sqlite -> bridge.domain","bridge.ui -> bridge.application","bridge.ui -> bridge.domain"],[])
        ];
    }

    public static ModuleValidationResult Correct(ModuleGraphCase test)=>new(test.ClassCount,test.Edges,test.Issues.Select(s=>new DesignIssue(s.Split(':',2)[0],s.StartsWith("MODULE_CYCLE:",StringComparison.Ordinal)?"architecture/module-contract.json":"architecture/code-diagrams.md",s.Split(':',2)[1])).ToArray());

    public static void AssertContract(ModuleGraphCase test,IModuleGraphValidator provider)
    {
        var before=test.Input.ModuleContract.ToJsonString();
        var result=provider.Validate(test.Input);
        Assert.Equal(test.ClassCount,result.ClassCount);
        Assert.Equal(test.Edges.Order(StringComparer.Ordinal),result.ModuleEdges.Order(StringComparer.Ordinal));
        Assert.Equal(test.Issues.Order(StringComparer.Ordinal),result.Issues.Select(i=>i.Code+":"+i.Subject).Order(StringComparer.Ordinal));
        Assert.All(result.Issues,i=>Assert.Equal(i.Code=="MODULE_CYCLE"?"architecture/module-contract.json":"architecture/code-diagrams.md",i.ArtifactPath));
        Assert.Equal(before,test.Input.ModuleContract.ToJsonString());
    }
}

public sealed class ScriptedModuleGraphValidator(ModuleValidationResult response,bool mutateInput=false) : IModuleGraphValidator
{
    public ModuleValidationInput? Received { get; private set; }
    public ModuleValidationResult Validate(ModuleValidationInput input){Received=input;if(mutateInput)input.ModuleContract["unexpected_mutation"]=true;return response;}
}

[Trait("Gate","Mock")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class ModuleGraphMockTests
{
    [Theory,MemberData(nameof(ModuleGraphContractCases.Cases),MemberType=typeof(ModuleGraphContractCases))]
    public void CorrectMocksPreserveOriginalInputsAssertionsAndDiagnostics(ModuleGraphCase test)
    {
        var mock=new ScriptedModuleGraphValidator(ModuleGraphContractCases.Correct(test));
        ModuleGraphContractCases.AssertContract(test,mock);Assert.Same(test.Input,mock.Received);
    }

    [Theory,MemberData(nameof(ModuleGraphContractCases.Cases),MemberType=typeof(ModuleGraphContractCases))]
    public void WrongClassCountsAreIndividuallyRejected(ModuleGraphCase test)=>Assert.NotNull(Record.Exception(()=>ModuleGraphContractCases.AssertContract(test,new ScriptedModuleGraphValidator(ModuleGraphContractCases.Correct(test) with{ClassCount=test.ClassCount+1}))));

    [Theory,MemberData(nameof(ModuleGraphContractCases.Cases),MemberType=typeof(ModuleGraphContractCases))]
    public void WrongEdgesAreIndividuallyRejected(ModuleGraphCase test)=>Assert.NotNull(Record.Exception(()=>ModuleGraphContractCases.AssertContract(test,new ScriptedModuleGraphValidator(ModuleGraphContractCases.Correct(test) with{ModuleEdges=test.Edges.Concat(["unexpected -> edge"]).ToArray()}))));

    [Theory,MemberData(nameof(ModuleGraphContractCases.Cases),MemberType=typeof(ModuleGraphContractCases))]
    public void MissingOrInventedDiagnosticIsIndividuallyRejected(ModuleGraphCase test)
    {
        var correct=ModuleGraphContractCases.Correct(test);
        var wrong=correct with{Issues=correct.Issues.Count>0?correct.Issues.Skip(1).ToArray():[new("UNEXPECTED","architecture/code-diagrams.md","unexpected")]};
        Assert.NotNull(Record.Exception(()=>ModuleGraphContractCases.AssertContract(test,new ScriptedModuleGraphValidator(wrong))));
    }

    [Theory,MemberData(nameof(ModuleGraphContractCases.DiagnosticCases),MemberType=typeof(ModuleGraphContractCases))]
    public void WrongDiagnosticPathsAreIndividuallyRejected(ModuleGraphCase test)
    {
        var correct=ModuleGraphContractCases.Correct(test);
        Assert.NotNull(Record.Exception(()=>ModuleGraphContractCases.AssertContract(test,new ScriptedModuleGraphValidator(correct with{Issues=correct.Issues.Select(i=>i with{ArtifactPath="wrong/path"}).ToArray()}))));
    }

    [Theory,MemberData(nameof(ModuleGraphContractCases.Cases),MemberType=typeof(ModuleGraphContractCases))]
    public void InputMutationIsIndividuallyRejected(ModuleGraphCase test)
    {
        try{Assert.NotNull(Record.Exception(()=>ModuleGraphContractCases.AssertContract(test,new ScriptedModuleGraphValidator(ModuleGraphContractCases.Correct(test),true))));}
        finally{test.Input.ModuleContract.Remove("unexpected_mutation");}
    }
}
