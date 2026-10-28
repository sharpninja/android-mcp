using System.IO.Compression;
using System.Text;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Sources.Acquisition;

public sealed record ToolCase(string Id,ToolArtifactRequest Request,byte[] Bytes,string[] Network,int Writes,string? Error,
    IReadOnlyDictionary<string,byte[]> Files,bool Cache=false,bool Proof=false,bool Conflict=false,bool CorruptCache=false,string? Checksum=null,bool CorruptProof=false);
public interface IToolArtifactProbe
{
    Task<ToolArtifactReceipt> AcquireAsync(ToolCase test);
    IReadOnlyList<string> Requests{get;}int Writes{get;}bool OriginalRetained{get;}
    Task<byte[]> ReadAsync(string path);
}
public static class ToolArtifactCases
{
    public const string BodyUrl="https://example.test/gradle.zip";
    public const string ChecksumUrl=BodyUrl+".sha256";
    public static byte[] Zip(params (string Path,byte[] Bytes,int Attributes)[] entries)
    {
        using var output=new MemoryStream();using(var archive=new ZipArchive(output,ZipArchiveMode.Create,true))foreach(var item in entries)
        {var entry=archive.CreateEntry(item.Path,CompressionLevel.NoCompression);entry.LastWriteTime=new(2000,1,1,0,0,0,TimeSpan.Zero);entry.ExternalAttributes=item.Attributes;using var stream=entry.Open();stream.Write(item.Bytes);}
        return output.ToArray();
    }
    private static byte[] BadCrc(byte[] bytes)
    {var result=bytes.ToArray();for(var i=0;i<result.Length-20;i++)if(result[i]==0x50&&result[i+1]==0x4b&&result[i+2]==1&&result[i+3]==2){Array.Clear(result,i+16,4);return result;}throw new InvalidOperationException("ZIP fixture lacks central directory.");}
    private static ToolCase Make(string id,byte[] bytes,bool zip=true,bool published=false,bool offline=false,bool cache=false,bool proof=false,
        string? error=null,int writes=0,string[]? network=null,Dictionary<string,byte[]>? files=null,bool conflict=false,bool corrupt=false,string? checksum=null,
        long expanded=512*1024*1024,long member=128*1024*1024,int entries=10000)
    {
        var pin=new ToolArtifactPin("fixture-tool","1.0",new(BodyUrl),ContentHash.Sha256(bytes),bytes.Length,zip?ToolArtifactFormat.Zip:ToolArtifactFormat.Stored,new(bytes.Length,expanded,member,entries),published?new(ChecksumUrl):null);
        return new(id,new(pin,offline?ToolArtifactAccess.Offline:ToolArtifactAccess.Online,"cache/tool.zip",zip?"tools":null,proof?"proof/retained.json":null),bytes,network??[],writes,error,files??new(),cache,proof,conflict,corrupt,checksum);
    }
    public static IEnumerable<object[]> All
    {
        get
        {
            var abc=Encoding.ASCII.GetBytes("abc");var zip=Zip(("gradle/bin/gradle",abc,0));var files=new Dictionary<string,byte[]>{{"tools/gradle/bin/gradle",abc}};
            yield return [Make("stored",abc,false,writes:1,network:[BodyUrl])];
            yield return [Make("zip-online",zip,published:true,writes:3,network:[ChecksumUrl,BodyUrl],files:files)];
            yield return [Make("cached-online-checks-published",zip,published:true,cache:true,writes:2,network:[ChecksumUrl],files:files)];
            yield return [Make("offline-proof",zip,published:true,offline:true,cache:true,proof:true,writes:1,files:files)];
            yield return [Make("offline-stored",abc,false,offline:true,cache:true)];
            yield return [Make("offline-cache-miss",zip,published:true,offline:true,proof:true,error:"TOOL_OFFLINE_MISS")];
            yield return [Make("offline-proof-miss",zip,published:true,offline:true,cache:true,error:"TOOL_CHECKSUM_PROOF")];
            yield return [Make("published-mismatch",zip,published:true,error:"TOOL_CHECKSUM",network:[ChecksumUrl],checksum:new string('0',64))];
            yield return [Make("cached-corruption",zip,cache:true,corrupt:true,error:"TOOL_HASH")];
            yield return [Make("offline-proof-binding",zip,published:true,offline:true,cache:true,proof:true,error:"TOOL_CHECKSUM_PROOF") with{CorruptProof=true}];
            yield return [Make("checksum-denied",zip,published:true,cache:true,error:"AUTH_PAUSE",network:[ChecksumUrl])];
            yield return [Make("unsafe-zip",Zip(("../escape",abc,0)),error:"ArgumentException",network:[BodyUrl])];
            yield return [Make("case-collision",Zip(("gradle/a",abc,0),("Gradle/A",abc,0)),error:"TOOL_ZIP_COLLISION",network:[BodyUrl])];
            yield return [Make("prefix-collision",Zip(("gradle",abc,0),("gradle/a",abc,0)),error:"TOOL_ZIP_COLLISION",network:[BodyUrl])];
            yield return [Make("implicit-case-collision",Zip(("gradle/A/a",abc,0),("gradle/a/b",abc,0)),error:"TOOL_ZIP_COLLISION",network:[BodyUrl])];
            yield return [Make("symlink",Zip(("gradle/a",abc,unchecked((int)0xa1ff0000))),error:"TOOL_ZIP_SPECIAL",network:[BodyUrl])];
            yield return [Make("crc",BadCrc(zip),error:"ZIP_CRC",network:[BodyUrl])];
            yield return [Make("safe-directory",Zip(("gradle/",[],unchecked((int)0x41ed0000)),("gradle/bin/gradle",abc,0)),writes:2,network:[BodyUrl],files:files)];
            yield return [Make("member-bound",zip,error:"TOOL_ZIP_LIMIT",member:2,network:[BodyUrl])];
            yield return [Make("expanded-bound",Zip(("gradle/a",abc,0),("gradle/b",abc,0)),error:"TOOL_ZIP_LIMIT",expanded:5,network:[BodyUrl])];
            yield return [Make("entry-bound",Zip(("gradle/a",abc,0),("gradle/b",abc,0)),error:"TOOL_ZIP_LIMIT",entries:1,network:[BodyUrl])];
            yield return [Make("existing-conflict",zip,conflict:true,error:"IOException",network:[BodyUrl],files:files)];
            var badUrl=Make("http",abc,false,error:"URL");yield return [badUrl with{Request=badUrl.Request with{Pin=badUrl.Request.Pin with{Url=new("http://example.test/tool")}}}];
            var badHash=Make("body-pin",abc,false,error:"TOOL_HASH",network:[BodyUrl]);yield return [badHash with{Request=badHash.Request with{Pin=badHash.Request.Pin with{Sha256=new string('0',64)}}}];
        }
    }
    public static async Task AssertCase(ToolCase test,IToolArtifactProbe provider)
    {
        var expectedNetwork=test.Network.ToList();
        if(test.Error is not null)
        {
            var error=await Record.ExceptionAsync(()=>provider.AcquireAsync(test));Assert.NotNull(error);
            if(test.Error is "ArgumentException" or "IOException")Assert.Equal(test.Error,error!.GetType().Name);
            else{Assert.IsType<TransportException>(error);Assert.Equal(test.Error,((TransportException)error!).Code);}
        }
        else
        {
            var receipt=await provider.AcquireAsync(test);Assert.Equal(test.Request.Pin,receipt.Pin);Assert.Equal(test.Request.CachePath,receipt.Artifact.RelativePath);
            Assert.Equal(test.Request.Pin.Sha256,receipt.Artifact.Sha256);Assert.Equal(test.Bytes.Length,receipt.Artifact.Length);Assert.Equal(test.Cache?"reused":"downloaded",receipt.Status);
            Assert.Equal(test.Request.Access,receipt.Access);Assert.Equal(test.Request.Pin.Url,receipt.ArtifactUrl);Assert.Equal(test.Files.Count,receipt.Files.Count);
            foreach(var file in receipt.Files){Assert.True(test.Files.ContainsKey(file.RelativePath));Assert.Equal(test.Files[file.RelativePath],await provider.ReadAsync(file.RelativePath));Assert.Equal(ContentHash.Sha256(test.Files[file.RelativePath]),file.Sha256);Assert.Equal(test.Files[file.RelativePath].Length,file.Length);}
            Assert.Equal(test.Request.Pin.PublishedChecksumUrl is not null,receipt.ChecksumProof is not null);
            Assert.Equal(test.Bytes,await provider.ReadAsync(test.Request.CachePath));
            var reused=await provider.AcquireAsync(test);Assert.Equal("reused",reused.Status);Assert.Equal(receipt.Artifact,reused.Artifact);Assert.Equal(receipt.Files,reused.Files);Assert.Equal(receipt.ChecksumProof,reused.ChecksumProof);
            if(test.Request.Access==ToolArtifactAccess.Online&&test.Request.Pin.PublishedChecksumUrl is not null)expectedNetwork.Add(test.Request.Pin.PublishedChecksumUrl.AbsoluteUri);
        }
        Assert.Equal(expectedNetwork,provider.Requests);Assert.Equal(test.Writes,provider.Writes);Assert.True(provider.OriginalRetained);
    }
}
public sealed class ScriptedToolArtifactProbe(string? mutation=null):IToolArtifactProbe
{
    private ToolCase? fixture;private int successes;
    public IReadOnlyList<string> Requests=>mutation=="requests"?[]:fixture!.Network.Concat(successes>1&&fixture.Request.Access==ToolArtifactAccess.Online&&fixture.Request.Pin.PublishedChecksumUrl is not null?new[]{fixture.Request.Pin.PublishedChecksumUrl.AbsoluteUri}:[]).Concat(mutation=="reuse-network"?new[]{ToolArtifactCases.BodyUrl}:[]).ToArray();
    public int Writes=>(fixture?.Writes??0)+(mutation=="writes"?1:0);public bool OriginalRetained=>mutation!="retained";
    public Task<byte[]> ReadAsync(string path)=>Task.FromResult(mutation=="bytes"?new byte[]{0}:path==fixture!.Request.CachePath?fixture.Bytes:fixture.Files[path]);
    public Task<ToolArtifactReceipt> AcquireAsync(ToolCase test)
    {
        fixture=test;if(test.Error is not null&&mutation!="error")
        {if(test.Error=="ArgumentException")throw new ArgumentException();if(test.Error=="IOException")throw new IOException();throw new TransportException(mutation=="code"?"wrong":test.Error,"Scripted tool admission rejection");}
        successes++;
        var files=test.Files.Select(x=>new StoredArtifact(x.Key,ContentHash.Sha256(x.Value),x.Value.Length,"application/octet-stream")).ToArray();
        var artifact=new StoredArtifact(mutation=="path"?"wrong":test.Request.CachePath,mutation=="hash"?"wrong":test.Request.Pin.Sha256,mutation=="size"?-1:test.Bytes.Length,"application/octet-stream");
        StoredArtifact? proof=test.Request.Pin.PublishedChecksumUrl is null?null:new("proof/result.json",new string('1',64),1,"application/json");if(mutation=="proof")proof=proof is null?new("wrong","wrong",1,"wrong"):null;
        return Task.FromResult(new ToolArtifactReceipt(mutation=="pin"?test.Request.Pin with{Version="wrong"}:test.Request.Pin,artifact,mutation=="files"?files.Length==0?[new("wrong","wrong",1,"wrong")]:[]:files,proof,mutation=="status"?"wrong":mutation=="reuse"&&successes>1?"downloaded":test.Cache||successes>1?"reused":"downloaded",mutation=="access"?test.Request.Access==ToolArtifactAccess.Online?ToolArtifactAccess.Offline:ToolArtifactAccess.Online:test.Request.Access,mutation=="url"?new("https://wrong.test/"):test.Request.Pin.Url));
    }
}
[Trait("Gate","Mock")]
public sealed class ToolArtifactMockTests
{
    [Theory,MemberData(nameof(ToolArtifactCases.All),MemberType=typeof(ToolArtifactCases))]
    public Task CorrectPinnedToolOutcomes(ToolCase test)=>ToolArtifactCases.AssertCase(test,new ScriptedToolArtifactProbe());
    [Theory,MemberData(nameof(ToolArtifactCases.All),MemberType=typeof(ToolArtifactCases))]
    public async Task EveryResultAndInteractionMutationRejected(ToolCase test)
    {
        var mutations=test.Error is null?new[]{"path","hash","size","pin","files","proof","status","access","url","writes","retained","bytes","reuse","reuse-network"}:test.Error is "IOException" or "ArgumentException"?new[]{"error","writes","retained"}:new[]{"error","code","writes","retained"};
        foreach(var mutation in mutations)Assert.NotNull(await Record.ExceptionAsync(()=>ToolArtifactCases.AssertCase(test,new ScriptedToolArtifactProbe(mutation))));
        if(test.Network.Length>0)Assert.NotNull(await Record.ExceptionAsync(()=>ToolArtifactCases.AssertCase(test,new ScriptedToolArtifactProbe("requests"))));
    }
}
