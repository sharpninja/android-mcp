using System.IO.Compression;
using System.Text;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using AndroidMcp.BuildTool.Documentation.Validation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

[Trait("Gate","Real"),Trait("Requirement","FR-BUILD-005"),Trait("Requirement","TEST-BUILD-005")]
public sealed class DocumentationBootstrapCarryRealTests
{
    [Theory,MemberData(nameof(BootstrapCarryCases.Cases),MemberType=typeof(BootstrapCarryCases))]
    public Task SameReceiptCasesBindExistingFactoryValidatorManifestAndZip(string id)=>BootstrapCarryCases.AssertContract(id,Probe);
    private static async Task<BootstrapCarryObservation> Probe(string id)
    {
        var receipt=BootstrapCarryCases.Receipt;ArtifactGenerationPreparation prepared;ArtifactGenerationPreparation prior;CancellationToken token;
        var verifier=new ArtifactReceiptVerifier(new NativeContentHash());var manifests=new DocumentationManifestBuilder(verifier);var packages=new DocumentationPackageBuilder(new RecordingDocumentationPackageChecker(false),verifier,manifests);
        var validation=new ReceiptValidation(receipt);
        if(id=="NARROW"){
            var test=DesignNarrowStageRecipeCases.Cases.Select(row=>(DesignNarrowRecipeCase)row[0]).Single(test=>test.Id=="verifyToolchain");
            var e=new DesignNarrowRecipeEnvironment(test);var services=new RecordingNarrowStageServices(e);var legacy=new RecordingCompositionServices(e.Original.Architecture.Inner.Composition);
            DesignCompositionRecipeFactory Factory(Artifact? value)=>new(services,services,services,services,services,legacy,services,uiProjector:services,bootstrapReceipt:value);
            prepared=Factory(receipt).Architecture(e.Request,e.Selection).Prepare(e.Inputs);prior=Factory(null).Architecture(e.Request,e.Selection).Prepare(e.Inputs);token=e.Cancellation.Token;
        }
        else if(id=="FULL"){
            var e=new FullArchitectureRecipeEnvironment("FULL");var services=new RecordingFullArchitectureServices(e);var legacy=new RecordingCompositionServices(e.Architecture.Inner.Composition);
            DesignCompositionRecipeFactory Factory(Artifact? value)=>new(services,legacy,services,services,services,legacy,services,bootstrapReceipt:value);
            prepared=Factory(receipt).Architecture(e.Request,e.Selection,e.Snapshot,services).Prepare(e.Inputs);prior=Factory(null).Architecture(e.Request,e.Selection,e.Snapshot,services).Prepare(e.Inputs);token=e.Architecture.Inner.Cancellation.Token;
        }
        else {
            var e=new DesignDocumentationStageEnvironment(id);var services=new RecordingDesignDocumentationStageServices(e);validation.Result=e.Before;
            DesignCompositionRecipeFactory Factory(Artifact? value)=>new(services,new ModuleGraphValidator(),services,services,services,packages,services,validation,manifests,services,verifier,bootstrapReceipt:value);
            prepared=Factory(receipt).Architecture(e.Request,e.Selection,e.Snapshot,services).Prepare(e.Inputs);prior=Factory(null).Architecture(e.Request,e.Selection,e.Snapshot,services).Prepare(e.Inputs);token=e.Cancellation.Token;
        }
        var result=await prepared.MaterializeAsync(token);var found=result.Single(artifact=>artifact.RelativePath==receipt.RelativePath);
        var checksums=result.SingleOrDefault(artifact=>artifact.RelativePath=="docs/checksums.sha256");var archive=result.SingleOrDefault(artifact=>artifact.RelativePath=="package/android-mcp-design-source.zip");
        var covered=checksums is not null&&Encoding.UTF8.GetString(checksums.Bytes.Span).Contains(ContentHash.Sha256(receipt.Bytes.Span)+"  "+receipt.RelativePath+"\n",StringComparison.Ordinal);
        var packaged=false;if(archive is not null){using var stream=new MemoryStream(archive.Bytes.ToArray());using var zip=new ZipArchive(stream,ZipArchiveMode.Read);var entry=zip.GetEntry(receipt.RelativePath);if(entry is not null){using var content=entry.Open();using var bytes=new MemoryStream();await content.CopyToAsync(bytes);packaged=bytes.ToArray().SequenceEqual(receipt.Bytes.ToArray());}}
        return new(prepared.OutputPaths.Contains(receipt.RelativePath,StringComparer.Ordinal),ReferenceEquals(receipt,found),validation.Seen,covered,packaged,!prior.OutputPaths.Contains(receipt.RelativePath,StringComparer.Ordinal));
    }
    private sealed class ReceiptValidation(Artifact expected):IGeneratedDesignValidator
    {
        public bool Seen {get;private set;}
        public GeneratedDesignValidationResult? Result {get;set;}
        public GeneratedDesignValidationResult Validate(GeneratedDesignValidationInput input)
        {Assert.Same(expected,input.Artifacts.Single(artifact=>artifact.RelativePath==expected.RelativePath));Seen=true;return Result! with{Phase=input.Phase};}
    }
}
