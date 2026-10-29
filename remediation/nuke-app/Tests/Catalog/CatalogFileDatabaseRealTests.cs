using System.Text;
using AndroidMcp.BuildTool.Catalog;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed class NativeCatalogFileDatabaseProvider:ICatalogFileDatabaseCaseProvider
{
    public CatalogFileDatabaseReply Execute(CatalogFileDatabaseCall call)
    {
        var root=Path.Combine(Path.GetTempPath(),"native-catalog-file-db-"+Guid.NewGuid().ToString("N"));var schema=Path.Combine(root,"schema");Directory.CreateDirectory(schema);
        var expected=new Dictionary<string,byte[]>(StringComparer.Ordinal);
        if(call.Variant!="missing-schema")
        foreach(var relative in CatalogDatabase.SchemaFiles(Enum.Parse<CatalogSchemaProfile>(call.Profile.ToString())))
        {
            var bytes=File.ReadAllBytes(Path.Combine(NativeSchemaProvider.Root(),relative));var path=Path.Combine(schema,relative);Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            if(call.Variant=="invalid-schema" && relative=="schema.sql") bytes=Encoding.UTF8.GetBytes("THIS IS NOT SQLITE DDL;");
            File.WriteAllBytes(path,bytes);expected.Add(path,bytes);
        }
        var stage=Path.Combine(root,"retained-stage.sqlite");var prior=Encoding.UTF8.GetBytes("retain these prior stage bytes unchanged");if(call.Variant=="existing") File.WriteAllBytes(stage,prior);
        bool Preserved()=>expected.All(pair=>File.ReadAllBytes(pair.Key).AsSpan().SequenceEqual(pair.Value));
        CatalogDatabase database;
        try{database=CatalogDatabase.CreateFile(schema,Enum.Parse<CatalogSchemaProfile>(call.Profile.ToString()),stage);}
        catch(FileNotFoundException){return new("prerequisite",File.Exists(stage),Preserved(),false,false,null,null,null,null,null,null,null);}
        catch(SqliteException){return new("schema-error",File.Exists(stage),Preserved(),false,false,null,null,null,null,null,null,null);}
        catch(IOException) when(call.Variant=="existing"){return new("exists",File.Exists(stage),Preserved(),File.ReadAllBytes(stage).AsSpan().SequenceEqual(prior),false,null,null,null,null,null,null,null);}
        bool fileBacked;bool immutable;bool integrity;bool foreign;bool bridge;bool metadata;
        using(database)
        {
            fileBacked=Path.GetFullPath(database.Connection.DataSource)==Path.GetFullPath(stage) && database.Connection.DataSource!=":memory:";
            using(var transaction=database.Connection.BeginTransaction())
            {
                using var insert=database.Command("INSERT INTO sources VALUES('fixture','fixture.txt','fixture-url','fixture-revision','fixture-time',$sha,37,'stable','fixture','fixture')",transaction,("$sha",new string('a',64)));
                insert.ExecuteNonQuery();transaction.Commit();
            }
            using(var change=database.Command("UPDATE sources SET path='changed' WHERE source_id='fixture'")) immutable=Record.Exception(()=>change.ExecuteNonQuery()) is SqliteException;
            using(var check=database.Command("PRAGMA integrity_check")) integrity=(string)check.ExecuteScalar()! =="ok";
            using(var check=database.Command("PRAGMA foreign_key_check")) using(var rows=check.ExecuteReader()) foreign=!rows.Read();
            bool Table(string name){using var query=database.Command("SELECT count(*) FROM sqlite_master WHERE type='table' AND name=$name",null,("$name",name));return (long)query.ExecuteScalar()! ==1;}
            bridge=Table("connection_profiles");metadata=Table("metadata_revisions");
        }
        using var reopened=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=stage,Mode=SqliteOpenMode.ReadOnly,ForeignKeys=true,Pooling=false}.ToString());reopened.Open();
        using var count=reopened.CreateCommand();count.CommandText="SELECT count(*) FROM sources";var total=(long)count.ExecuteScalar()!;
        return new("ok",File.Exists(stage),Preserved(),false,true,(int)total,integrity,foreign,immutable,bridge,metadata,fileBacked);
    }
}
[Trait("Gate","Real")]
public sealed class CatalogFileDatabaseRealTests
{
    [Theory,MemberData(nameof(CatalogFileDatabaseCases.All),MemberType=typeof(CatalogFileDatabaseCases))]
    public void NativeFileDatabaseUsesSameInputsAndAssertions(CatalogFileDatabaseCase test)=>CatalogFileDatabaseCases.AssertCase(test,new NativeCatalogFileDatabaseProvider());
}
