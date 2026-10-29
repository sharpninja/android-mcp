using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Catalog;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed class NativeCatalogProducerAdmission:ICatalogProducerAdmissionProvider
{
    public async Task<CatalogProducerAdmissionReply> BuildAsync(CatalogProducerAdmissionCall call)
    {
        var current=new SourceProducerIdentityBuilder().Create();var admitted=current;
        if(call.Variant=="wrong-assembly")admitted=current with{Identity=current.Identity with{ProducerFiles=current.Identity.ProducerFiles.ToDictionary(pair=>pair.Key,pair=>pair.Key=="native/assembly" ? new string('0',64) : pair.Value,StringComparer.Ordinal)}};
        if(call.Variant=="wrong-context")admitted=current with{Identity=current.Identity with{ContextIdentity=new string('0',64)}};
        var profile=call.Variant=="release" ? SourceExtractionProfile.RELEASE011 : SourceExtractionProfile.CurrentCandidate;CatalogBuild? service=null;
        using var provider=new NativeCatalogBuildProvider
        {
            NativeAdmission=admitted,SourceProfile=profile,SourceValues=values=>values[2]=("aidl","IFoo.aidl","package a; oneway interface I { void ping(); }"),
            Execution=async invocation=>
            {
                service=invocation.Service;
                try{await service.BuildAsync(invocation.InventoryPath,invocation.OutputDirectory,invocation.StageDirectory);return null;}
                catch(CatalogBuildException error){return error;}
            }
        };
        await Task.CompletedTask;var result=provider.Build(new("typed-native-admission"));Assert.NotNull(service);
        var output=Path.Combine(provider.EvidenceRoot,"out");
        if(result.Outcome!="ok")return new(result.Outcome,service.AdmittedProducerInputs is not null,true,
            result.Outcome=="catalog-producer-admission-mismatch",!Directory.Exists(output) && !Directory.Exists(Path.Combine(provider.EvidenceRoot,"private-stage")),result.OriginalInputsRetained);
        var identity=service.AdmittedProducerInputs!;var saved=JsonNode.Parse(File.ReadAllText(Path.Combine(output,"validation.json")))!["native_producer"]!;
        var canonical=new CanonicalJson();var observed=canonical.Hash(JsonSerializer.SerializeToElement(identity.NativeProducer))==canonical.Hash(JsonSerializer.SerializeToElement(admitted.Identity));
        return new("ok",observed,identity.Profile.Id==profile.Id && saved["identity"]!["profile_id"]!.GetValue<string>()==profile.Id,
            saved["assembly_sha256"]!.GetValue<string>()==current.Identity.ProducerFiles["native/assembly"] && saved["identity"]!["native_producer"]!["context_identity"]!.GetValue<string>()==current.Identity.ContextIdentity,
            result.FinalDatabase && result.Integrity==true && result.ForeignKeys==true,result.OriginalInputsRetained);
    }
}
[Trait("Gate","Real")]
[Trait("Requirement","TEST-BUILD-004")]
[Trait("Requirement","TEST-BUILD-007")]
public sealed class CatalogProducerAdmissionRealTests
{
    [Theory,MemberData(nameof(CatalogProducerAdmissionCases.All),MemberType=typeof(CatalogProducerAdmissionCases))]
    public Task ExistingTypedAdmissionUsesSameMockQualifiedCurrentModuleAndProfileCases(CatalogProducerAdmissionCase test)=>CatalogProducerAdmissionCases.AssertCase(test,new NativeCatalogProducerAdmission());
}
