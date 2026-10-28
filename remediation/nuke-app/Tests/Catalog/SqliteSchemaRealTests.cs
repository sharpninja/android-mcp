using System.Globalization;
using System.Text.RegularExpressions;
using AndroidMcp.BuildTool.Catalog;
using AndroidMcp.BuildTool.Core;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed class NativeSchemaProvider:ISchemaCaseProvider
{
    private readonly CatalogDatabase database;
    internal static string Root()
    {
        for(var directory=new DirectoryInfo(AppContext.BaseDirectory);directory is not null;directory=directory.Parent)
        {
            var path=Path.Combine(directory.FullName,"remediation","catalog");
            if(!File.Exists(Path.Combine(path,"schema.sql"))) continue;
            foreach(var (file,hash) in new[]{("schema.sql","1da92241bbfd56070d4ac68531766a6ec411e5fd7b6710613a98666daefbd4bc"),
                ("bridge-config.sql","fb05207e7d3e5d2943ba362f313a75b60317d1493a63e92286ca8b5ea36a5e1f"),
                ("migrations/source_metadata_ddl.sql","2b585541c4463fc6b6ae408a80a573b81b635adc4206f960d2b7d8284e79a655")})
                Assert.Equal(hash,ContentHash.Sha256(File.ReadAllBytes(Path.Combine(path,file))));
            return path;
        }
        throw new FileNotFoundException("Pinned canonical catalog SQL fixtures were not found");
    }
    public NativeSchemaProvider(SchemaFixtureProfile profile)=>database=CatalogDatabase.CreateInMemory(Root(),Enum.Parse<CatalogSchemaProfile>(profile.ToString()));
    public SchemaReply Execute(SchemaCommand input)
    {
        // Legacy fixture SQL uses positional markers. The provider requires
        // named markers; these exact fixture statements contain no '?' literals.
        // This transport adaptation changes no inputs, values or assertions.
        var position=0;
        var sql=Regex.Replace(input.Sql,"\\?",_=>"$p"+(position++).ToString(CultureInfo.InvariantCulture));
        Assert.Equal(input.Parameters.Length,position);
        var parameters=input.Parameters.Select((value,index)=>(Name:"$p"+index.ToString(CultureInfo.InvariantCulture),Value:value)).ToArray();
        try
        {
            using var command=database.Command(sql,null,parameters);using var reader=command.ExecuteReader();
            var rows=new List<string?[]>();
            while(reader.Read())
                rows.Add(Enumerable.Range(0,reader.FieldCount).Select(index=>reader.IsDBNull(index) ? null : Convert.ToString(reader.GetValue(index),CultureInfo.InvariantCulture)).ToArray());
            return new(rows.ToArray());
        }
        catch(SqliteException error) { return new([],true,error.SqliteErrorCode); }
    }
    public void Dispose()=>database.Dispose();
}
[Trait("Gate","Real")]
public sealed class SqliteSchemaRealTests
{
    [Theory,MemberData(nameof(SqliteSchemaCases.All),MemberType=typeof(SqliteSchemaCases))]
    public void NativeSqliteUsesSameScriptsAndAssertions(SqliteSchemaCase test)
    { using var provider=new NativeSchemaProvider(test.Profile);SqliteSchemaCases.AssertCase(test,provider); }
}
