using System.Collections;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Catalog;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed class NativeCatalogImportCursorProvider:ICatalogImportCursorCaseProvider
{
    public CatalogImportCursorReply Import(CatalogImportCursorCall call)
    {
        using var runtime=new SourcesRuntimeFactory().Create(SourceExtractionProfile.CurrentCandidate);var guard=new SourceWindowDispatcher(runtime.Dispatcher);
        var observations=new List<CatalogImportObservation>();using var provider=new NativeCatalogBuildProvider{SourceDispatcher=guard,ImportObserver=observations.Add};
        var result=provider.Build(new(call.Variant));var rawPath=observations.FirstOrDefault(value=>value.Event=="raw-stage")?.StagePath;var rawExists=rawPath is not null && File.Exists(rawPath);
        int Scalar(string path,string sql)
        {using var connection=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=path,Mode=SqliteOpenMode.ReadOnly,Pooling=false}.ToString());connection.Open();using var command=connection.CreateCommand();command.CommandText=sql;return (int)(long)command.ExecuteScalar()!;}
        var rawCount=rawExists ? Scalar(rawPath!,"SELECT count(*) FROM source_records") : 0;
        var main=Path.Combine(provider.EvidenceRoot,"out/catalog.sqlite");var final=File.Exists(main);
        var declarations=final ? Scalar(main,"SELECT count(*) FROM binding_declarations") : 0;
        bool? excluded=final ? Scalar(main,"SELECT count(*) FROM sqlite_master WHERE type='table' AND name IN ('source_records','source_concerns')")==0 : null;
        var batches=observations.Where(value=>value.Event=="entry-batch").ToArray();
        return new(result.Outcome,guard.Windows,guard.LateReads,rawCount,declarations,batches.Length,batches.Select(value=>value.Count).DefaultIfEmpty(0).Max(),rawExists,excluded,final,result.OriginalInputsRetained);
    }
}
public sealed class SourceWindowDispatcher(ISourcesExtractionDispatcher inner):ISourcesExtractionDispatcher
{
    private GuardedRecords? previous;
    public int Windows {get;private set;}
    public int LateReads {get;private set;}
    public IReadOnlyList<string> SupportedKinds=>inner.SupportedKinds;
    public ExtractionResult Extract(SourceDocument source,string? kind,string? scope=null)
    {
        previous?.Retire();var result=inner.Extract(source,kind,scope);Windows++;
        previous=new(result.Records,()=>LateReads++);return result with{Records=previous};
    }
    private sealed class GuardedRecords(IReadOnlyList<JsonObject> records,Action late):IReadOnlyList<JsonObject>
    {
        private bool retired;
        internal void Retire()=>retired=true;
        private void Check(){if(retired){late();throw new InvalidOperationException("A previous source record window was read after the next extraction began");}}
        public int Count {get{Check();return records.Count;}}
        public JsonObject this[int index] {get{Check();return records[index];}}
        public IEnumerator<JsonObject> GetEnumerator(){Check();for(var index=0;index<records.Count;index++){Check();yield return records[index];}}
        IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
    }
}
[Trait("Gate","Real")]
[Trait("Requirement","TEST-BUILD-004")]
public sealed class CatalogImportCursorRealTests
{
    [Theory,MemberData(nameof(CatalogImportCursorCases.All),MemberType=typeof(CatalogImportCursorCases))]
    public void NativeSourceWindowsAndRetainedSqliteUseSameAssertions(CatalogImportCursorCase test)=>CatalogImportCursorCases.AssertCase(test,new NativeCatalogImportCursorProvider());
}
