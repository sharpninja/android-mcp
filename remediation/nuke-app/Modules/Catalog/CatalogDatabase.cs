using Microsoft.Data.Sqlite;

namespace AndroidMcp.BuildTool.Catalog;

public enum CatalogSchemaProfile { Entry, Bridge, Metadata, Combined }

/// <summary>Private offline SQLite fixture database using the retained canonical DDL.</summary>
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
    public static CatalogDatabase CreateInMemory(string catalogRoot,CatalogSchemaProfile profile)
    {
        // Resolve and read all prerequisites before opening the private database.
        var scripts=SchemaFiles(profile).Select(path=>File.ReadAllText(Path.Combine(catalogRoot,path))).ToArray();
        var connection=new SqliteConnection(new SqliteConnectionStringBuilder
        { DataSource=":memory:",ForeignKeys=true,Pooling=false }.ToString());
        try
        {
            connection.Open();
            foreach(var script in scripts)
            {
                using var command=connection.CreateCommand();command.CommandText=script;command.ExecuteNonQuery();
            }
            return new(connection);
        }
        catch { connection.Dispose();throw; }
    }
    public SqliteCommand Command(string sql,SqliteTransaction? transaction=null,params (string Name,object? Value)[] parameters)
    {
        var command=Connection.CreateCommand();command.CommandText=sql;command.Transaction=transaction;
        foreach(var (name,value) in parameters) command.Parameters.AddWithValue(name,value ?? DBNull.Value);
        return command;
    }
    public void Dispose()=>Connection.Dispose();
}
