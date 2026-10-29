using System.Text;
using AndroidMcp.BuildTool.Catalog;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed class NativePermissionExportProbe:IPermissionExportProbe
{
    public async Task<PermissionExportObservation> RunAsync(PermissionExportCase test)
    {
        var root=Directory.CreateDirectory(Path.Combine(Path.GetTempPath(),"permission-export-"+Guid.NewGuid().ToString("N"))).FullName;
        var source=Path.Combine(root,"release");var dbPath=Path.Combine(source,"catalog.sqlite");
        var contracts=NativeSchemaProvider.Root();
        using(var seed=CatalogDatabase.CreateFile(contracts,CatalogSchemaProfile.Entry,dbPath))
        {
            void Insert(string sql,params(string Name,object? Value)[] values){using var command=seed.Command(sql,null,values);command.ExecuteNonQuery();}
            foreach(var id in new[]{PermissionExportCases.S1,PermissionExportCases.S2})
                Insert("INSERT INTO sources VALUES($id,'fixture','https://example.test/source','revision','fixture',$id,34,'fixture','manifest','fixture')",("$id",id));
            Insert("INSERT INTO bindings(binding_id,signature,package,owner,kind,name,raw_signature) VALUES($id,'fixture:method','fixture','fixture.Owner','method','method','void method()')",("$id",PermissionExportCases.Binding));
            foreach(var row in PermissionExportCases.Definitions(test))Insert("INSERT INTO permission_definitions VALUES($name,$source,$level,$metadata)",
                ("$name",row.Name),("$source",row.Source),("$level",row.Protection),("$metadata",row.Metadata));
            foreach(var row in PermissionExportCases.Symbols(test))Insert("INSERT INTO permission_symbols VALUES($symbol,$name,$source,$line)",
                ("$symbol",row.Symbol),("$name",row.Name),("$source",row.Source),("$line",row.Line));
            foreach(var row in PermissionExportCases.Relations(test))Insert("INSERT INTO permission_relations(binding_id,source_id,line,logic,expression_json,raw_annotation) VALUES($binding,$source,$line,$logic,$expression,$raw)",
                ("$binding",row.Binding),("$source",row.Source),("$line",row.Line),("$logic",row.Logic),("$expression",row.Expression),("$raw",row.Raw));
        }
        var before=ContentHash.Sha256(await File.ReadAllBytesAsync(dbPath));
        var selectedSource=test.Mode=="missing"?Path.Combine(root,"missing-release"):source;
        var output=test.Mode switch{"same-root"=>source,"descendant"=>Path.Combine(source,"nested-export"),_=>Path.Combine(root,"output")};
        byte[]? existing=null;
        if(test.Mode=="nonempty"){Directory.CreateDirectory(output);existing="keep"u8.ToArray();await File.WriteAllBytesAsync(Path.Combine(output,"keep.bin"),existing);}
        if(test.Mode=="bad-schema")
        {
            contracts=Path.Combine(root,"invalid-schema");Directory.CreateDirectory(Path.Combine(contracts,"contracts"));
            await File.WriteAllTextAsync(Path.Combine(contracts,"contracts/PermissionSummary.schema.json"),"not-json");
        }
        bool? readOnly=null;
        CatalogDatabase Open(string path,bool mode){readOnly=mode;return CatalogDatabase.OpenExisting(path,mode);}
        ICreateNewStreamingArtifactStore Store(string path)
        {
            var native=new FileArtifactStore(path,value=>ContentHash.Sha256(value.Span));
            return test.Mode=="write-failure"?new SampleWriteFailure(native):native;
        }
        var operation=new PermissionExportOperation(contracts,new CanonicalJson(),new NativeContentHash(),Open,Store);
        PermissionExportResult? result=null;
        var error=await Record.ExceptionAsync(async()=>result=await operation.RunAsync(new(selectedSource,output),new CancellationToken(canceled:test.Mode=="cancel")));
        var files=new Dictionary<string,byte[]>(StringComparer.Ordinal);
        foreach(var name in new[]{"permissions.jsonl","permission-expression-samples.json","permission-coverage.json"})
        {
            var path=Path.Combine(output,name);if(File.Exists(path))files.Add(name,await File.ReadAllBytesAsync(path));
        }
        return new(Path.GetFullPath(selectedSource),before,ContentHash.Sha256(await File.ReadAllBytesAsync(dbPath)),readOnly,result,files,error,
            existing,test.Mode=="nonempty"?await File.ReadAllBytesAsync(Path.Combine(output,"keep.bin")):null);
    }
    // Fault injection delegates every real file operation to the same store.
    private sealed class SampleWriteFailure(ICreateNewStreamingArtifactStore inner):ICreateNewStreamingArtifactStore
    {
        public Task<ReadOnlyMemory<byte>> ReadAsync(string path,CancellationToken token=default)=>inner.ReadAsync(path,token);
        public Task<StoredArtifact> WriteAsync(Artifact artifact,CancellationToken token=default)=>inner.WriteAsync(artifact,token);
        public Task<StoredArtifact> WriteNewAsync(Artifact artifact,CancellationToken token=default)=>artifact.RelativePath=="permission-expression-samples.json"
            ?throw new IOException("Injected retained-output write failure"):inner.WriteNewAsync(artifact,token);
        public Task<StoredArtifact> WriteStreamAsync(string path,string media,Func<Stream,CancellationToken,Task> producer,CancellationToken token=default)=>inner.WriteStreamAsync(path,media,producer,token);
        public Task<StoredArtifact> WriteNewStreamAsync(string path,string media,Func<Stream,CancellationToken,Task> producer,CancellationToken token=default)=>inner.WriteNewStreamAsync(path,media,producer,token);
    }
}
[Trait("Gate","Real"),Trait("Requirement","TEST-BUILD-004")]
public sealed class PermissionExportRealTests
{
    [Theory,MemberData(nameof(PermissionExportCases.Data),MemberType=typeof(PermissionExportCases))]
    public Task SameAssertionsBindNativeSqliteSchemaAndImmutableStore(PermissionExportCase test)=>PermissionExportCases.AssertCase(test,new NativePermissionExportProbe());
}
