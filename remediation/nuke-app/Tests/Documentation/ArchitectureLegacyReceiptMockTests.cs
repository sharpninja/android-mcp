using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public sealed record ArchitectureLegacyReceiptCase(string Id,string[] Aliases,bool Enabled=true,string Failure="")
{public override string ToString()=>Id;}
public sealed class ArchitectureLegacyReceiptEnvironment
{
    public ArchitectureLegacyReceiptCase Case {get;}
    public ArchitectureSubsetEnvironment Subset {get;}
    public ArchitecturePipelineEnvironment Pipeline=>Subset.Inner;
    public Artifact ExpectedReceipt {get;}=LegacyReceiptCases.Create().Single(test=>test.Id=="ARCH-ORIGINAL").Expected;
    public IReadOnlyList<Artifact> Expected {get;}
    public ProcessResult? Validation {get;set;}
    public int ReceiptWorkspaceReads {get;set;}
    public bool Emits=>Case.Enabled&&Subset.Has(DesignStage.ArchitectureValidate);
    public ArchitectureLegacyReceiptEnvironment(ArchitectureLegacyReceiptCase test)
    {
        Case=test;Subset=new(new(test.Id,test.Aliases));
        if(Emits){Subset.ExpectedWork.Add(ExpectedReceipt.RelativePath,ExpectedReceipt);Pipeline.ExpectedWork.Add(ExpectedReceipt.RelativePath,ExpectedReceipt);}
        Expected=Subset.ExpectedPublic.Concat(Emits?[ExpectedReceipt]:Array.Empty<Artifact>()).OrderBy(artifact=>artifact.RelativePath,StringComparer.Ordinal).ToArray();
        if(test.Failure=="cancel-before")Pipeline.Cancellation.Cancel();
    }
}
public sealed class RecordingLegacyReceiptHash(ArchitectureLegacyReceiptEnvironment e):IArtifactReceiptVerifier
{
    public StoredArtifact Describe(Artifact artifact)
    {
        Assert.Same(e.Subset.Inputs.Single(input=>input.RelativePath=="architecture/workspace.dsl"),artifact);
        Assert.NotNull(e.Validation);Assert.Equal(0,e.Validation.ExitCode);Assert.False(e.Validation.Cancelled);
        e.ReceiptWorkspaceReads++;e.Pipeline.Trace.Add("receipt.workspace");return e.Pipeline.Receipts.Describe(artifact);
    }
    public bool Matches(Artifact artifact,StoredArtifact identity)=>e.Pipeline.Receipts.Matches(artifact,identity);
}
public sealed class RecordingLegacyValidationTransport(ArchitectureLegacyReceiptEnvironment e):IArchitectureRendererTransport
{
    public async Task<ProcessResult> ExecuteAsync(DocumentationRendererProfile profile,ArchitectureRendererContext context,ArchitectureRendererCommand command,CancellationToken token=default)
    {
        var result=await new RecordingPipelineTransport(e.Pipeline).ExecuteAsync(profile,context,command,token);
        if(command.Phase!=ArchitectureRendererPhase.ValidateWorkspace)return result;
        if(e.Case.Failure=="validation-throw")throw new ArchitectureAssetException("RENDERER_PROCESS","Recorded validation-phase failure");
        result=result with{StandardOutputPath="retained/validation.stdout",StandardErrorPath="retained/validation.stderr",
            ExitCode=e.Case.Failure=="validation-exit"?1:0,Cancelled=e.Case.Failure=="validation-cancelled"};
        e.Validation=result;e.Pipeline.Trace.Add("validation.return");return result;
    }
}
public static class ArchitectureLegacyReceiptCases
{
    public static IEnumerable<object[]> Cases=>new[]
    {
        new ArchitectureLegacyReceiptCase("VALIDATE",["validateArchitecture"]),new("RENDER",["generateArchitectureDiagrams"]),new("EXPORT",["exportArchitecture"]),
        new("UNION",["exportArchitecture","generateArchitectureDiagrams"]),new("OLD-DISABLED",["generateArchitectureDiagrams"],false),
        new("EXTRACT-NO-VALIDATION",["extractCodeViews"]),new("MODEL-NO-VALIDATION",["validateModelContracts"]),new("TOOLS-NO-VALIDATION",["verifyToolchain"]),
        new("VALIDATION-EXIT",["validateArchitecture"],true,"validation-exit"),new("VALIDATION-CANCELLED",["validateArchitecture"],true,"validation-cancelled"),
        new("VALIDATION-THROW",["validateArchitecture"],true,"validation-throw"),new("CANCEL-BEFORE",["validateArchitecture"],true,"cancel-before")
    }.Select(test=>new object[]{test});
    public static async Task AssertContract(ArchitectureLegacyReceiptEnvironment e,IArchitectureStageProjectionComposer provider)
    {
        var before=e.Subset.Inputs.Select(input=>input.Bytes.ToArray()).ToArray();
        var prepared=provider.PrepareSubset(e.Subset.Selection,e.Subset.Inputs,e.Subset.Profile,e.Subset.Code,e.Subset.Context,e.Subset.Admission);
        Assert.Equal(e.Expected.Select(artifact=>artifact.RelativePath),prepared.OutputPaths.Order(StringComparer.Ordinal));
        Assert.Empty(e.Pipeline.Trace);Assert.Equal(0,e.ReceiptWorkspaceReads);Assert.Null(prepared.VerifyBeforeCommitAsync);
        IReadOnlyList<Artifact>? actual=null;var error=await Record.ExceptionAsync(async()=>actual=await prepared.MaterializeAsync(e.Pipeline.Cancellation.Token));
        if(e.Case.Failure=="cancel-before")
        {Assert.IsAssignableFrom<OperationCanceledException>(error);Assert.Null(actual);Assert.Empty(e.Pipeline.Trace);Assert.Equal(0,e.Subset.Runtime.Calls);return;}
        if(e.Case.Failure.StartsWith("validation-",StringComparison.Ordinal))
        {
            var failure=Assert.IsType<ArtifactGenerationException>(error);Assert.Equal("PROJECTION_FAILED",failure.Code);Assert.Null(actual);
            Assert.Equal(e.Case.Failure=="validation-throw"?"RENDERER_PROCESS":"LEGACY_RECEIPT_UNVALIDATED",Assert.IsType<ArchitectureAssetException>(failure.InnerException).Code);
            Assert.Equal(0,e.ReceiptWorkspaceReads);Assert.DoesNotContain(e.ExpectedReceipt.RelativePath,e.Pipeline.Work.Keys);Assert.DoesNotContain("work-commit",e.Pipeline.Trace);
            Assert.Equal(new[]{ArchitectureRendererPhase.RenderCode,ArchitectureRendererPhase.ValidateWorkspace},e.Pipeline.Commands.Select(command=>command.Phase));return;
        }
        Assert.Null(error);Assert.NotNull(actual);DesignDocumentationStageCases.AssertArtifacts(e.Expected,actual);
        Assert.Equal(e.Subset.ExpectedPhases,e.Pipeline.Commands.Select(command=>command.Phase));
        Assert.Equal(e.Emits?1:0,e.ReceiptWorkspaceReads);
        if(e.Emits)
        {
            var write="write:"+e.ExpectedReceipt.RelativePath;
            Assert.Equal(1,e.Pipeline.Trace.Count(entry=>entry==write));
            Assert.True(e.Pipeline.Trace.IndexOf("validation.return")<e.Pipeline.Trace.IndexOf("receipt.workspace"));
            Assert.True(e.Pipeline.Trace.IndexOf("receipt.workspace")<e.Pipeline.Trace.IndexOf(write));
            Assert.True(e.Pipeline.Trace.IndexOf(write)<e.Pipeline.Trace.IndexOf("work-commit"));
            Assert.Equal(e.ExpectedReceipt.Bytes.ToArray(),e.Pipeline.Work[e.ExpectedReceipt.RelativePath].Bytes.ToArray());
        }
        else Assert.DoesNotContain(e.ExpectedReceipt.RelativePath,e.Pipeline.Work.Keys);
        Assert.Equal(e.Subset.Has(DesignStage.Tools)?1:0,e.Subset.BootstrapCalls);
        for(var index=0;index<before.Length;index++)Assert.Equal(before[index],e.Subset.Inputs[index].Bytes.ToArray());
    }
}
public sealed class ScriptedArchitectureLegacyReceipt(ArchitectureLegacyReceiptEnvironment e,string mutation=""):IArchitectureStageProjectionComposer
{
    public ArtifactGenerationPreparation PrepareSubset(DesignStagePlan selected,IReadOnlyList<Artifact> inputs,DocumentationRendererProfile? profile,
        IReadOnlyList<Artifact> code,ArchitectureRendererContext? context,Func<CancellationToken,Task>? admit)
    {
        var paths=e.Expected.Select(artifact=>artifact.RelativePath).ToArray();if(mutation=="paths")paths=paths.Where(path=>path!=e.ExpectedReceipt.RelativePath).ToArray();
        return new(paths,async token=>
        {
            if(mutation!="cancel-before")token.ThrowIfCancellationRequested();
            if(!e.Subset.Has(DesignStage.Tools))return e.Expected;
            var store=new RecordingPipelineStore(e.Pipeline);var transport=new RecordingLegacyValidationTransport(e);
            var formatter=new DocumentationLegacyReceipts(new CanonicalJson(),new StrictJsonInspector(),new RecordingLegacyReceiptHash(e));
            var recipe=new ArtifactGenerationRecipe(inputs.Select(artifact=>new ArtifactSourceSpec(artifact.RelativePath,artifact.MediaType,e.Pipeline.Receipts.Describe(artifact))).ToArray(),_=>new(e.Subset.ExpectedWork.Keys.ToArray(),async innerToken=>
            {
                foreach(var source in inputs.Concat(code))await store.WriteAsync(source,innerToken);await store.WriteAsync(e.Pipeline.ExpectedWork[".docker-cli/config.json"],innerToken);
                await admit!(innerToken);await new RecordingPipelineAdmission(e.Pipeline).AdmitAsync(profile!,context!,innerToken);
                if(mutation=="early")await store.WriteAsync(formatter.Architecture(inputs.Single(input=>input.RelativePath=="architecture/workspace.dsl"),new(0,"wrong","wrong",DateTimeOffset.UnixEpoch,DateTimeOffset.UnixEpoch,false)),innerToken);
                foreach(var phase in e.Subset.ExpectedPhases)
                {
                    var sources=phase==ArchitectureRendererPhase.RenderCode?code.Select(artifact=>artifact.RelativePath).ToArray():phase==ArchitectureRendererPhase.RenderNative?ArchitecturePipelineEnvironment.NativeKeys.Select(key=>"build/structurizr-profiled/structurizr-"+key+".puml").ToArray():Array.Empty<string>();
                    var result=await transport.ExecuteAsync(profile!,context!,new(phase,sources),innerToken);
                    if(mutation=="twice"&&phase==ArchitectureRendererPhase.ValidateWorkspace)await transport.ExecuteAsync(profile!,context!,new(phase,[]),innerToken);
                    if(phase==ArchitectureRendererPhase.ValidateWorkspace&&e.Emits&&mutation!="omit")
                    {
                        if(mutation=="wrong-phase")result=result with{ExitCode=0,Cancelled=false};
                        var receipt=formatter.Architecture(inputs.Single(input=>input.RelativePath=="architecture/workspace.dsl"),result);
                        if(mutation=="bytes")receipt=receipt with{Bytes=new byte[]{0}};
                        await store.WriteAsync(receipt,innerToken);
                    }
                }
                foreach(var artifact in e.Subset.ExpectedWork.Values.Where(artifact=>artifact.RelativePath!=e.ExpectedReceipt.RelativePath))await store.WriteAsync(artifact,innerToken);
                return e.Subset.ExpectedWork.Keys.Select(path=>e.Pipeline.Work[path]).ToArray();
            }));
            await e.Subset.Runtime.ExecuteAsync(ArchitecturePipelineEnvironment.WorkGeneration,recipe,token);
            var result=e.Expected.ToArray();return mutation=="output"?result.Where(artifact=>artifact.RelativePath!=e.ExpectedReceipt.RelativePath).ToArray():result;
        });
    }
    public ArchitectureCompositionPlan Plan(IReadOnlyList<Artifact> inputs,DocumentationRendererProfile profile,IReadOnlyList<Artifact> code)=>throw new InvalidOperationException("Old six-input host remains on its existing contract");
    public Task<IReadOnlyList<Artifact>> RenderAsync(ArchitectureCompositionPlan plan,ArchitectureRendererContext context,IReadOnlyList<ToolArtifactReceipt> tools,CancellationToken token=default)=>throw new InvalidOperationException("Old six-input host remains on its existing contract");
}
[Trait("Gate","Mock")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class ArchitectureLegacyReceiptMockTests
{
    [Theory,MemberData(nameof(ArchitectureLegacyReceiptCases.Cases),MemberType=typeof(ArchitectureLegacyReceiptCases))]
    public Task CorrectOptInPhaseReceiptsAndOldDefaultsPass(ArchitectureLegacyReceiptCase test)
    {var e=new ArchitectureLegacyReceiptEnvironment(test);return ArchitectureLegacyReceiptCases.AssertContract(e,new ScriptedArchitectureLegacyReceipt(e));}
    [Theory]
    [InlineData("VALIDATE","paths")][InlineData("VALIDATE","early")][InlineData("VALIDATE","omit")][InlineData("VALIDATE","twice")]
    [InlineData("VALIDATE","bytes")][InlineData("VALIDATE","output")][InlineData("VALIDATION-EXIT","wrong-phase")]
    [InlineData("VALIDATION-CANCELLED","wrong-phase")][InlineData("CANCEL-BEFORE","cancel-before")]
    public async Task EachPhaseTimingOutputAndCancellationFaultIsRejected(string id,string mutation)
    {var test=ArchitectureLegacyReceiptCases.Cases.Select(row=>(ArchitectureLegacyReceiptCase)row[0]).Single(test=>test.Id==id);var e=new ArchitectureLegacyReceiptEnvironment(test);Assert.NotNull(await Record.ExceptionAsync(()=>ArchitectureLegacyReceiptCases.AssertContract(e,new ScriptedArchitectureLegacyReceipt(e,mutation))));}
}
