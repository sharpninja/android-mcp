using System.IO.Compression;
using System.Text;
using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources.Acquisition;

public sealed record SealedCacheCase(string Id,string Operation,string? Error,int Reads,int Writes);
public sealed record SealedCacheObservation(SourceCacheLookup? Lookup,SourceCachePublication? Publication,
    SourceCacheReceipt? LegacyReceipt,Exception? Error,IReadOnlyList<string> Reads,IReadOnlyList<string> Writes,
    bool OriginalsRetained,bool TokenForwarded);
public interface ISealedCacheProbe
{
    Task<SealedCacheObservation> ExecuteAsync(SealedCacheFixture fixture,CancellationToken cancellationToken);
}

// Deterministic input preparation is shared by the mock and future real adapters. No SourceCache call builds expectations.
public sealed class SealedCacheFixture
{
    public const string DataPath="cache/items.json.gz";
    public const string SidecarPath=DataPath+".csharp-cache.json";
    // Independently calculated from the documented contract3 canonical identity using native PowerShell SHA256.
    public const string Fingerprint="f24f99bc77ef32bd226f77d32addc4dfcfefe3d7dfd2055ed1da46afc30b909e";
    public required SealedCacheCase Case {get;init;}
    public SourceCacheRequest Request {get;private set;}=SourceCacheCases.Request;
    public SourceCacheSeal? Seal {get;private set;}
    public SourceCacheReceipt Receipt {get;private set;}=null!;
    public byte[] Records {get;private set;}=SourceCacheCases.Expected;
    public Dictionary<string,Artifact> Files {get;}=new(StringComparer.Ordinal);
    public IReadOnlyList<string> ExpectedReads=>Enumerable.Repeat(SidecarPath,Case.Reads).Select((path,index)=>
        Case.Operation=="lookup"&&index==1?DataPath:Case.Operation=="publish"&&Case.Id=="publish-reuse"&&index==2?DataPath:path).ToArray();
    public IReadOnlyList<string> ExpectedWrites=>Case.Writes==0?Array.Empty<string>():[DataPath,SidecarPath];
    public SourceCachePublication ExpectedPublication=>new(Receipt,PinCurrent());
    public SourceCacheLookup ExpectedLookup=>new(Records,Receipt,Seal!);

    public static SealedCacheFixture Create(SealedCacheCase test)
    {
        var fixture=new SealedCacheFixture{Case=test};
        var payload=test.Id=="hit-empty"?Array.Empty<byte>():Encoding.UTF8.GetBytes("{\"value\":1}\n");
        if(test.Id=="wire-final-lf")payload=Encoding.UTF8.GetBytes("{\"value\":1}");
        if(test.Id=="wire-blank-line")payload=Encoding.UTF8.GetBytes("\n");
        if(test.Id=="decoded-count")payload=Encoding.UTF8.GetBytes("{\"value\":1}\n{\"value\":2}\n");
        if(test.Id=="hit-empty")fixture.Records=Encoding.UTF8.GetBytes("[]");
        var compressed=GZip(payload);
        fixture.Receipt=new(DataPath,"csharp-v1","candidate",new string('1',64),AcquisitionCases.AbcSha,
            Fingerprint,ContentHash.Sha256(compressed),new string('2',64),test.Id=="hit-empty"?0:1,true);
        fixture.Files[DataPath]=new(DataPath,compressed,"application/gzip");
        fixture.SetReceipt(fixture.Receipt);
        fixture.Seal=fixture.PinCurrent();
        var seal=fixture.Seal!;
        switch(test.Id)
        {
            case "hit":fixture.Request=fixture.Request with{RecordsJson=ReadOnlyMemory<byte>.Empty};break;
            case "hit-ignore-invalid":fixture.Request=fixture.Request with{RecordsJson=new byte[]{255,0}};break;
            case "hit-empty":fixture.Request=fixture.Request with{RecordsJson=Encoding.UTF8.GetBytes("not-json")};break;
            case "seal-null":fixture.Seal=null;break;
            case "seal-data-null":fixture.Seal=seal with{Data=null!};break;
            case "seal-sidecar-null":fixture.Seal=seal with{Sidecar=null!};break;
            case "seal-data-path":fixture.Seal=seal with{Data=seal.Data with{RelativePath="elsewhere.gz"}};break;
            case "seal-sidecar-path":fixture.Seal=seal with{Sidecar=seal.Sidecar with{RelativePath="elsewhere.json"}};break;
            case "seal-data-hash":fixture.Seal=seal with{Data=seal.Data with{Sha256="bad"}};break;
            case "seal-sidecar-hash":fixture.Seal=seal with{Sidecar=seal.Sidecar with{Sha256="bad"}};break;
            case "seal-data-length":fixture.Seal=seal with{Data=seal.Data with{Length=-1}};break;
            case "seal-sidecar-length":fixture.Seal=seal with{Sidecar=seal.Sidecar with{Length=-1}};break;
            case "seal-count":fixture.Seal=seal with{RecordCount=-1};break;
            case "seal-data-media":fixture.Seal=seal with{Data=seal.Data with{MediaType="text/plain"}};break;
            case "seal-sidecar-media":fixture.Seal=seal with{Sidecar=seal.Sidecar with{MediaType="text/plain"}};break;
            case "identity-producer":fixture.Request=fixture.Request with{ProducerId="python-v1"};break;
            case "identity-profile":fixture.Request=fixture.Request with{ProfileId="other"};break;
            case "identity-recipe":fixture.Request=fixture.Request with{RecipeSha256=new string('4',64)};break;
            case "identity-source":fixture.Request=fixture.Request with{SourceSha256=new string('4',64)};break;
            case "identity-kind":fixture.Request=fixture.Request with{Kind="java"};break;
            case "identity-runtime":fixture.Request=fixture.Request with{RuntimeIdentity="other"};break;
            case "identity-code":fixture.Request=fixture.Request with{ProducerFiles=new Dictionary<string,string>{{"producer.cs",new string('4',64)}}};break;
            case "identity-historical":fixture.Request=fixture.Request with{HistoricalGZipSha256=new string('4',64)};break;
            case "sidecar-missing":fixture.Files.Remove(SidecarPath);break;
            case "sidecar-tampered":fixture.Files[SidecarPath]=new(SidecarPath,new byte[]{0},"application/json");break;
            case "sidecar-length":fixture.Seal=seal with{Sidecar=seal.Sidecar with{Length=seal.Sidecar.Length+1}};break;
            case "sidecar-self-repin":
                var replacement=GZip(Encoding.UTF8.GetBytes("{\"value\":9}\n"));
                fixture.Files[DataPath]=new(DataPath,replacement,"application/gzip");
                fixture.SetReceipt(fixture.Receipt with{GZipSha256=ContentHash.Sha256(replacement)});break;
            case "data-missing":fixture.Files.Remove(DataPath);break;
            case "data-tampered":fixture.Files[DataPath]=new(DataPath,new byte[]{0},"application/gzip");break;
            case "data-length":fixture.Seal=seal with{Data=seal.Data with{Length=seal.Data.Length+1}};break;
            case "receipt-count":fixture.SetReceipt(fixture.Receipt with{RecordCount=2});fixture.Seal=fixture.PinCurrent() with{RecordCount=1};break;
            case "receipt-gzip-pin":fixture.SetReceipt(fixture.Receipt with{GZipSha256=new string('4',64)});fixture.Seal=fixture.PinCurrent();break;
            case "strict-history":fixture.Request=fixture.Request with{StrictHistoricalBytes=true};break;
            case "publish-fresh":case "legacy-write":fixture.Files.Clear();break;
            case "publish-conflict":fixture.SetReceipt(fixture.Receipt with{ProfileId="other"});break;
        }
        return fixture;
    }
    private void SetReceipt(SourceCacheReceipt receipt)=>Files[SidecarPath]=new(SidecarPath,
        new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(receipt)),"application/json");
    private SourceCacheSeal PinCurrent()=>new(Pin(Files[DataPath]),Pin(Files[SidecarPath]),Receipt.RecordCount);
    private static StoredArtifact Pin(Artifact artifact)=>new(artifact.RelativePath,ContentHash.Sha256(artifact.Bytes.Span),artifact.Bytes.Length,artifact.MediaType);
    public static byte[] GZip(byte[] bytes)
    {
        using var output=new MemoryStream();using(var gzip=new GZipStream(output,CompressionLevel.Optimal,true))gzip.Write(bytes);return output.ToArray();
    }
    public SourceCachePublication PublicationExpectation()
    {
        // Fresh publication pins come from independently prepared fixtures, never the implementation output.
        var clean=Create(new("publish-reuse","publish",null,3,0));return clean.ExpectedPublication;
    }
}

public static class SealedCacheCases
{
    public static IEnumerable<object[]> All=>Cases.Select(test=>new object[]{test});
    public static IReadOnlyList<SealedCacheCase> Cases {get;}=Build();
    private static IReadOnlyList<SealedCacheCase> Build()
    {
        var cases=new List<SealedCacheCase>();
        foreach(var id in new[]{"hit","hit-ignore-invalid","hit-empty"})cases.Add(new(id,"lookup",null,2,0));
        foreach(var id in new[]{"seal-null","seal-data-null","seal-sidecar-null","seal-data-path","seal-sidecar-path","seal-data-hash","seal-sidecar-hash","seal-data-length","seal-sidecar-length","seal-count","seal-data-media","seal-sidecar-media"})cases.Add(new(id,"lookup","CACHE_SEAL",0,0));
        cases.Add(new("identity-producer","lookup","CACHE_IDENTITY",0,0));
        foreach(var id in new[]{"identity-profile","identity-recipe","identity-source","identity-kind","identity-runtime","identity-code","identity-historical"})cases.Add(new(id,"lookup","CACHE_IDENTITY",1,0));
        cases.Add(new("sidecar-missing","lookup","FileNotFoundException",1,0));
        foreach(var id in new[]{"sidecar-tampered","sidecar-length","sidecar-self-repin"})cases.Add(new(id,"lookup","CACHE_SIDECAR_HASH",1,0));
        cases.Add(new("data-missing","lookup","FileNotFoundException",2,0));
        foreach(var id in new[]{"data-tampered","data-length"})cases.Add(new(id,"lookup","CACHE_HASH",2,0));
        cases.Add(new("receipt-count","lookup","CACHE_RECORDS",1,0));
        cases.Add(new("receipt-gzip-pin","lookup","CACHE_IDENTITY",1,0));
        foreach(var id in new[]{"decoded-count","wire-final-lf","wire-blank-line"})cases.Add(new(id,"lookup","CACHE_RECORDS",2,0));
        cases.Add(new("strict-history","lookup","CACHE_BYTES",2,0));
        cases.Add(new("cancelled","lookup","OperationCanceledException",0,0));
        cases.Add(new("publish-fresh","publish",null,1,2));
        cases.Add(new("publish-reuse","publish",null,3,0));
        cases.Add(new("legacy-write","write",null,1,2));
        cases.Add(new("publish-conflict","publish","CACHE_IDENTITY",1,0));
        return cases;
    }
    public static async Task AssertCase(SealedCacheCase test,ISealedCacheProbe probe)
    {
        var fixture=SealedCacheFixture.Create(test);using var cancel=new CancellationTokenSource();if(test.Id=="cancelled")cancel.Cancel();
        var result=await probe.ExecuteAsync(fixture,cancel.Token);
        Assert.Equal(fixture.ExpectedReads,result.Reads);Assert.Equal(fixture.ExpectedWrites,result.Writes);
        Assert.True(result.OriginalsRetained);Assert.True(result.TokenForwarded);
        if(test.Error is not null)
        {
            Assert.NotNull(result.Error);Assert.Null(result.Lookup);Assert.Null(result.Publication);Assert.Null(result.LegacyReceipt);
            if(test.Error=="FileNotFoundException")Assert.IsType<FileNotFoundException>(result.Error);
            else if(test.Error=="OperationCanceledException")Assert.IsAssignableFrom<OperationCanceledException>(result.Error);
            else{var failure=Assert.IsType<TransportException>(result.Error);Assert.Equal(test.Error,failure.Code);}
            return;
        }
        Assert.Null(result.Error);
        if(test.Operation=="lookup")
        {
            var lookup=Assert.IsType<SourceCacheLookup>(result.Lookup);Assert.Equal(fixture.Records,lookup.RecordsJson.ToArray());
            AssertReceipt(fixture.Receipt,lookup.Receipt);AssertSeal(fixture.Seal!,lookup.Seal);Assert.Null(result.Publication);Assert.Null(result.LegacyReceipt);
        }
        else if(test.Operation=="publish")
        {
            var publication=Assert.IsType<SourceCachePublication>(result.Publication);var expected=fixture.PublicationExpectation();
            AssertReceipt(expected.Receipt,publication.Receipt);AssertSeal(expected.Seal,publication.Seal);Assert.Null(result.Lookup);Assert.Null(result.LegacyReceipt);
        }
        else{AssertReceipt(fixture.PublicationExpectation().Receipt,Assert.IsType<SourceCacheReceipt>(result.LegacyReceipt));Assert.Null(result.Lookup);Assert.Null(result.Publication);}
    }
    public static void AssertReceipt(SourceCacheReceipt expected,SourceCacheReceipt actual)=>Assert.Equal(expected,actual);
    public static void AssertSeal(SourceCacheSeal expected,SourceCacheSeal actual)=>Assert.Equal(expected,actual);
}

public sealed class ScriptedSealedCacheProbe(string? mutation=null):ISealedCacheProbe
{
    public Task<SealedCacheObservation> ExecuteAsync(SealedCacheFixture fixture,CancellationToken cancellationToken)
    {
        SourceCacheLookup? lookup=null;SourceCachePublication? publication=null;SourceCacheReceipt? legacy=null;Exception? error=null;
        if(fixture.Case.Error is not null)error=fixture.Case.Error switch
        {"FileNotFoundException"=>new FileNotFoundException(),"OperationCanceledException"=>new OperationCanceledException(cancellationToken),_=>new TransportException(mutation=="error-code"?"wrong":fixture.Case.Error,"Scripted rejection")};
        else if(fixture.Case.Operation=="lookup")lookup=fixture.ExpectedLookup;
        else if(fixture.Case.Operation=="publish")publication=fixture.PublicationExpectation();
        else legacy=fixture.PublicationExpectation().Receipt;
        SourceCacheReceipt ChangeReceipt(SourceCacheReceipt receipt)=>mutation switch
        {
            "receipt-path"=>receipt with{Path="wrong"},"receipt-producer"=>receipt with{ProducerId="python"},"receipt-profile"=>receipt with{ProfileId="wrong"},
            "receipt-recipe"=>receipt with{RecipeSha256="wrong"},"receipt-source"=>receipt with{SourceSha256="wrong"},"receipt-fingerprint"=>receipt with{ProducerFingerprint="wrong"},
            "receipt-gzip"=>receipt with{GZipSha256="wrong"},"receipt-historical"=>receipt with{HistoricalGZipSha256=null},"receipt-count"=>receipt with{RecordCount=99},"receipt-derived"=>receipt with{DerivedGeneration=false},_=>receipt
        };
        SourceCacheSeal ChangeSeal(SourceCacheSeal seal)=>mutation switch
        {
            "seal-data-path"=>seal with{Data=seal.Data with{RelativePath="wrong"}},"seal-data-sha"=>seal with{Data=seal.Data with{Sha256="wrong"}},
            "seal-data-length"=>seal with{Data=seal.Data with{Length=99}},"seal-data-media"=>seal with{Data=seal.Data with{MediaType="wrong"}},
            "seal-sidecar-path"=>seal with{Sidecar=seal.Sidecar with{RelativePath="wrong"}},"seal-sidecar-sha"=>seal with{Sidecar=seal.Sidecar with{Sha256="wrong"}},
            "seal-sidecar-length"=>seal with{Sidecar=seal.Sidecar with{Length=99}},"seal-sidecar-media"=>seal with{Sidecar=seal.Sidecar with{MediaType="wrong"}},"seal-count"=>seal with{RecordCount=99},_=>seal
        };
        if(lookup is not null)lookup=lookup with{Receipt=ChangeReceipt(lookup.Receipt),Seal=ChangeSeal(lookup.Seal),RecordsJson=mutation=="records"?new byte[]{0}:lookup.RecordsJson};
        if(publication is not null)publication=publication with{Receipt=ChangeReceipt(publication.Receipt),Seal=ChangeSeal(publication.Seal)};
        if(legacy is not null)legacy=ChangeReceipt(legacy);
        if(mutation=="error-omitted")error=null;if(mutation=="error-type")error=new IOException("wrong type");
        if(mutation=="error-added")error=new IOException("unexpected");
        if(mutation=="result-omitted"){lookup=null;publication=null;legacy=null;}
        if(mutation=="error-result")lookup=fixture.ExpectedLookup;
        return Task.FromResult(new SealedCacheObservation(lookup,publication,legacy,error,
            mutation=="reads"?new[]{"wrong"}:fixture.ExpectedReads,mutation=="writes"?new[]{"wrong"}:fixture.ExpectedWrites,
            mutation!="originals",mutation!="token"));
    }
}

[Trait("Gate","Mock")]
public sealed class SealedCacheMockTests
{
    [Theory,MemberData(nameof(SealedCacheCases.All),MemberType=typeof(SealedCacheCases))]
    public Task CorrectSealedAdmissionAndPublication(SealedCacheCase test)=>SealedCacheCases.AssertCase(test,new ScriptedSealedCacheProbe());
    [Theory,MemberData(nameof(SealedCacheCases.All),MemberType=typeof(SealedCacheCases))]
    public async Task IndividualPropertyErrorAndInteractionMutationsRejected(SealedCacheCase test)
    {
        var mutations=new List<string>{"reads","writes","originals","token"};
        if(test.Error is not null)
        {
            mutations.AddRange(["error-omitted","error-type","error-result"]);
            if(test.Error is not ("FileNotFoundException" or "OperationCanceledException"))mutations.Add("error-code");
        }
        else
        {
            mutations.AddRange(["error-added","result-omitted","receipt-path","receipt-producer","receipt-profile","receipt-recipe","receipt-source","receipt-fingerprint","receipt-gzip","receipt-historical","receipt-count","receipt-derived"]);
            if(test.Operation!="write")mutations.AddRange(["seal-data-path","seal-data-sha","seal-data-length","seal-data-media","seal-sidecar-path","seal-sidecar-sha","seal-sidecar-length","seal-sidecar-media","seal-count"]);
            if(test.Operation=="lookup")mutations.Add("records");
        }
        foreach(var mutation in mutations)Assert.NotNull(await Record.ExceptionAsync(()=>SealedCacheCases.AssertCase(test,new ScriptedSealedCacheProbe(mutation))));
    }
}
