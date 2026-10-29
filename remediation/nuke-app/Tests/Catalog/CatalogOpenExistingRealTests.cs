using System.Text;
using AndroidMcp.BuildTool.Catalog;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed class NativeCatalogOpenExistingProvider:ICatalogOpenExistingCaseProvider
{
    public CatalogOpenExistingReply Open(CatalogOpenExistingCall call)
    {
        var root=Path.Combine(Path.GetTempPath(),"catalog-existing-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        var path=call.Variant=="missing" ? Path.Combine(root,"absent-parent","fixture.sqlite") : Path.Combine(root,"fixture.sqlite");
        if(call.Variant=="invalid") File.WriteAllBytes(path,Encoding.UTF8.GetBytes("Retain these invalid SQLite fixture bytes"));
        else if(call.Variant=="existing")
        {
            using var created=CatalogDatabase.CreatePrivateFile(path,["CREATE TABLE fixture_probe(value TEXT NOT NULL);"]);
            using var insert=created.Command("INSERT INTO fixture_probe VALUES($value)",null,("$value",CatalogOpenExistingCases.Value));insert.ExecuteNonQuery();
        }
        var original=File.Exists(path) ? File.ReadAllBytes(path) : null;var parent=Path.GetDirectoryName(path)!;var parentExisted=Directory.Exists(parent);
        var priorEntries=Directory.EnumerateFileSystemEntries(root,"*",SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToArray();
        bool BytesRetained()=>original is null ? !File.Exists(path) : File.Exists(path) && File.ReadAllBytes(path).AsSpan().SequenceEqual(original);
        bool ParentRetained()=>Directory.Exists(parent)==parentExisted && priorEntries.SequenceEqual(Directory.EnumerateFileSystemEntries(root,"*",SearchOption.AllDirectories).Order(StringComparer.Ordinal),StringComparer.Ordinal);
        bool foreign;bool same;bool write;bool noSchema;string value;
        try
        {
            using var database=call.ReadOnly is null ? CatalogDatabase.OpenExisting(path) : CatalogDatabase.OpenExisting(path,call.ReadOnly.Value);
            using(var read=database.Command("SELECT value FROM fixture_probe")) Assert.Equal(CatalogOpenExistingCases.Value,(string)read.ExecuteScalar()!);
            same=Path.GetFullPath(database.Connection.DataSource)==Path.GetFullPath(path);
            using(var query=database.Command("PRAGMA foreign_keys")) foreign=(long)query.ExecuteScalar()! ==1;
            using(var query=database.Command("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name"))
            using(var rows=query.ExecuteReader()) {Assert.True(rows.Read());noSchema=rows.GetString(0)=="fixture_probe" && !rows.Read();}
            using(var transaction=database.Connection.BeginTransaction(deferred:true))
            {
                using var update=database.Command("UPDATE fixture_probe SET value='changed only inside rolled-back fixture transaction'",transaction);
                var failure=Record.Exception(()=>update.ExecuteNonQuery());write=failure is null;
                if(!write) Assert.Equal(8,Assert.IsType<SqliteException>(failure).SqliteErrorCode);
                transaction.Rollback();
            }
            using(var query=database.Command("SELECT value FROM fixture_probe")) value=(string)query.ExecuteScalar()!;
        }
        catch(SqliteException) when(call.Variant is "invalid" or "missing")
        {
            return new(call.Variant=="missing" ? "missing" : "sqlite-error",File.Exists(path),BytesRetained(),ParentRetained(),null,null,null,null,true);
        }
        return new("ok",File.Exists(path),BytesRetained(),ParentRetained(),value,write,foreign,same,noSchema);
    }
}
[Trait("Gate","Real")]
[Trait("Requirement","TEST-BUILD-004")]
public sealed class CatalogOpenExistingRealTests
{
    [Theory,MemberData(nameof(CatalogOpenExistingCases.All),MemberType=typeof(CatalogOpenExistingCases))]
    public void NativeAdmissionUsesSameSevenAssertions(CatalogOpenExistingCase test)=>CatalogOpenExistingCases.AssertCase(test,new NativeCatalogOpenExistingProvider());
}
