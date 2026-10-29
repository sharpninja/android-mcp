using Microsoft.Data.Sqlite;
using AndroidMcp.BuildTool.Core;
using System.Text;

namespace AndroidMcp.BuildTool.Catalog;

public enum CatalogSchemaProfile { Entry, Bridge, Metadata, Combined }
internal sealed record CatalogContractSnapshot(StoredArtifact Pin,string Text);

/// <summary>One retained canonical schema engine for private memory or file-backed staging databases.</summary>
public sealed class CatalogDatabase:IDisposable
{
    public SqliteConnection Connection { get; }
    private CatalogDatabase(SqliteConnection connection)=>Connection=connection;
    public static IReadOnlyList<string> SchemaFiles(CatalogSchemaProfile profile)=>profile switch
    {
        CatalogSchemaProfile.Entry=>["schema.sql"],
        CatalogSchemaProfile.Bridge=>["bridge-config.sql"],
        CatalogSchemaProfile.Metadata=>["migrations/source_metadata_ddl.sql"],
        CatalogSchemaProfile.Combined=>["schema.sql","bridge-config.sql","migrations/source_metadata_ddl.sql"],
        _=>throw new ArgumentOutOfRangeException(nameof(profile))
    };
    internal static CatalogContractSnapshot ReadContract(string root,string relative)
    {
        var bytes=File.ReadAllBytes(ArtifactPaths.Resolve(root,relative));
        using var stream=new MemoryStream(bytes,false);using var reader=new StreamReader(stream,Encoding.UTF8,true);
        return new(new(relative,ContentHash.Sha256(bytes),bytes.LongLength,relative.EndsWith(".json",StringComparison.Ordinal) ? "application/schema+json" : "text/plain"),reader.ReadToEnd());
    }
    private static string[] Scripts(string catalogRoot,CatalogSchemaProfile profile)=>SchemaFiles(profile).Select(path=>ReadContract(catalogRoot,path).Text).ToArray();
    public static CatalogDatabase CreateInMemory(string catalogRoot,CatalogSchemaProfile profile)=>Open(Scripts(catalogRoot,profile),":memory:",SqliteOpenMode.ReadWriteCreate);
    public static CatalogDatabase CreateFile(string catalogRoot,CatalogSchemaProfile profile,string databasePath)
    {
        // Resolve/read every prerequisite before creating any stage file.
        return CreatePrivateFile(databasePath,Scripts(catalogRoot,profile));
    }
    public static CatalogDatabase OpenExisting(string databasePath,bool readOnly=true)
    {
        // ReadWrite never implies ReadWriteCreate. Admission applies no DDL and
        // cannot create a missing database or its parent directory.
        return Open([],Path.GetFullPath(databasePath),readOnly ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWrite);
    }
    internal static CatalogDatabase CreatePrivateFile(string databasePath,IReadOnlyList<string> scripts)
    {
        var full=Path.GetFullPath(databasePath);var parent=Path.GetDirectoryName(full)!;
        full=ArtifactPaths.Resolve(parent,Path.GetFileName(full));Directory.CreateDirectory(parent);
        full=ArtifactPaths.Resolve(parent,Path.GetFileName(full));
        // CreateNew refuses an earlier stage without changing its bytes. Failed
        // initialization retains this private file; it is never a final marker.
        using(var file=new FileStream(full,FileMode.CreateNew,FileAccess.Write,FileShare.None)) file.Flush(true);
        return Open(scripts,full,SqliteOpenMode.ReadWrite);
    }
    private static CatalogDatabase Open(IReadOnlyList<string> scripts,string dataSource,SqliteOpenMode mode)
    {
        var connection=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=dataSource,Mode=mode,ForeignKeys=true,Pooling=false}.ToString());
        try
        {
            connection.Open();
            foreach(var script in scripts){using var command=connection.CreateCommand();command.CommandText=script;command.ExecuteNonQuery();}
            return new(connection);
        }
        catch{connection.Dispose();throw;}
    }
    public SqliteCommand Command(string sql,SqliteTransaction? transaction=null,params (string Name,object? Value)[] parameters)
    {
        var command=Connection.CreateCommand();command.CommandText=sql;command.Transaction=transaction;
        foreach(var (name,value) in parameters) command.Parameters.AddWithValue(name,value ?? DBNull.Value);
        return command;
    }
    public void Dispose()=>Connection.Dispose();
}
