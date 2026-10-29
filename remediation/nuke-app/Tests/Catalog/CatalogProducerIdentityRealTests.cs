using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Catalog;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed class NativeCatalogProducerIdentity:ICatalogProducerIdentityProvider
{
    private sealed record Observation(string Outcome,string? Processing,string? Source,string? Registry,int ContractCount,bool ProfileCorrect,
        bool NativeCorrect,bool Retained,bool Published,bool NoPartial,bool RootIndependent);
    // Existing assembly-source seam admits another REAL loaded PE for the code
    // input vector. No binary, MVID or Python producer fingerprint is fabricated.
    private sealed class AlternateLoadedAssembly:ISourceProducerAssemblySource
    {
        public SourceProducerAssemblyObservation Capture()
        {
            var assembly=typeof(CatalogProducerIdentityCases).Assembly;
            return new(assembly.Location,assembly.ManifestModule.ModuleVersionId,File.ReadAllBytes(assembly.Location),RuntimeInformation.FrameworkDescription+"|"+RuntimeInformation.RuntimeIdentifier);
        }
    }
    private sealed class ReorderedProducer:ISourceProducerIdentityBuilder
    {
        public SourceProducerIdentityAdmission Create(SourceJavaAdmission? java=null)
        {
            var result=new SourceProducerIdentityBuilder().Create(java);
            return result with{Identity=result.Identity with{ProducerFiles=result.Identity.ProducerFiles.Reverse().ToDictionary(pair=>pair.Key,pair=>pair.Value,StringComparer.Ordinal)}};
        }
    }
    private sealed class ObservedReader(ISourceExtractGenerationReader inner):ISourceExtractGenerationReader
    {
        public int Opens {get;private set;}
        public Task<ISourceExtractGeneration> OpenAsync(SourceExtractGenerationRequest request,CancellationToken cancellationToken=default)
        {Opens++;return inner.OpenAsync(request,cancellationToken);}
    }
    public async Task<CatalogProducerIdentityReply> BuildAsync(CatalogProducerIdentityCall call)
    {
        var fixtureRoot=Path.Combine(Path.GetTempPath(),"catalog-producer-identity-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(fixtureRoot);
        var first=CopyContracts(Path.Combine(fixtureRoot,"first root"),false);var second=CopyContracts(Path.Combine(fixtureRoot,"second root"),call.Variant=="metadata-unused-missing");
        var target=call.Variant switch{"entry"=>"contracts/Entry.schema.json","permission"=>"contracts/PermissionExpression.schema.json","resolution"=>"contracts/DeviceResolution.schema.json","ddl"=>"schema.sql","metadata-used" or "metadata-unused"=>"contracts/SourceContractMetadata.schema.json",_=>null};
        if(target is not null)
        {
            var path=Path.Combine(second,target);var before=File.ReadAllBytes(path);File.WriteAllBytes(path+".before",before);
            if(target.EndsWith(".sql",StringComparison.Ordinal))File.AppendAllText(path,"\n-- Independent admitted contract byte change\n");
            else{var json=JsonNode.Parse(before)!.AsObject();json["$comment"]="Independent admitted contract byte change";File.WriteAllText(path,json.ToJsonString());}
        }
        var metadata=call.Variant is not("metadata-unused" or "metadata-unused-missing");
        var baseline=await Build(first,SourceExtractionProfile.CurrentCandidate,metadata,false,null,false,false);
        var profile=call.Variant is "profile" or "profile-mismatch" ? SourceExtractionProfile.RELEASE011 : SourceExtractionProfile.CurrentCandidate;
        ISourceProducerIdentityBuilder? identity=call.Variant=="native-code" ? new SourceProducerIdentityBuilder(new AlternateLoadedAssembly()) : call.Variant=="root-order" ? new ReorderedProducer() : null;
        var actual=await Build(second,profile,metadata,call.Variant is "intake-profile-wins" or "profile-mismatch",identity,call.Variant=="profile-mismatch",call.Variant=="contract-snapshot");
        return new(actual.Outcome,actual.Processing is not null && actual.Processing!=baseline.Processing,actual.Source is not null && actual.Source!=baseline.Source,
            actual.Registry is not null && actual.Registry!=baseline.Registry,actual.ContractCount,actual.ProfileCorrect,actual.NativeCorrect,
            baseline.Retained && actual.Retained,actual.Published,actual.NoPartial,baseline.RootIndependent && actual.RootIndependent);
    }
    private static string CopyContracts(string destination,bool omitMetadata)
    {
        var original=NativeSchemaProvider.Root();Directory.CreateDirectory(destination);
        foreach(var relative in new[]{"schema.sql","contracts/Entry.schema.json","contracts/PermissionExpression.schema.json","contracts/DeviceResolution.schema.json","contracts/SourceContractMetadata.schema.json"})
        {
            if(omitMetadata && relative=="contracts/SourceContractMetadata.schema.json")continue;
            var target=Path.Combine(destination,relative);Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(Path.Combine(original,relative),target);
            Assert.Equal(ContentHash.Sha256(File.ReadAllBytes(Path.Combine(original,relative))),ContentHash.Sha256(File.ReadAllBytes(target)));
        }
        return destination;
    }
    private static async Task<Observation> Build(string root,SourceExtractionProfile profile,bool metadata,bool useIntake,ISourceProducerIdentityBuilder? identity,bool mismatch,bool mutateAfterAdmission)
    {
        var canonical=new CanonicalJson();var factory=new SourcesRuntimeFactory();CatalogBuild? service=null;ObservedReader? reader=null;
        var selected=useIntake && !mismatch ? SourceExtractionProfile.RELEASE011 : profile;
        var expected=useIntake && mismatch ? SourceExtractionProfile.CurrentCandidate : selected;
        using var provider=new NativeCatalogBuildProvider
        {
            CatalogRootOverride=root,IncludeSourceMetadata=metadata,SourceProfile=useIntake && !mismatch ? null : profile,ProducerIdentityBuilder=identity,
            SourceValues=values=>values[2]=("aidl","IFoo.aidl","package a; oneway interface I { void ping(); }"),
            ImportObserver=observation=>
            {
                if(mutateAfterAdmission && observation.Event=="raw-stage")
                {
                    var path=Path.Combine(root,"contracts/Entry.schema.json");File.Copy(path,path+".admitted");
                    File.WriteAllText(path,new JsonObject{["not"]=new JsonObject()}.ToJsonString());
                }
            },
            IntakeFactory=!useIntake ? null : async fixture=>
            {
                var prepared=await NativeCatalogSourceCacheInput.CreateAsync(fixture,null,factory,[],profile:expected);
                var raw=new FileArtifactStore(Path.Combine(fixture.Root,"producer raw originals"),bytes=>ContentHash.Sha256(bytes.Span));
                var cache=new SourceCache(prepared.Generations,prepared.Leases,canonical);
                reader=new(new SourcesExtractService(raw,prepared.Generations,factory,cache,canonical));return new(reader,prepared.Request);
            },
            Execution=async invocation=>
            {
                service=invocation.Service;
                try{await service.BuildAsync(invocation.InventoryPath,invocation.OutputDirectory,invocation.StageDirectory);return null;}
                catch(CatalogBuildException error){return error;}
            }
        };
        await Task.CompletedTask;var result=provider.Build(new("producer-identity"));Assert.NotNull(service);
        var output=Path.Combine(provider.EvidenceRoot,"out");
        if(result.Outcome!="ok")return new(result.Outcome,null,null,null,0,mismatch && service.AdmittedProducerInputs is null,true,result.OriginalInputsRetained,false,
            !Directory.Exists(output) && !Directory.Exists(Path.Combine(provider.EvidenceRoot,"private-stage")) && reader?.Opens==0,true);
        var inputs=service.AdmittedProducerInputs!;var validation=JsonNode.Parse(File.ReadAllText(Path.Combine(output,"validation.json")))!;var producer=validation["native_producer"]!;var envelope=producer["identity"]!;
        var sources=File.ReadAllLines(Path.Combine(output,"sources.jsonl")).Select(raw=>JsonNode.Parse(raw)!.AsObject()).ToArray();var aidl=sources.Single(source=>source["path"]!.GetValue<string>()=="IFoo.aidl");
        var manifest=JsonNode.Parse(File.ReadAllText(Path.Combine(output,"device_inventory_manifest.json")))!;var files=inputs.NativeProducer.ProducerFiles;
        var expectedNative=(identity ?? new SourceProducerIdentityBuilder()).Create().Identity;
        var nativeCorrect=producer["id"]!.GetValue<string>()=="csharp-nuke-catalog-v2" && producer["historical_python_identity"]!.GetValue<bool>()==false &&
            producer["assembly_sha256"]!.GetValue<string>()==expectedNative.ProducerFiles["native/assembly"] && producer["processing_sha256"]!.GetValue<string>()==service.ProducerFingerprint &&
            files.OrderBy(pair=>pair.Key,StringComparer.Ordinal).SequenceEqual(expectedNative.ProducerFiles.OrderBy(pair=>pair.Key,StringComparer.Ordinal));
        var pins=envelope["contracts"]!.AsArray().Select(pin=>pin!["path"]!.GetValue<string>()).ToArray();
        var noRoots=!envelope.ToJsonString().Contains(root,StringComparison.Ordinal) && pins.SequenceEqual(pins.Order(StringComparer.Ordinal));
        var details=File.ReadAllLines(Path.Combine(output,"source_details.jsonl")).Select(raw=>JsonNode.Parse(raw)!["document_json"]!.GetValue<string>()).Select(raw=>JsonNode.Parse(raw)!).ToArray();
        nativeCorrect &= details.All(source=>source["x-source-identity-version"]!.GetValue<int>()==4);
        return new("ok",service.ProducerFingerprint,aidl["source_id"]!.GetValue<string>(),manifest["registry_id"]!.GetValue<string>(),inputs.ContractPins.Count,
            inputs.Profile.Id==expected.Id && envelope["profile_id"]!.GetValue<string>()==expected.Id,nativeCorrect,result.OriginalInputsRetained,result.FinalDatabase,
            result.FinalDatabase && validation["json_schema_failures"]!.AsArray().Count==0 && (!mismatch || reader?.Opens==0),noRoots);
    }
}
[Trait("Gate","Real")]
[Trait("Requirement","TEST-BUILD-004")]
[Trait("Requirement","TEST-BUILD-007")]
public sealed class CatalogProducerIdentityRealTests
{
    [Theory,MemberData(nameof(CatalogProducerIdentityCases.All),MemberType=typeof(CatalogProducerIdentityCases))]
    public Task ExistingBuildUsesSameMockProvenProducerAndCallerCases(CatalogProducerIdentityCase test)=>CatalogProducerIdentityCases.AssertCase(test,new NativeCatalogProducerIdentity());
}
