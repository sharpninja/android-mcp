using System.Data;
using AndroidMcp.BuildTool.Catalog;
using AndroidMcp.BuildTool.Core;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed class NativeCatalogPersistentBuildProvider:ICatalogPersistentBuildCaseProvider
{
    public CatalogPersistentBuildReply Build(CatalogPersistentBuildCall call)
    {
        CatalogDatabase? stageDatabase=null;string? stagePath=null;TrackingCatalogStore? tracking=null;
        using var provider=new NativeCatalogBuildProvider
        {
            StageFactory=path=>{stagePath=path;stageDatabase=CatalogDatabase.CreateFile(NativeSchemaProvider.Root(),CatalogSchemaProfile.Entry,path);return stageDatabase;},
            StoreFactory=path=>tracking=new(path,()=>stageDatabase?.Connection.State==ConnectionState.Closed,call.Variant=="fail-inventory-stream")
        };
        var result=provider.Build(new(call.Variant=="fail-inventory-stream" ? "base" : call.Variant));var output=Path.Combine(provider.EvidenceRoot,"out");
        var stageExists=stagePath is not null && File.Exists(stagePath);var reopened=0;
        if(stageExists)
        {using var connection=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=stagePath,Mode=SqliteOpenMode.ReadOnly,Pooling=false}.ToString());connection.Open();using var count=connection.CreateCommand();count.CommandText="SELECT count(*) FROM registry_entries";reopened=(int)(long)count.ExecuteScalar()!;}
        var final=File.Exists(Path.Combine(output,"catalog.sqlite"));var entriesPath=Path.Combine(output,"entries.jsonl");
        var entries=File.Exists(entriesPath) ? File.ReadLines(entriesPath).Count(line=>line.Length!=0) : 0;
        var equal=stageExists && final && File.ReadAllBytes(stagePath!).AsSpan().SequenceEqual(File.ReadAllBytes(Path.Combine(output,"catalog.sqlite")));
        var partialPath=Path.Combine(output,"device_inventory.jsonl");var partial=call.Variant=="fail-inventory-stream" && File.Exists(partialPath) && new FileInfo(partialPath).Length==1 ? 1 : 0;
        return new(result.Outcome,entries,reopened,stageExists,tracking?.ClosedAtFinalCopy ?? false,stageExists,equal,tracking?.StreamWrites ?? 0,tracking?.BufferedWrites ?? 0,
            tracking?.LargestWrite ?? 0,final,File.Exists(Path.Combine(output,"validation-failure.json")),partial);
    }
}
public sealed class TrackingCatalogStore(string root,Func<bool> closed,bool failInventory):IStreamingArtifactStore
{
    private readonly FileArtifactStore inner=new(root,bytes=>ContentHash.Sha256(bytes.Span));
    public int StreamWrites {get;private set;}
    public int BufferedWrites {get;private set;}
    public int LargestWrite {get;private set;}
    public bool ClosedAtFinalCopy {get;private set;}
    public Task<StoredArtifact> WriteAsync(Artifact artifact,CancellationToken cancellationToken=default)
    {BufferedWrites++;return inner.WriteAsync(artifact,cancellationToken);}
    public Task<ReadOnlyMemory<byte>> ReadAsync(string path,CancellationToken cancellationToken=default)=>inner.ReadAsync(path,cancellationToken);
    public Task<StoredArtifact> WriteStreamAsync(string path,string mediaType,Func<Stream,CancellationToken,Task> producer,CancellationToken cancellationToken=default)
    {
        StreamWrites++;if(path=="catalog.sqlite") ClosedAtFinalCopy=closed();
        return inner.WriteStreamAsync(path,mediaType,async (stream,token)=>
        {using var observed=new ObservedWriteStream(stream,length=>LargestWrite=Math.Max(LargestWrite,length),failInventory && path=="device_inventory.jsonl");await producer(observed,token);},cancellationToken);
    }
    private sealed class ObservedWriteStream(Stream inner,Action<int> observe,bool fail):Stream
    {
        private bool failed;
        public override bool CanRead=>false;public override bool CanSeek=>false;public override bool CanWrite=>true;
        public override long Length=>throw new NotSupportedException();public override long Position {get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
        public override void Flush()=>inner.Flush();public override Task FlushAsync(CancellationToken token)=>inner.FlushAsync(token);
        public override void Write(byte[] buffer,int offset,int count)=>Write(buffer.AsSpan(offset,count));
        public override void Write(ReadOnlySpan<byte> bytes)
        {
            observe(bytes.Length);if(fail){if(!failed && bytes.Length!=0){inner.Write(bytes[..1]);failed=true;}throw new IOException("Injected inventory stream failure after one retained byte");}inner.Write(bytes);
        }
        public override Task WriteAsync(byte[] buffer,int offset,int count,CancellationToken token)=>WriteAsync(buffer.AsMemory(offset,count),token).AsTask();
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> bytes,CancellationToken token=default)
        {
            observe(bytes.Length);if(fail){if(!failed && bytes.Length!=0){await inner.WriteAsync(bytes[..1],token);failed=true;}throw new IOException("Injected inventory stream failure after one retained byte");}await inner.WriteAsync(bytes,token);
        }
        public override int Read(byte[] buffer,int offset,int count)=>throw new NotSupportedException();public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException();public override void SetLength(long value)=>throw new NotSupportedException();
    }
}
[Trait("Gate","Real")]
public sealed class CatalogPersistentBuildRealTests
{
    [Theory,MemberData(nameof(CatalogPersistentBuildCases.All),MemberType=typeof(CatalogPersistentBuildCases))]
    public void NativeStageAndStreamUseSameCasesAndAssertions(CatalogPersistentBuildCase test)=>CatalogPersistentBuildCases.AssertCase(test,new NativeCatalogPersistentBuildProvider());
}
