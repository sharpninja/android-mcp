using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Catalog;
using AndroidMcp.BuildTool.Catalog.Migration;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog.Migration;

// Adversarial configured reader/session adapters only. The shared native opener, SQLite, hash and store stay real.
public sealed class NativeMigrationStreamingProvider:IMigrationStreamingProvider
{
    private sealed class Probe
    {
        public List<Session> Sessions {get;}=[];
        public int MaxRead;
        public int Reads;
        public bool LongRead;
    }
    private sealed class Reader(IStreamingArtifactReader inner,MigrationStreamingCall call,Probe probe):IStreamingArtifactReader
    {
        public Task<ArtifactReadSnapshot?> InspectAsync(string path,CancellationToken token=default)=>inner.InspectAsync(path,token);
        public async Task<IArtifactReadSession> OpenReadAsync(string path,CancellationToken token=default)
        {
            var session=new Session(await inner.OpenReadAsync(path,token),call,probe,path=="old.sqlite");probe.Sessions.Add(session);return session;
        }
    }
    private sealed class Session:IArtifactReadSession
    {
        private readonly IArtifactReadSession inner;private readonly MigrationStreamingCall call;
        public bool Source {get;}public bool Disposed {get;private set;}public int Observes {get;private set;}
        public TrackingStream Tracking {get;}public Stream Content=>Tracking;public ArtifactReadSnapshot Opened {get;}
        public Session(IArtifactReadSession value,MigrationStreamingCall request,Probe probe,bool source)
        {
            inner=value;call=request;Source=source;var large=request.Variant==(source?"source-long-read-fault":"stage-long-read-fault");
            Opened=large?value.Opened with{Native=value.Opened.Native with{Length=(long)int.MaxValue+4096}}:value.Opened;
            Tracking=new(value.Content,request,probe,source,large);
        }
        public async Task<ArtifactReadSnapshot> ObserveCurrentPathAsync(CancellationToken token=default)
        {
            var current=await inner.ObserveCurrentPathAsync(token);Observes++;
            return Observes>=2&&call.Variant==(Source?"source-identity-change":"stage-identity-change")
                ?current with{Native=current.Native with{FileIdLow=current.Native.FileIdLow^1UL}}:current;
        }
        public async ValueTask DisposeAsync(){Disposed=true;await inner.DisposeAsync();}
    }
    private sealed class TrackingStream(Stream inner,MigrationStreamingCall call,Probe probe,bool source,bool large):Stream
    {
        public int Rewinds {get;private set;}
        public override bool CanRead=>true;public override bool CanSeek=>call.Variant!="source-nonseekable"||!source;public override bool CanWrite=>false;
        public override long Length=>large?(long)int.MaxValue+4096:inner.Length;
        public override long Position{get=>inner.Position;set{if(value==0)Rewinds++;inner.Position=value;}}
        public override int Read(byte[] buffer,int offset,int count)=>Read(buffer.AsSpan(offset,count));
        public override int Read(Span<byte> buffer)
        {
            probe.Reads++;probe.MaxRead=Math.Max(probe.MaxRead,buffer.Length);
            if(large){probe.LongRead=Length>int.MaxValue;throw new IOException(source?"Configured long source stream fault":"Configured long stage stream fault");}
            var position=inner.Position;var count=inner.Read(buffer);
            if(position==0&&count>0&&Rewinds>=2&&call.Variant==(source?"source-hash-change":"stage-hash-change"))buffer[0]^=1;
            return count;
        }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken token=default){token.ThrowIfCancellationRequested();return ValueTask.FromResult(Read(buffer.Span));}
        public override Task<int> ReadAsync(byte[] buffer,int offset,int count,CancellationToken token)=>ReadAsync(buffer.AsMemory(offset,count),token).AsTask();
        public override long Seek(long offset,SeekOrigin origin){if(origin==SeekOrigin.Begin&&offset==0)Rewinds++;return inner.Seek(offset,origin);}
        public override void Flush(){}public override void SetLength(long value)=>throw new NotSupportedException();public override void Write(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
    }
    public async Task<JsonObject> ExecuteAsync(MigrationStreamingCall call)
    {
        var published=Path.Combine(NativeCatalogMigrationProvider.Root(),"publication","android-mcp-git");
        var catalog=Path.Combine(published,"rehydration","producers","release-011");var scripts=NativeCatalogMigrationProvider.OldScripts();
        var root=Path.Combine(Path.GetTempPath(),"migration-streaming-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        var source=Path.Combine(root,"old.sqlite");var destination=Path.Combine(root,"new.sqlite");var stage=destination+".staging";
        using(var old=CatalogDatabase.CreatePrivateFile(source,scripts))
        {
            using var command=old.Command("INSERT INTO registries VALUES('r',5,'1.1.0','fixture',37,$hash,'now','published','{}');INSERT INTO active_registry VALUES(1,'r')",null,("$hash",CatalogMigrationCases.Hash));command.ExecuteNonQuery();
        }
        var original=File.ReadAllBytes(source);var probe=new Probe();CatalogMigrationResult? migrated=null;
        var migration=new CatalogCopyMigration(catalog,path=>new Reader(NativeCatalogMigrationProvider.ConfiguredReader(path),call,probe));
        var error=await Record.ExceptionAsync(async()=>migrated=await migration.MigrateAsync(source,destination));
        var outcome=error is null?"migrated":error.Message.Contains("Original source changed",StringComparison.Ordinal)?"source-changed":error.Message.Contains("Retained stage changed",StringComparison.Ordinal)?"stage-changed":error.Message.Contains("Configured long source",StringComparison.Ordinal)?"long-source-read-fault":error.Message.Contains("Configured long stage",StringComparison.Ordinal)?"long-stage-read-fault":error.Message.Contains("readable seekable",StringComparison.Ordinal)?"seekable-required":error.GetType().Name+":"+error.Message;
        var sourceSessions=probe.Sessions.Where(value=>value.Source).ToArray();var stageSessions=probe.Sessions.Where(value=>!value.Source).ToArray();
        var rewinds=call.Variant switch
        {
            "bounded-copy"=>sourceSessions.Single().Tracking.Rewinds>=3&&stageSessions.Single().Tracking.Rewinds>=4,
            "source-hash-change"=>sourceSessions.Single().Tracking.Rewinds>=2,
            "stage-hash-change"=>stageSessions.Single().Tracking.Rewinds>=2,
            _=>true
        };
        var stageRetained=!File.Exists(stage)||new FileInfo(stage).Length>0;
        if(File.Exists(stage)){using var retained=CatalogDatabase.OpenExisting(stage);using var command=retained.Command("PRAGMA integrity_check");stageRetained&=(string)command.ExecuteScalar()! =="ok";}
        var pins=true;
        if(migrated is not null)
        {
            using var sourceHash=File.OpenRead(source);using var stageHash=File.OpenRead(stage);using var finalHash=File.OpenRead(destination);
            pins=migrated.Source.Length==sourceHash.Length&&migrated.Source.Sha256==ContentHash.Sha256(sourceHash)&&migrated.RetainedStage.Length==stageHash.Length&&migrated.RetainedStage.Sha256==ContentHash.Sha256(stageHash)&&migrated.Destination.Length==finalHash.Length&&migrated.Destination.Sha256==ContentHash.Sha256(finalHash);
        }
        return new JsonObject
        {
            ["outcome"]=outcome,["source_session_opened_once"]=sourceSessions.Length==1,["stage_session_opened_once"]=stageSessions.Length==1,
            ["bounded_read_requests"]=probe.MaxRead<=131072,["same_sessions_rewound"]=rewinds,["every_session_disposed"]=probe.Sessions.All(value=>value.Disposed),
            ["source_bytes_retained"]=original.SequenceEqual(File.ReadAllBytes(source)),["stage_bytes_retained"]=stageRetained,["final_absent"]=!File.Exists(destination),
            ["long_length_reached_read_without_truncation"]=!call.Variant.Contains("long",StringComparison.Ordinal)||probe.LongRead&&probe.Reads>0,
            ["success_pins_match_actual_stream_hashes"]=pins
        };
    }
}
[Trait("Gate","Real"),Trait("Requirement","TEST-BUILD-004"),Trait("Requirement","TEST-BUILD-007")]
public sealed class CatalogMigrationStreamingRealTests
{
    [Theory,MemberData(nameof(CatalogMigrationStreamingCases.All),MemberType=typeof(CatalogMigrationStreamingCases))]
    public Task SameStreamingProbesBindNativeMigrationAndSharedIo(MigrationStreamingCase test)=>CatalogMigrationStreamingCases.AssertCase(test,new NativeMigrationStreamingProvider());
}
