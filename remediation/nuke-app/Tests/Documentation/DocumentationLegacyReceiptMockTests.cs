using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using AndroidMcp.BuildTool.Documentation.Validation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public enum LegacyReceiptKind { Module,Model,Ui,Architecture,Docs }
public sealed record LegacyReceiptCase(string Id,LegacyReceiptKind Kind,Artifact Expected,string? Error=null,
    ModuleValidationResult? Modules=null,ModelContractValidationResult? Model=null,Artifact? UiManifest=null,
    Artifact? Workspace=null,ProcessResult? Process=null,GeneratedDesignValidationResult? Documents=null)
{public override string ToString()=>Id;}

public static class LegacyReceiptCases
{
    public static readonly string[] Names=["module-graph.json","model-contracts.json","ui-projection.json","architecture-validation.txt","docs-validation.txt"];
    public static readonly string[] Pins=["7445eb59d4342a447c6975e2211f09e01d0a7a8889fe584720acaa62f6bee5ce","21cef9e5e767a2025070c21d48bc98cf72845dc2237aa13d69ab430aa004e8db","66bf34c5a56947a709dc91a94cf802853d508fd2507b81ca33ed78861e57b320","c1c7d76a0b86a8506defdeb2b396ea774bcc188b2bae38c09d9b161017dff6c4","94a3317902d1900854e80bbad4d91b3375d0329575ddce6fd9f19945d74e0c25"];
    public static Artifact Original(LegacyReceiptKind kind)
    {
        var index=(int)kind;var bytes=File.ReadAllBytes(Path.Combine(DocumentationFixture.Current.Workspace,"remediation/nuke-planning/docs/fixtures/legacy-gradle-receipts-original",Names[index]+".fixture"));
        Assert.Equal(Pins[index],ContentHash.Sha256(bytes));return new("build/receipts/"+Names[index],bytes,index<3?"application/json":"text/plain");
    }
    public static Artifact Json(Artifact original,Action<JsonObject> change)
    {var value=JsonNode.Parse(original.Bytes.Span)!.AsObject();change(value);return original with{Bytes=Encoding.UTF8.GetBytes(value.ToJsonString()+"\n")};}
    private static readonly Lazy<(Artifact Artifact,StoredArtifact Identity)> ProjectedUi=new(()=>
    {
        // Input to the receipt formatter comes from the existing, independently
        // proven projector, not the receipt formatter or a borrowed file identity.
        var fixture=DocumentationFixture.Current;var verifier=new ArtifactReceiptVerifier(new NativeContentHash());
        var blueprint=verifier.Describe(fixture.Input.BlueprintMarkdown);var design=verifier.Describe(fixture.Input.DesignMarkdown);
        var projected=new DocumentationProjector().Project(fixture.Input).Artifacts.Single(artifact=>artifact.RelativePath=="docs/ui-asset-manifest.json");
        Assert.Equal("application/json",projected.MediaType);
        Assert.True(verifier.Matches(fixture.Input.BlueprintMarkdown,blueprint));Assert.True(verifier.Matches(fixture.Input.DesignMarkdown,design));
        var retained=projected with{Bytes=projected.Bytes.ToArray()};return (retained,verifier.Describe(retained));
    });
    public static Artifact NativeUiInput()
    {
        var (projected,identity)=ProjectedUi.Value;
        // Each test owns a clone; mutation controls cannot corrupt the cached input.
        var input=projected with{Bytes=projected.Bytes.ToArray()};
        Assert.True(new ArtifactReceiptVerifier(new NativeContentHash()).Matches(input,identity));return input;
    }
    private static Artifact ArchitectureExpected(Artifact workspace)
    {
        const string originalHash="ee49fbdd60efac1c68356c5fa39ccb6c75609526fc580677405d61a0963d9281";
        var original=Original(LegacyReceiptKind.Architecture);var text=Encoding.UTF8.GetString(original.Bytes.Span);
        Assert.Equal("Structurizr validation exited0\nworkspace_sha256="+originalHash+"\n",text);
        return original with{Bytes=Encoding.UTF8.GetBytes(text.Replace(originalHash,ContentHash.Sha256(workspace.Bytes.Span),StringComparison.Ordinal))};
    }
    public static IReadOnlyList<LegacyReceiptCase> Create()
    {
        var moduleCase=ModuleGraphContractCases.Create().Single(test=>test.Id=="MOD-ORIGINAL-FULL-GRAPH");
        var modules=ModuleGraphContractCases.Correct(moduleCase);
        var model=new ModelContractValidationResult(true,ModelContractCases.Scope,11,76);
        var ui=NativeUiInput();
        var workspace=ArchitecturePreparationCases.CreateCode()[0].Input.Workspace;
        var process=new ProcessResult(0,"/retained/stdout","/retained/stderr",DateTimeOffset.UnixEpoch,DateTimeOffset.UnixEpoch.AddSeconds(1),false);
        var docs=new GeneratedDesignValidationResult(DocumentationValidationPhase.BeforeManifest,new(0,261,221,17),[],[],[],[]);
        var minimalUi=new Artifact("docs/ui-asset-manifest.json",Encoding.UTF8.GetBytes("{\"screens\":[{\"controls\":{\"C1\":{},\"C2\":{}},\"state_specs\":{\"S1\":{}}}],\"storyboards\":[{\"steps\":[{}]}],\"state_svg_files\":[\"screens/states/S1.svg\",\"screens/steps/T1.svg\"]}"),"application/json");
        var otherWorkspace=new Artifact(workspace.RelativePath,Encoding.UTF8.GetBytes("another admitted workspace\n"),workspace.MediaType);
        var rows=new List<LegacyReceiptCase>
        {
            new("MODULE-ORIGINAL",LegacyReceiptKind.Module,Original(LegacyReceiptKind.Module),Modules:modules),
            new("MODULE-VARIABLE",LegacyReceiptKind.Module,Json(Original(LegacyReceiptKind.Module),value=>{value["class_count"]=2;value["module_edges"]=new JsonArray("a -> b");}),Modules:new(2,["a -> b"],[])),
            new("MODEL-ORIGINAL",LegacyReceiptKind.Model,Original(LegacyReceiptKind.Model),Model:model),
            new("MODEL-VARIABLE",LegacyReceiptKind.Model,Json(Original(LegacyReceiptKind.Model),value=>{value["scope"]="fixture metadata scope";value["screens"]=2;value["states"]=5;}),Model:new(true,"fixture metadata scope",2,5)),
            new("UI-ORIGINAL",LegacyReceiptKind.Ui,Original(LegacyReceiptKind.Ui),UiManifest:ui),
            new("UI-VARIABLE",LegacyReceiptKind.Ui,Json(Original(LegacyReceiptKind.Ui),value=>{value["screens"]=1;value["controls"]=2;value["states"]=1;value["storyboards"]=1;value["steps"]=1;value["state_svg_files"]=new JsonArray("screens/states/S1.svg","screens/steps/T1.svg");}),UiManifest:minimalUi),
            new("ARCH-ORIGINAL",LegacyReceiptKind.Architecture,ArchitectureExpected(workspace),Workspace:workspace,Process:process),
            new("ARCH-VARIABLE",LegacyReceiptKind.Architecture,ArchitectureExpected(otherWorkspace),Workspace:otherWorkspace,Process:process),
            new("DOCS-ORIGINAL",LegacyReceiptKind.Docs,Original(LegacyReceiptKind.Docs),Documents:docs),
            new("DOCS-VARIABLE",LegacyReceiptKind.Docs,new("build/receipts/docs-validation.txt",Encoding.UTF8.GetBytes("Vector SVGs=7; Markdown relative links valid; no raster substitutions; no public rendering\n"),"text/plain"),Documents:docs with{Counters=docs.Counters with{RenderedSvgCount=7}})
        };
        rows.Add(rows[0] with{Id="MODULE-REJECTED",Modules=modules with{Issues=[new("MODULE_CYCLE","architecture/module-contract.json","fixture")]},Error="LEGACY_RECEIPT_UNVALIDATED"});
        rows.Add(rows[2] with{Id="MODEL-REJECTED",Model=model with{Validated=false},Error="LEGACY_RECEIPT_UNVALIDATED"});
        rows.Add(rows[4] with{Id="UI-INVALID-JSON",UiManifest=ui with{Bytes=new byte[]{0}},Error="LEGACY_RECEIPT_UI"});
        rows.Add(rows[4] with{Id="UI-WRONG-PATH",UiManifest=ui with{RelativePath="docs/not-the-projected-manifest.json"},Error="LEGACY_RECEIPT_UI"});
        rows.Add(rows[4] with{Id="UI-WRONG-MEDIA",UiManifest=ui with{MediaType="application/octet-stream"},Error="LEGACY_RECEIPT_UI"});
        rows.Add(rows[6] with{Id="ARCH-NONZERO",Process=process with{ExitCode=1},Error="LEGACY_RECEIPT_UNVALIDATED"});
        rows.Add(rows[6] with{Id="ARCH-CANCELLED",Process=process with{Cancelled=true},Error="LEGACY_RECEIPT_UNVALIDATED"});
        rows.Add(rows[8] with{Id="DOCS-REJECTED",Documents=docs with{Issues=[new("BROKEN_LINK","README.md","fixture")]},Error="LEGACY_RECEIPT_UNVALIDATED"});
        rows.Add(rows[8] with{Id="DOCS-PARSE-FAILURE",Documents=docs with{ParseDiagnostics=[new("docs/a.svg",1,2,"fixture")]},Error="LEGACY_RECEIPT_UNVALIDATED"});
        rows.Add(rows[8] with{Id="DOCS-AFTER-IS-NOT-BEFORE",Documents=docs with{Phase=DocumentationValidationPhase.AfterManifest},Error="LEGACY_RECEIPT_PHASE"});
        return rows;
    }
    public static IEnumerable<object[]> Cases=>Create().Select(test=>new object[]{test});
    public static IEnumerable<object[]> MetadataFaults=>Create().Where(test=>test.Error is null).SelectMany(test=>new[]{"path","media","missing-lf","extra-lf"}.Select(mutation=>new object[]{test,mutation}));
    public static IEnumerable<object[]> JsonFieldFaults=>Create().Where(test=>test.Error is null&&test.Expected.MediaType=="application/json")
        .SelectMany(test=>JsonNode.Parse(test.Expected.Bytes.Span)!.AsObject().Select(member=>new object[]{test,member.Key}));
    public static IEnumerable<object[]> StateFileFaults=>Create().Where(test=>test.Error is null&&test.Kind==LegacyReceiptKind.Ui)
        .SelectMany(test=>JsonNode.Parse(test.Expected.Bytes.Span)!["state_svg_files"]!.AsArray().Select((_,index)=>new object[]{test,index}));
    public static void AssertReceipt(LegacyReceiptCase test,Func<LegacyReceiptCase,Artifact> provider)
    {
        var before=JsonSerializer.SerializeToUtf8Bytes(test);Artifact? actual=null;var error=Record.Exception(()=>actual=provider(test));
        if(test.Error is not null){Assert.Equal(test.Error,Assert.IsType<ArchitectureAssetException>(error).Code);Assert.Null(actual);}
        else
        {
            Assert.Null(error);Assert.NotNull(actual);Assert.Equal(test.Expected.RelativePath,actual.RelativePath);Assert.Equal(test.Expected.MediaType,actual.MediaType);
            var text=Encoding.UTF8.GetString(actual.Bytes.Span);Assert.EndsWith("\n",text,StringComparison.Ordinal);Assert.False(text.EndsWith("\n\n",StringComparison.Ordinal));
            if(test.Expected.MediaType=="application/json")
            {
                var canonical=new CanonicalJson();
                // Semantic JSON compatibility is intentional; original pretty JSON
                // bytes remain pinned fixtures and are not relabelled byte parity.
                Assert.Equal(canonical.Serialize(test.Expected.Bytes),canonical.Serialize(actual.Bytes));
            }
            else Assert.Equal(test.Expected.Bytes.ToArray(),actual.Bytes.ToArray());
        }
        Assert.Equal(before,JsonSerializer.SerializeToUtf8Bytes(test));
    }
}
public sealed class ScriptedLegacyReceipt(string mutation="",string fieldName="",int stateIndex=-1)
{
    public Artifact Format(LegacyReceiptCase test)
    {
        if(test.Error is not null&&mutation!="outcome")throw new ArchitectureAssetException(test.Error,"Retain unvalidated failure; emit no success receipt");
        if(test.Error is null&&mutation=="outcome")throw new ArchitectureAssetException("WRONG","fixture");
        var result=test.Expected with{Bytes=test.Expected.Bytes.ToArray()};
        if(mutation=="path")return result with{RelativePath="validation/not-the-public-receipt"};
        if(mutation=="media")return result with{MediaType="wrong"};
        if(mutation=="missing-lf")return result with{Bytes=Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(result.Bytes.Span).TrimEnd('\n'))};
        if(mutation=="extra-lf")return result with{Bytes=Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(result.Bytes.Span)+"\n")};
        if(mutation=="field")return LegacyReceiptCases.Json(result,value=>value[fieldName]="wrong");
        if(mutation=="state-file")return LegacyReceiptCases.Json(result,value=>value["state_svg_files"]![stateIndex]="screens/steps/WRONG.svg");
        if(mutation=="text")return result with{Bytes=Encoding.UTF8.GetBytes("Wrong success scope\n")};
        if(mutation=="input")
        {Assert.NotNull(test.UiManifest);Assert.True(MemoryMarshal.TryGetArray(test.UiManifest.Bytes,out var bytes));bytes.Array![bytes.Offset]^=1;}
        return result;
    }
}
[Trait("Gate","Mock")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class DocumentationLegacyReceiptMockTests
{
    [Theory,MemberData(nameof(LegacyReceiptCases.Cases),MemberType=typeof(LegacyReceiptCases))]
    public void CorrectFiveReceiptMocksPreserveTypedAuthorities(LegacyReceiptCase test)=>LegacyReceiptCases.AssertReceipt(test,new ScriptedLegacyReceipt().Format);
    [Theory,MemberData(nameof(LegacyReceiptCases.Cases),MemberType=typeof(LegacyReceiptCases))]
    public void EveryWrongValidationOutcomeIsRejected(LegacyReceiptCase test)=>Assert.NotNull(Record.Exception(()=>LegacyReceiptCases.AssertReceipt(test,new ScriptedLegacyReceipt("outcome").Format)));
    [Theory,MemberData(nameof(LegacyReceiptCases.MetadataFaults),MemberType=typeof(LegacyReceiptCases))]
    public void EachReceiptPathMediaAndTerminatorFaultIsRejected(LegacyReceiptCase test,string mutation)=>Assert.NotNull(Record.Exception(()=>LegacyReceiptCases.AssertReceipt(test,new ScriptedLegacyReceipt(mutation).Format)));
    [Theory,MemberData(nameof(LegacyReceiptCases.JsonFieldFaults),MemberType=typeof(LegacyReceiptCases))]
    public void EveryOriginalJsonFieldRejectsWrongMetadata(LegacyReceiptCase test,string fieldName)=>Assert.NotNull(Record.Exception(()=>LegacyReceiptCases.AssertReceipt(test,new ScriptedLegacyReceipt("field",fieldName).Format)));
    [Theory,MemberData(nameof(LegacyReceiptCases.StateFileFaults),MemberType=typeof(LegacyReceiptCases))]
    public void EachStateAndStepSvgReceiptPathIsChecked(LegacyReceiptCase test,int index)=>Assert.NotNull(Record.Exception(()=>LegacyReceiptCases.AssertReceipt(test,new ScriptedLegacyReceipt("state-file",stateIndex:index).Format)));
    [Theory]
    [InlineData("ARCH-ORIGINAL")][InlineData("ARCH-VARIABLE")][InlineData("DOCS-ORIGINAL")][InlineData("DOCS-VARIABLE")]
    public void ExactTextReceiptsRejectDifferentSuccessText(string id)
    {var test=LegacyReceiptCases.Create().Single(test=>test.Id==id);Assert.NotNull(Record.Exception(()=>LegacyReceiptCases.AssertReceipt(test,new ScriptedLegacyReceipt("text").Format)));}
    [Fact]
    public void ReceiptFormattingCannotMutateAdmittedProjection()
    {var test=LegacyReceiptCases.Create().Single(test=>test.Id=="UI-ORIGINAL");Assert.NotNull(Record.Exception(()=>LegacyReceiptCases.AssertReceipt(test,new ScriptedLegacyReceipt("input").Format)));}
    [Fact]
    public void ProvenProjectorInputIsCachedAndIsolatedWithoutChangingHistoricalCarrier()
    {
        var carrier=DocumentationFixture.Current.Correct.Artifacts.Single(artifact=>artifact.RelativePath=="docs/ui-asset-manifest.json");
        var historicalBytes=carrier.Bytes.ToArray();var input=LegacyReceiptCases.NativeUiInput();var expected=input.Bytes.ToArray();
        Assert.Equal("application/json",input.MediaType);Assert.True(MemoryMarshal.TryGetArray(input.Bytes,out var bytes));bytes.Array![bytes.Offset]^=1;
        Assert.Equal(expected,LegacyReceiptCases.NativeUiInput().Bytes.ToArray());
        Assert.Equal("text/plain",carrier.MediaType);Assert.Equal(historicalBytes,carrier.Bytes.ToArray());
    }
}
