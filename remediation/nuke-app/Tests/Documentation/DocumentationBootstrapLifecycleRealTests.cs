using System.Text;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

[Trait("Gate","Real"),Trait("Requirement","FR-BUILD-005"),Trait("Requirement","TEST-BUILD-005")]
public sealed class DocumentationBootstrapLifecycleRealTests
{
    [Theory,MemberData(nameof(BootstrapLifecycleCases.Cases),MemberType=typeof(BootstrapLifecycleCases))]
    public Task SameLateCaptureManifestAndFailureCasesBindActualLinuxFiles(string scenario)=>BootstrapLifecycleCases.AssertContract(scenario,Lifecycle);
    private static async Task<BootstrapLifecycleObservation> Lifecycle(string scenario)
    {
        if(!OperatingSystem.IsLinux())throw new PlatformNotSupportedException("Actual mode boundary requires Linux");
        var root=Path.Combine(Path.GetTempPath(),"bootstrap-late-lifecycle",Guid.NewGuid().ToString("N"));
        var sourceRoot=Path.Combine(root,"source");var cacheRoot=Path.Combine(root,"cache");
        FileArtifactStore Store(string path)=>new(path,value=>ContentHash.Sha256(value.Span));
        var source=Store(sourceRoot);var cache=Store(cacheRoot);var outputRoot=Path.Combine(root,"output");var output=Store(outputRoot);
        var e=new BootstrapHookEnvironment("FULL");const string bin=".tools/gradle-8.14.3/bin/gradle";
        var gradle=await cache.WriteAsync(new(bin,"abc"u8.ToArray(),"application/octet-stream"));
        var canonical=Store(Path.Combine(DocumentationFixture.Current.Workspace,"remediation/design-c4"));
        await cache.WriteAsync(new("gradlew",await canonical.ReadAsync("gradlew"),"application/octet-stream"));
        File.SetUnixFileMode(ArtifactPaths.Resolve(cacheRoot,"gradlew"),(UnixFileMode)438);
        File.SetUnixFileMode(ArtifactPaths.Resolve(cacheRoot,bin),(UnixFileMode)416);
        e.Original.Receipts[0]=e.Original.Receipts[0] with{Files=[gradle]};
        var expected=Encoding.UTF8.GetBytes(scenario=="PRESENT-BAD"?WrapperPropertiesCases.Expected.Replace("networkTimeout=10000","networkTimeout=1",StringComparison.Ordinal):WrapperPropertiesCases.Expected);
        var propertyPath=ArtifactPaths.Resolve(sourceRoot,WrapperPropertiesCases.Path);
        if(!scenario.StartsWith("MISSING-",StringComparison.Ordinal))await source.WriteAsync(new(WrapperPropertiesCases.Path,expected,"application/octet-stream"));
        var initiallyMissing=!File.Exists(propertyPath);Artifact? captured=null;var afterMode=false;
        var native=new NativeArtifactWorkspace(cacheRoot);
        var effects=new DocumentationBootstrapFileEffects(native,native,new DocumentationWrapperPropertiesValidator(),async token=>{
            if(!OperatingSystem.IsLinux())throw new PlatformNotSupportedException("Mode observation requires Linux");
            afterMode=File.GetUnixFileMode(ArtifactPaths.Resolve(cacheRoot,"gradlew"))==(UnixFileMode)493;
            captured=new(WrapperPropertiesCases.Path,await source.ReadAsync(WrapperPropertiesCases.Path,token),"application/octet-stream");
            if(scenario=="POSTCAPTURE-CHANGE"){
                await source.WriteAsync(captured with{RelativePath="retained-original-properties.bin"},token);
                await File.WriteAllBytesAsync(propertyPath,"changed configuration"u8.ToArray(),token);
            }
            return captured;
        });
        var tools=new AcquisitionBoundary(e.Tools,async index=>{
            if(index==4&&scenario=="MISSING-CREATED")await source.WriteAsync(new(WrapperPropertiesCases.Path,expected,"application/octet-stream"));
        });
        string? error=null;var manifestHasProperties=false;
        try{
            await new DocumentationToolBootstrap(tools,effects).AcquireAsync(e.Original.Profile,ToolArtifactAccess.Online,null);
            Assert.NotNull(captured);
            var inner=new ArtifactGenerationRecipe([], _=>new([], _=>Task.FromResult<IReadOnlyList<Artifact>>([])));
            var receipts=new ArtifactReceiptVerifier(new NativeContentHash());
            var recipe=new DocumentationConfigurationRecipe(receipts).Bind(inner,[captured]);
            await new ArtifactGenerationRuntime(source,output,new FileArtifactLeaseProvider(Path.Combine(root,"leases")),receipts,new DocumentationGenerationManifestFactory(),"late-properties").ExecuteAsync("late-properties",recipe);
            var manifest=JsonNode.Parse((await output.ReadAsync("generation-manifest.json")).Span)!;
            var row=Assert.Single(manifest["inputs"]!.AsArray())!;manifestHasProperties=row["path"]!.GetValue<string>()==WrapperPropertiesCases.Path&&row["sha256"]!.GetValue<string>()==ContentHash.Sha256(expected)&&row["size_bytes"]!.GetValue<int>()==expected.Length;
        }
        catch(FileNotFoundException){error="missing";}catch(DirectoryNotFoundException){error="missing";}
        catch(ArchitectureAssetException failure){error=failure.Code;}catch(ArtifactGenerationException failure){error=failure.Code;}
        return new(error,initiallyMissing,afterMode,e.Requests.Count,manifestHasProperties,captured is null||captured.Bytes.Span.SequenceEqual(expected));
    }
    [Theory,MemberData(nameof(RetainedToolCases.Cases),MemberType=typeof(RetainedToolCases))]
    public Task SameRetainedPinObservationsBindActualStoreAndPinnedReader(string scenario)=>RetainedToolCases.AssertContract(scenario,Retained);
    private static async Task<RetainedToolObservation> Retained(string scenario)
    {
        var root=Path.Combine(Path.GetTempPath(),"bootstrap-retained-pins",Guid.NewGuid().ToString("N"));
        var store=new FileArtifactStore(root,value=>ContentHash.Sha256(value.Span));var original=RendererProfileCases.Original().Expected;
        var hash=ContentHash.Sha256("abc"u8);var profile=original with{Downloads=original.Downloads.Select(pin=>pin with{Sha256=hash,SizeBytes=3}).ToArray()};
        var receipts=new List<ToolArtifactReceipt>();const string proof="proofs/gradle.json";
        for(var index=0;index<profile.Downloads.Count;index++){
            var source=profile.Downloads[index];var zip=index==0;
            var pin=new ToolArtifactPin(source.LocalPath,source.Revision,source.Url,source.Sha256,3,zip?ToolArtifactFormat.Zip:ToolArtifactFormat.Stored,zip?new(3,1000,1000,10):new(3,0,0,0),zip?new(source.Url.AbsoluteUri+".sha256"):null);
            var artifact=new StoredArtifact(source.LocalPath,hash,3,"application/octet-stream");
            await store.WriteAsync(new("originals/"+index+".bin","abc"u8.ToArray(),"application/octet-stream"));
            if(scenario!="MISSING"||index!=0)await store.WriteAsync(new(source.LocalPath,"abc"u8.ToArray(),"application/octet-stream"));
            receipts.Add(new(pin,artifact,[],zip?new(proof,hash,3,"application/json"):null,"reused",ToolArtifactAccess.Offline,source.Url));
        }
        await store.WriteAsync(new(proof,"abc"u8.ToArray(),"application/json"));
        var reader=new RecordingReader(new PinnedArtifactReader(store));var facade=new DocumentationAdmittedToolBootstrap(profile,ToolArtifactAccess.Offline,proof,receipts,reader);
        if(scenario=="CHANGED")await File.WriteAllBytesAsync(ArtifactPaths.Resolve(root,profile.Downloads[0].LocalPath),"xyz"u8.ToArray());
        string? error=null;
        try{await facade.AcquireAsync(profile,ToolArtifactAccess.Offline,proof);}catch(PinnedArtifactException failure){error=failure.Code;}catch(FileNotFoundException){error="missing";}
        var retained=true;for(var index=0;index<profile.Downloads.Count;index++)retained&=(await store.ReadAsync("originals/"+index+".bin")).Span.SequenceEqual("abc"u8);
        return new(error,reader.Reads,retained);
    }
    private sealed class AcquisitionBoundary(IToolArtifactAcquirer inner,Func<int,Task> boundary):IToolArtifactAcquirer
    {
        private int count;
        public async Task<ToolArtifactReceipt> AcquireAsync(ToolArtifactRequest request,CancellationToken token=default)
        {var receipt=await inner.AcquireAsync(request,token);await boundary(++count);return receipt;}
    }
    private sealed class RecordingReader(IPinnedArtifactReader inner):IPinnedArtifactReader
    {
        public int Reads{get;private set;}
        public Task<ReadOnlyMemory<byte>> ReadAsync(StoredArtifact pin,CancellationToken token=default){Reads++;return inner.ReadAsync(pin,token);}
    }
}
