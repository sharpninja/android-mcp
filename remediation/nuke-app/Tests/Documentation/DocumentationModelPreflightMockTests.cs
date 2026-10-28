using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public sealed class ModelPreflightEnvironment
{
    public string Scenario {get;}
    public List<string> Trace {get;}=[];
    public DocumentationModelPreflightInput Input {get;}
    public DocumentationModel Model {get;}
    public DocumentationProjection Ui {get;}=DocumentationFixture.Current.Correct;
    public ModuleValidationResult Modules {get;}
    public ModelContractValidationResult Validated {get;}=new(true,ModelContractCases.Scope,11,76);
    public JsonObject Module {get;}=(JsonObject)ModelContractFixture.Current.Module.DeepClone();
    public ModelContractInput? ReceivedContract {get;set;}
    public ModelPreflightEnvironment(string scenario)
    {
        Scenario=scenario;var f=DocumentationFixture.Current;var plan=DocumentationModelSourceCases.Expected();
        var rows=plan.Sources.Select(source=>
        {
            Artifact? content;
            if(source.Role==DocumentationInputRole.CanonicalContractOptional&&scenario=="OPTIONAL-CANONICAL-ABSENT")content=null;
            else
            {
                var bytes=source.Role==DocumentationInputRole.CanonicalContractOptional
                    ?ModelContractFixture.Current.Copies.Single(copy=>copy.File==DocumentationModelSourceCases.Contracts.Single(contract=>contract.Id==source.ContractId).File).Snapshot.Bytes.ToArray()
                    :f.ReadOriginalDesignBytes(source.RelativePath);
                if(scenario=="MODULE-JSON"&&source.RelativePath=="architecture/module-contract.json")bytes=new byte[]{0};
                content=new(source.RelativePath,bytes,source.MediaType);
            }
            return new DocumentationCapturedSource(new(source,content),content is null?null:new ArtifactReceiptVerifier(new NativeContentHash()).Describe(content));
        }).ToArray();
        var snapshot=new DocumentationModelSourceSnapshot(rows,SourceCollectionEnvironment.ExpectedReceipt(rows));
        Input=new(rows.Where(row=>row.Observation.Source.Role!=DocumentationInputRole.CanonicalContractOptional).Select(row=>row.Observation.Content!).ToArray(),snapshot,scenario!="MODULE-ISSUES-ALLOWED");
        Model=DocumentationModelReadCases.Correct(new("PREFLIGHT",Artifact(DocumentationAssetPaths.Blueprint),f.Blueprint));
        Modules=new(96,[],scenario is "MODULE-ISSUES" or "MODULE-ISSUES-ALLOWED"?[new("MODULE_CYCLE","architecture/module-contract.json","fixture")]:[],scenario=="DECLARATIONS-MISSING"?null:ModelContractFixture.Current.Classes);
    }
    public Artifact Artifact(string path)=>Input.RequiredArtifacts.Single(artifact=>artifact.RelativePath==path);
}
public sealed class RecordingModelPreflightServices(ModelPreflightEnvironment e):IDocumentationModelReader,IParsedDocumentationProjector,IModelContractValidator,IModuleGraphValidator,IStrictJsonInspector
{
    public DocumentationModel Read(Artifact input)
    {e.Trace.Add("model.read");Assert.Same(e.Artifact(DocumentationAssetPaths.Blueprint),input);if(e.Scenario=="READER-FAILURE")throw new InvalidDataException("reader fixture");return e.Model;}
    public DocumentationProjection Project(DocumentationInput input)=>throw new InvalidOperationException("Preflight must use parsed overload");
    public DocumentationProjection Project(DocumentationInput input,DocumentationModel model)
    {e.Trace.Add("ui.project");Assert.Same(e.Model,model);Assert.Same(e.Artifact(DocumentationAssetPaths.Blueprint),input.BlueprintMarkdown);Assert.Same(e.Artifact(DocumentationAssetPaths.Design),input.DesignMarkdown);if(e.Scenario=="UI-FAILURE")throw new InvalidDataException("ui fixture");return e.Ui;}
    public ModuleValidationResult Validate(ModuleValidationInput input)
    {e.Trace.Add("module");Assert.True(JsonNode.DeepEquals(e.Module,input.ModuleContract));Assert.Equal(Encoding.UTF8.GetString(e.Artifact("architecture/code-diagrams.md").Bytes.Span),input.CodeMarkdown);return e.Modules;}
    public ModelContractValidationResult Validate(ModelContractInput input)
    {
        e.Trace.Add("contracts");e.ReceivedContract=input;Assert.Same(e.Model,input.Model);Assert.Same(e.Artifact("architecture/code-diagrams.md"),input.CodeMarkdown);Assert.Same(e.Artifact("architecture/workspace.dsl"),input.WorkspaceDsl);Assert.Same(e.Artifact(DocumentationAssetPaths.Design),input.DesignMarkdown);
        Assert.True(JsonNode.DeepEquals(e.Module,input.ModuleContract));Assert.Same(e.Modules.DeclaredClasses,input.DeclaredClasses);
        var expected=e.Input.Sources.Sources.Where(row=>row.Observation.Source.Role==DocumentationInputRole.ContractSnapshotRequired).ToArray();Assert.Equal(3,input.ContractCopies.Count);
        for(var index=0;index<expected.Length;index++)
        {
            var source=expected[index].Observation.Source;var copy=input.ContractCopies[index];Assert.Equal(Path.GetFileName(source.RelativePath),copy.File);Assert.Equal(source.ExpectedSha256,copy.ExpectedSha256);Assert.Same(e.Artifact(source.RelativePath),copy.Snapshot);
            var canonical=e.Input.Sources.Sources.Single(row=>row.Observation.Source.Role==DocumentationInputRole.CanonicalContractOptional&&row.Observation.Source.ContractId==source.ContractId).Observation.Content;
            Assert.Same(canonical,copy.Canonical);
        }
        if(e.Scenario=="CONTRACT-FAILURE")throw new ModelContractException(["scripted contract rejection"]);return e.Validated;
    }
    public StrictJsonInspection Inspect(ReadOnlyMemory<byte> bytes)
    {e.Trace.Add("json");Assert.Equal(e.Artifact("architecture/module-contract.json").Bytes.ToArray(),bytes.ToArray());return new StrictJsonInspector().Inspect(bytes);}
    public StrictJsonInspection Inspect(JsonElement value)=>throw new InvalidOperationException("Use original admitted module bytes");
}
public static class DocumentationModelPreflightCases
{
    public static IEnumerable<object[]> Cases=>new[]{"FULL","OPTIONAL-CANONICAL-ABSENT","MODULE-ISSUES-ALLOWED","MODULE-ISSUES","DECLARATIONS-MISSING","MODULE-JSON","READER-FAILURE","CONTRACT-FAILURE","UI-FAILURE"}.Select(scenario=>new object[]{scenario});
    public static void AssertContract(ModelPreflightEnvironment e,IDocumentationModelPreflight provider)
    {
        var before=e.Input.RequiredArtifacts.Select(artifact=>artifact.Bytes.ToArray()).ToArray();var model=e.Model.Blueprint.ToJsonString();DocumentationModelPreflightResult? result=null;
        var error=Record.Exception(()=>result=provider.Prepare(e.Input));
        var success=e.Scenario is "FULL" or "OPTIONAL-CANONICAL-ABSENT" or "MODULE-ISSUES-ALLOWED";
        if(success){Assert.Null(error);Assert.NotNull(result);Assert.Same(e.Model,result.Model);Assert.Same(e.Ui,result.Ui);Assert.Same(e.Modules,result.Modules);Assert.Same(e.Validated,result.Contracts);}
        else
        {
            Assert.Null(result);
            if(e.Scenario is "MODULE-ISSUES" or "MODULE-JSON")Assert.Equal("MODULE_CONTRACT",Assert.IsType<ArchitectureAssetException>(error).Code);
            else if(e.Scenario is "DECLARATIONS-MISSING" or "CONTRACT-FAILURE")Assert.Equal(e.Scenario=="DECLARATIONS-MISSING"?new[]{"Module declaration authority missing"}:new[]{"scripted contract rejection"},Assert.IsType<ModelContractException>(error).Errors);
            else Assert.Equal(e.Scenario=="READER-FAILURE"?"reader fixture":"ui fixture",Assert.IsType<InvalidDataException>(error).Message);
        }
        var trace=e.Scenario=="MODULE-JSON"?new[]{"json"}:e.Scenario is "MODULE-ISSUES" or "DECLARATIONS-MISSING"?new[]{"json","module"}:e.Scenario=="READER-FAILURE"?new[]{"json","module","model.read"}:e.Scenario=="CONTRACT-FAILURE"?new[]{"json","module","model.read","contracts"}:new[]{"json","module","model.read","contracts","ui.project"};
        Assert.Equal(trace,e.Trace);Assert.Equal(model,e.Model.Blueprint.ToJsonString());
        for(var index=0;index<before.Length;index++)Assert.Equal(before[index],e.Input.RequiredArtifacts[index].Bytes.ToArray());
    }
}
public sealed class ScriptedDocumentationModelPreflight(ModelPreflightEnvironment e,string mutation=""):IDocumentationModelPreflight
{
    public DocumentationModelPreflightResult Prepare(DocumentationModelPreflightInput input)
    {
        Assert.Same(e.Input,input);var services=new RecordingModelPreflightServices(e);
        if(mutation=="outcome")return new(e.Model,e.Ui,e.Modules,e.Validated);
        var parsed=services.Inspect(e.Artifact("architecture/module-contract.json").Bytes);
        if(parsed.CanonicalDiagnostics.Count>0||parsed.Value is null)throw new ArchitectureAssetException("MODULE_CONTRACT","Invalid module JSON");
        var module=services.Validate(new ModuleValidationInput(e.Module,Encoding.UTF8.GetString(e.Artifact("architecture/code-diagrams.md").Bytes.Span)));
        if(input.EnforceModuleGraph&&module.Issues.Count>0&&mutation!="module-bypass")throw new ArchitectureAssetException("MODULE_CONTRACT","Module graph rejected");
        if(module.DeclaredClasses is null)throw new ModelContractException(["Module declaration authority missing"]);
        var model=services.Read(e.Artifact(DocumentationAssetPaths.Blueprint));if(mutation=="extra-read")_ =services.Read(e.Artifact(DocumentationAssetPaths.Blueprint));
        var copies=input.Sources.Sources.Where(row=>row.Observation.Source.Role==DocumentationInputRole.ContractSnapshotRequired).Select(row=>
        {var source=row.Observation.Source;return new ContractCopyObservation(Path.GetFileName(source.RelativePath),source.ExpectedSha256!,e.Artifact(source.RelativePath),input.Sources.Sources.Single(canonical=>canonical.Observation.Source.Role==DocumentationInputRole.CanonicalContractOptional&&canonical.Observation.Source.ContractId==source.ContractId).Observation.Content);}).ToArray();
        var check=new ModelContractInput(e.Artifact("architecture/code-diagrams.md"),e.Artifact("architecture/workspace.dsl"),e.Artifact(DocumentationAssetPaths.Design),mutation=="validator-model"?model with{}:model,e.Module,module.DeclaredClasses,copies);
        var validated=mutation=="skip-contract"?e.Validated:services.Validate(check);
        var ui=mutation=="skip-ui"?e.Ui:services.Project(new(e.Artifact(DocumentationAssetPaths.Blueprint),e.Artifact(DocumentationAssetPaths.Design)),mutation=="ui-model"?model with{}:model);
        if(mutation=="input-bytes"){Assert.True(MemoryMarshal.TryGetArray(input.RequiredArtifacts[0].Bytes,out var bytes));bytes.Array![bytes.Offset]^=1;}
        return new(mutation=="result-model"?model with{}:model,ui,module,validated);
    }
}
[Trait("Gate","Mock")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class DocumentationModelPreflightMockTests
{
    [Theory,MemberData(nameof(DocumentationModelPreflightCases.Cases),MemberType=typeof(DocumentationModelPreflightCases))]
    public void CorrectSharedModelPreflightMocksPass(string scenario){var e=new ModelPreflightEnvironment(scenario);DocumentationModelPreflightCases.AssertContract(e,new ScriptedDocumentationModelPreflight(e));}
    [Theory,MemberData(nameof(DocumentationModelPreflightCases.Cases),MemberType=typeof(DocumentationModelPreflightCases))]
    public void EveryWrongPreflightOutcomeIsRejected(string scenario){var e=new ModelPreflightEnvironment(scenario);Assert.NotNull(Record.Exception(()=>DocumentationModelPreflightCases.AssertContract(e,new ScriptedDocumentationModelPreflight(e,"outcome"))));}
    [Theory]
    [InlineData("extra-read")][InlineData("validator-model")][InlineData("ui-model")][InlineData("skip-contract")][InlineData("skip-ui")][InlineData("input-bytes")][InlineData("result-model")]
    public void EveryAuthorityAndSourceBypassIsRejected(string mutation){var e=new ModelPreflightEnvironment("FULL");Assert.NotNull(Record.Exception(()=>DocumentationModelPreflightCases.AssertContract(e,new ScriptedDocumentationModelPreflight(e,mutation))));}
    [Fact]
    public void EnforcedModuleFailureCannotBecomeMetadataOnly(){var e=new ModelPreflightEnvironment("MODULE-ISSUES");Assert.NotNull(Record.Exception(()=>DocumentationModelPreflightCases.AssertContract(e,new ScriptedDocumentationModelPreflight(e,"module-bypass"))));}
}
