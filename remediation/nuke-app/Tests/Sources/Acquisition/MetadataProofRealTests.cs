using System.Text;
using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Publication;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Sources.Acquisition;

public sealed class NativeMetadataProofProbe:IMetadataProofProbe
{
    public async Task<MetadataProofObservation> RunAsync(MetadataProofCase test)
    {
        var inner=new MemoryAcquisitionStore();var receiptFields=new Dictionary<string,object?>{{"etag","\"one\""}};
        if(test.Change=="missing-etag")receiptFields.Clear();
        if(test.Change=="null-etag")receiptFields["etag"]=null;
        if(test.Change=="blank-etag")receiptFields["etag"]=" ";
        if(test.Change=="official-field"){receiptFields["official"]=true;receiptFields["archive_url"]="https://unrelated.invalid/not-authority";}
        var receipt=JsonSerializer.SerializeToUtf8Bytes(receiptFields);
        if(test.Change=="bad-json")receipt=Encoding.UTF8.GetBytes("{");
        if(test.Change=="array-json")receipt=Encoding.UTF8.GetBytes("[]");
        var xml=Encoding.UTF8.GetBytes(test.Change is "bad-xml" or "bad-xml-null-package"?"<broken":SdkVersionCases.Valid);
        if(test.Change!="missing-receipt")inner.Files[MetadataProofCases.Receipt]=new(MetadataProofCases.Receipt,receipt,"application/json");
        if(test.Change!="missing-xml")inner.Files[MetadataProofCases.Xml]=new(MetadataProofCases.Xml,xml,"application/xml");
        var originals=inner.Files.ToDictionary(item=>item.Key,item=>item.Value.Bytes.ToArray());
        var receiptPin=new StoredArtifact(MetadataProofCases.Receipt,test.Change=="receipt-hash"?new string('0',64):ContentHash.Sha256(receipt),receipt.Length+(test.Change=="receipt-size"?1:0),"application/json");
        var xmlPin=new StoredArtifact(MetadataProofCases.Xml,test.Change=="xml-hash"?new string('0',64):ContentHash.Sha256(xml),xml.Length,"application/xml");
        var pins=new List<StoredArtifact>();if(test.Change!="missing-receipt-pin")pins.Add(receiptPin);if(test.Change!="missing-xml-pin")pins.Add(xmlPin);
        var profile=test.Generic?AcquisitionCompatibilityProfile.GenericRehydrate:AcquisitionCompatibilityProfile.CanonicalRestore;
        var archive=new SourceArchivePin(new("https://example.test/sdk.zip"),"data/api-versions.xml",
            ArchiveSha256:test.Change=="declared-hash"?new string('d',64):null,SizeBytes:test.Change=="expected-size"?1:null,Compatibility:profile);
        if(test.Change=="inline")archive=archive with{ETag="wrong",RepositoryXml=Encoding.UTF8.GetBytes("<inline-wrong/>")};
        if(test.Change=="row-wins")archive=archive with{PlatformVersion="36.1"};
        if(test.Change=="wrong-platform-key")archive=archive with{PlatformVersion="37.2"};
        var source=new SourcePin("raw/api.xml",new("https://example.test/member"),"fixed",AcquisitionCases.AbcSha,3,
            GitBlob:"f2ba8f84ab5c1bce84a7b441cb1959cfc7093b7f",Archive:archive,Compatibility:profile);
        var present=test.Change!="missing-reference";var pathKind=test.Change=="null-reference"?JsonValueKind.Null:present?JsonValueKind.String:JsonValueKind.Undefined;
        var receiptPath=!present||pathKind==JsonValueKind.Null?null:test.Change=="escape"?"../outside":MetadataProofCases.Receipt;
        var platform=test.Change is "missing-platform" or "wrong-platform-key"?null:"37.2";
        var packagePresent=test.Change is "package" or "null-package" or "number-package" or "wrong-package" or "bad-xml-null-package";
        var packageKind=test.Change is "null-package" or "bad-xml-null-package"?JsonValueKind.Null:test.Change=="number-package"?JsonValueKind.Number:packagePresent?JsonValueKind.String:JsonValueKind.Undefined;
        var package=test.Change=="package"?"platforms;android-37.2":test.Change=="wrong-package"?"wrong":null;
        var reference=new RehydrationCanonicalMetadataReference(present,pathKind,receiptPath,platform,packagePresent,packageKind,package);
        var observed=new ObservedInputs(new PinnedArtifactReader(inner));var versions=new ObservedVersions();
        var service=new SnapshotMetadataProofReader(observed,pins,versions);
        SnapshotMetadataProofAdmission? result=null;var proofs=0;string? error=null;
        try{for(var index=0;index<test.Repeats;index++){result=await service.BindAsync(source,reference);if(result.Evidence is not null)proofs++;}}
        catch(TransportException problem){error=problem.Code;}
        catch(PinnedArtifactException problem){error=problem.Code;}
        catch(FileNotFoundException){error="NOT_FOUND";}
        catch(ArgumentException){error="PATH";}
        var output=error is null?result?.Pin.Archive:null;
        var preserved=result is null||result.Pin with{Archive=source.Archive}==source;
        if(result?.Pin.Archive is {} actual)
            preserved&=actual with{SizeBytes=archive.SizeBytes,ETag=archive.ETag,RepositoryXml=archive.RepositoryXml,PlatformVersion=archive.PlatformVersion,PackagePath=archive.PackagePath,ArchiveSha256=archive.ArchiveSha256,ArchiveSha1=archive.ArchiveSha1}==archive;
        var evidence=result?.Evidence;
        if(evidence is not null)preserved&=evidence.SourcePath==source.LocalPath&&evidence.SourceSha256==source.Sha256&&evidence.MetadataReceipt==receiptPin&&evidence.RepositoryXml==xmlPin&&evidence.DeclaredArchiveSha256==archive.ArchiveSha256;
        var local=evidence is null||evidence.IntegrityScope=="local-input-integrity-only"&&!evidence.OfficialSourceAuthority;
        return new(error,output?.SizeBytes,output?.ETag,output?.PlatformVersion,output?.PackagePath,observed.Reads,versions.Calls,preserved,
            originals.All(item=>inner.Files[item.Key].Bytes.Span.SequenceEqual(item.Value)),local,proofs,inner.Writes,0,
            evidence?.WholeArchiveHashVerified==true||!test.Generic&&output?.ArchiveSha256 is not null);
    }
    private sealed class ObservedInputs(IPinnedArtifactReader inner):IPinnedArtifactReader
    {
        public List<string> Reads{get;}=[];
        public Task<ReadOnlyMemory<byte>> ReadAsync(StoredArtifact pin,CancellationToken cancellationToken=default){Reads.Add(pin.RelativePath);return inner.ReadAsync(pin,cancellationToken);}
    }
    private sealed class ObservedVersions:ISdkArchivePackageVersionValidator
    {
        private readonly SdkArchiveVersionValidator inner=new();public int Calls{get;private set;}
        public long ResolveSize(ReadOnlyMemory<byte> xml,string platform,string? package=null,long? expected=null)=>inner.ResolveSize(xml,platform,package,expected);
        public long ResolvePackageSize(ReadOnlyMemory<byte> xml,string platform,SdkArchivePackageChoice package,long? expected=null)
        {Calls++;return inner.ResolvePackageSize(xml,platform,package,expected);}
    }
}
[Trait("Gate","Real")]
public sealed class MetadataProofRealTests
{
    [Theory,MemberData(nameof(MetadataProofCases.All),MemberType=typeof(MetadataProofCases))]
    public Task SameOriginalProofInputCases(MetadataProofCase test)=>MetadataProofCases.AssertCase(test,new NativeMetadataProofProbe());
}
