using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed record CatalogBuildCall(string Variant="base");
public sealed record CatalogBuildReply(string Outcome,bool FinalDatabase,bool UnpublishedFailure,int Sources,int Bindings,int Occurrences,int Declarations,
    int PermissionDefinitions,int Entries,int Inventory,int GoIntOccurrences,bool? Integrity,bool? ForeignKeys,bool? EntrySchemas,bool? PermissionSchemas,
    bool? InventorySchemas,bool? InventoryStreamHash,bool? ExportDatabaseAgreement,bool? ImmutableBindings,bool? DefaultDeny,bool SourceFingerprintNative,
    bool OriginalInputsRetained,bool OutputRefusedWithoutMutation,bool? KnownCameraPermission=null);
public sealed record CatalogBuildCase(string Id,CatalogBuildCall Call,CatalogBuildReply Expected);
public interface ICatalogBuildCaseProvider:IDisposable { CatalogBuildReply Build(CatalogBuildCall call); }
public static class CatalogBuildCases
{
    // Exact test_build.BuildTests.fixture input bytes, no old producer execution.
    public const string Signature="""
    // Signature format: 6.0
    package android {
     public class Manifest.permission {
     field public static final String CAMERA="android.permission.CAMERA";
     }
    }
    package a {
     public class A {
     ctor public A();
     method @RequiresPermission(android.Manifest.permission.CAMERA) public void go(int);
     method public void go(String);
     }
    }
    """;
    public const string Manifest="<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\"><permission android:name=\"android.permission.CAMERA\" android:protectionLevel=\"dangerous\"/></manifest>";
    public const string Aidl="package a; @RequiresNoPermission interface IFoo { @EnforcePermission(\"android.permission.CAMERA\") void go(in int n)=4; }";
    public static string Sha(string value)=>Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static CatalogBuildReply Base=>new("ok",true,false,3,10,10,10,1,10,10,1,true,true,true,true,true,true,true,true,true,true,true,false,KnownCameraPermission:true);
    public static CatalogBuildReply Failure(string outcome,bool preserved=true)=>new(outcome,false,true,0,0,0,0,0,0,0,0,null,null,null,null,null,null,null,null,null,true,preserved,false);
    public static IEnumerable<object[]> All=>new[]
    {
        new CatalogBuildCase("BUILD-01-source-fixture-through-sealed-catalog",new(),Base),
        new CatalogBuildCase("BUILD-02-source-hash-failure-unpublished",new("source-sha-mismatch"),Failure("source-sha-mismatch")),
        new CatalogBuildCase("BUILD-03-git-blob-framing-failure-unpublished",new("git-blob-mismatch"),Failure("git-blob-mismatch")),
        new CatalogBuildCase("BUILD-04-invalid-permission-ast-unpublished",new("invalid-permission-ast"),Failure("invalid-permission-ast")),
        new CatalogBuildCase("BUILD-05-same-identity-two-immutable-occurrences",new("duplicate-source"),Base with{Sources=4,Occurrences=18,Declarations=18,GoIntOccurrences=2}),
        new CatalogBuildCase("BUILD-06-nonempty-output-refused-preserved",new("nonempty-output"),Failure("nonempty-output") with{UnpublishedFailure=false,OutputRefusedWithoutMutation=true}),
        new CatalogBuildCase("BUILD-07-source-cache-hash-failure-unpublished",new("cache-sha-mismatch"),Failure("cache-sha-mismatch"))
    }.Select(test=>new object[]{test});
    public static void AssertCase(CatalogBuildCase test,ICatalogBuildCaseProvider provider)=>Assert.Equal(test.Expected,provider.Build(test.Call));
}
public sealed class ScriptedCatalogBuild(CatalogBuildCase test,int corrupt=-1):ICatalogBuildCaseProvider
{
    public CatalogBuildReply Build(CatalogBuildCall call)
    {
        Assert.Same(test.Call,call);var value=test.Expected;
        static bool? Flip(bool? input)=>input is null ? true : !input;
        return corrupt switch
        {
            0=>value with{Outcome="wrong"},1=>value with{FinalDatabase=!value.FinalDatabase},2=>value with{UnpublishedFailure=!value.UnpublishedFailure},
            3=>value with{Sources=value.Sources+1},4=>value with{Bindings=value.Bindings+1},5=>value with{Occurrences=value.Occurrences+1},6=>value with{Declarations=value.Declarations+1},
            7=>value with{PermissionDefinitions=value.PermissionDefinitions+1},8=>value with{Entries=value.Entries+1},9=>value with{Inventory=value.Inventory+1},10=>value with{GoIntOccurrences=value.GoIntOccurrences+1},
            11=>value with{Integrity=Flip(value.Integrity)},12=>value with{ForeignKeys=Flip(value.ForeignKeys)},13=>value with{EntrySchemas=Flip(value.EntrySchemas)},14=>value with{PermissionSchemas=Flip(value.PermissionSchemas)},
            15=>value with{InventorySchemas=Flip(value.InventorySchemas)},16=>value with{InventoryStreamHash=Flip(value.InventoryStreamHash)},17=>value with{ExportDatabaseAgreement=Flip(value.ExportDatabaseAgreement)},
            18=>value with{ImmutableBindings=Flip(value.ImmutableBindings)},19=>value with{DefaultDeny=Flip(value.DefaultDeny)},20=>value with{SourceFingerprintNative=!value.SourceFingerprintNative},
            21=>value with{OriginalInputsRetained=!value.OriginalInputsRetained},22=>value with{OutputRefusedWithoutMutation=!value.OutputRefusedWithoutMutation},23=>value with{KnownCameraPermission=Flip(value.KnownCameraPermission)},_=>value
        };
    }
    public void Dispose() { }
}
[Trait("Gate","Mock")]
public sealed class CatalogBuildMockTests
{
    [Theory,MemberData(nameof(CatalogBuildCases.All),MemberType=typeof(CatalogBuildCases))]
    public void CorrectEndToEndResponsesPass(CatalogBuildCase test)=>CatalogBuildCases.AssertCase(test,new ScriptedCatalogBuild(test));
    [Theory,MemberData(nameof(CatalogBuildCases.All),MemberType=typeof(CatalogBuildCases))]
    public void EveryArtifactCountIntegrityAndPublicationMutationRejected(CatalogBuildCase test)
    {for(var field=0;field<24;field++) Assert.NotNull(Record.Exception(()=>CatalogBuildCases.AssertCase(test,new ScriptedCatalogBuild(test,field))));}
}
