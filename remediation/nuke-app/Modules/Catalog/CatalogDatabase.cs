using Microsoft.Data.Sqlite;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Catalog;

public enum CatalogSchemaProfile { Entry, Bridge, Metadata, Combined }

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
    private static string[] Scripts(string catalogRoot,CatalogSchemaProfile profile)=>SchemaFiles(profile).Select(path=>File.ReadAllText(Path.Combine(catalogRoot,path))).ToArray();
    public static CatalogDatabase CreateInMemory(string catalogRoot,CatalogSchemaProfile profile)=>Open(Scripts(catalogRoot,profile),":memory:",SqliteOpenMode.ReadWriteCreate);
    public static CatalogDatabase CreateFile(string catalogRoot,CatalogSchemaProfile profile,string databasePath)
    {
        // Resolve/read every prerequisite before creating any stage file.
        var scripts=Scripts(catalogRoot,profile);var full=Path.GetFullPath(databasePath);var parent=Path.GetDirectoryName(full)!;
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
