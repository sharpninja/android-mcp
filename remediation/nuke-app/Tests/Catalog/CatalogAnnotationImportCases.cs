using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed record CatalogAnnotationImportCall(string Variant,string Item,string AnnotationPlatform="37.2");
public sealed record CatalogAnnotationImportReply(string? BindingSignature,string TargetMatchKind,string Scope,int EvidenceRows,int ExternalPermissionRelations,
    bool RawAnnotationRetained,bool ValuesRetained,bool OriginalsRetained);
public sealed record CatalogAnnotationImportCase(string Id,CatalogAnnotationImportCall Call,CatalogAnnotationImportReply Expected);
public interface ICatalogAnnotationImportCaseProvider { CatalogAnnotationImportReply Import(CatalogAnnotationImportCall call); }
public static class CatalogAnnotationImportCases
{
    // Actual original SourceTargets/XmlJvmImport item spellings. Sources owns
    // archive/XML and target spelling facts; Catalog owns scoped DB attachment.
    private static CatalogAnnotationImportReply Unknown(string scope="declaration")=>new(null,"unresolved-or-ambiguous",scope,1,0,true,true,true);
    private static CatalogAnnotationImportReply Match(string signature,string kind,string scope="declaration",int relations=1)=>new(signature,kind,scope,1,relations,true,true,true);
    public static IEnumerable<object[]> All=>new[]
    {
        new CatalogAnnotationImportCase("ANN-01-formal-shadow-does-not-become-java-lang",new("shadowed-formal","a.A void go(java.lang.String)","37.0"),Unknown()),
        new CatalogAnnotationImportCase("ANN-02-current-index-exact-jvm-target",new("current-exact","a.A void go(java.lang.String)"),Match("jvm:a/A#go(Ljava/lang/String;)V","current-sdk-index-exact")),
        new CatalogAnnotationImportCase("ANN-03-known-generic-outer-erasure",new("generic-erasure","a.A a.Result<java.lang.String> call(a.Callback<T>)"),Match("jvm:a/A#call(La/Callback;)La/Result;","current-sdk-index-jvm-erasure-exact")),
        new CatalogAnnotationImportCase("ANN-04-top-level-type-variable-not-guessed",new("unknown-formal","a.A a.Result call(T)"),Unknown()),
        new CatalogAnnotationImportCase("ANN-05-current-index-not-borrowed-for-other-platform",new("generic-erasure","a.A a.Result<java.lang.String> call(a.Callback<T>)","37.0"),Unknown()),
        new CatalogAnnotationImportCase("ANN-06-class-named-ordinary-method-not-constructor",new("method-class-name","a.A int A()"),Match("jvm:a/A#A()I","current-sdk-index-exact")),
        new CatalogAnnotationImportCase("ANN-07-exact-snapshot-sdk-spelling-fallback",new("same-snapshot","a.A void go(java.lang.String)","37.0"),Match("java:a.A#go(String)","same-snapshot-sdk-signature-java-lang-spelling-equivalence")),
        new CatalogAnnotationImportCase("ANN-08-foreign-revision-cannot-supply-fallback",new("foreign-revision","a.A void go(java.lang.String)","37.0"),Unknown()),
        new CatalogAnnotationImportCase("ANN-09-source-signature-cannot-supply-sdk-fallback",new("source-signature-only","a.A void go(java.lang.String)","37.0"),Unknown()),
        new CatalogAnnotationImportCase("ANN-10-parameter-evidence-is-not-declaration-permission",new("current-exact","a.A void go(java.lang.String) 0"),Match("jvm:a/A#go(Ljava/lang/String;)V","current-sdk-index-exact","parameter:0",0))
    }.Select(test=>new object[]{test});
    public static void AssertCase(CatalogAnnotationImportCase test,ICatalogAnnotationImportCaseProvider provider)=>Assert.Equal(test.Expected,provider.Import(test.Call));
}
public sealed class ScriptedCatalogAnnotationImport(CatalogAnnotationImportCase test,int wrong=-1):ICatalogAnnotationImportCaseProvider
{
    public CatalogAnnotationImportReply Import(CatalogAnnotationImportCall call)
    {
        Assert.Same(test.Call,call);var value=test.Expected;
        return wrong switch
        {
            0=>value with{BindingSignature=value.BindingSignature is null ? "java:wrong" : null},1=>value with{TargetMatchKind="wrong"},2=>value with{Scope="wrong"},
            3=>value with{EvidenceRows=value.EvidenceRows+1},4=>value with{ExternalPermissionRelations=value.ExternalPermissionRelations+1},
            5=>value with{RawAnnotationRetained=!value.RawAnnotationRetained},6=>value with{ValuesRetained=!value.ValuesRetained},7=>value with{OriginalsRetained=!value.OriginalsRetained},_=>value
        };
    }
}
[Trait("Gate","Mock")]
[Trait("Requirement","TEST-BUILD-004")]
public sealed class CatalogAnnotationImportMockTests
{
    [Theory,MemberData(nameof(CatalogAnnotationImportCases.All),MemberType=typeof(CatalogAnnotationImportCases))]
    public void CorrectScopedAttachmentResponsesPass(CatalogAnnotationImportCase test)=>CatalogAnnotationImportCases.AssertCase(test,new ScriptedCatalogAnnotationImport(test));
    [Theory,MemberData(nameof(CatalogAnnotationImportCases.All),MemberType=typeof(CatalogAnnotationImportCases))]
    public void EveryScopeIdentityEvidenceAndPermissionMutationRejected(CatalogAnnotationImportCase test)
    {for(var wrong=0;wrong<8;wrong++) Assert.NotNull(Record.Exception(()=>CatalogAnnotationImportCases.AssertCase(test,new ScriptedCatalogAnnotationImport(test,wrong))));}
}
