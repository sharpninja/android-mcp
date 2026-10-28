using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

public sealed record ArtifactReferenceCase(string Id,ArtifactReferenceRequest Request,ArtifactReferenceResolution Expected,string? DecodedHtml=null);
public sealed record ArtifactReferenceRig(IArtifactReferenceResolver Resolver,IReadOnlyList<string> HtmlDecoderCalls);
public static class ArtifactReferenceCases
{
    private const string Source="docs/guide.md";
    private static ArtifactReferenceResolution L(string path,bool directory=false)=>new(ArtifactReferenceDisposition.Local,path,directory,null);
    private static ArtifactReferenceResolution I(string reason)=>new(ArtifactReferenceDisposition.Ignored,null,false,reason);
    private static ArtifactReferenceResolution X(string reason)=>new(ArtifactReferenceDisposition.Invalid,null,false,reason);
    private static ArtifactReferenceResolution R()=>new(ArtifactReferenceDisposition.Remote,null,false,"offline-external");
    private static ArtifactReferenceCase G(string id,string reference,ArtifactReferenceResolution expected)=>new(id,new(Source,reference,ArtifactReferenceProfile.GradleMarkdown),expected);
    private static ArtifactReferenceCase P(string id,string reference,ArtifactReferenceResolution expected)=>new(id,new(Source,reference,ArtifactReferenceProfile.RepositoryMarkdown),expected);
    private static ArtifactReferenceCase H(string id,string reference,ArtifactReferenceResolution expected,bool repository=false,string? decoded=null)=>new(id,new("docs/storyboards.html",reference,repository?ArtifactReferenceProfile.RepositoryOfflineAsset:ArtifactReferenceProfile.GradleOfflineAsset),expected,decoded);
    public static readonly IReadOnlyList<ArtifactReferenceCase> All=
    [
        G("REF-G01-local","page.md",L("docs/page.md")),
        G("REF-G02-parent","../README.md",L("README.md")),
        G("REF-G03-fragment","#section",I("empty")),
        G("REF-G04-angle-fragment","<../README.md#section>",L("README.md")),
        G("REF-G05-percent-literal","A%20B.md",L("docs/A%20B.md")),
        G("REF-G06-plus-literal","A+B.md",L("docs/A+B.md")),
        G("REF-G07-http-uri","https://example.test/a",I("uri")),
        G("REF-G08-data-uri","data:image/svg+xml;base64,AA==",I("uri")),
        G("REF-G09-mailto-not-exempt","mailto:owner@example.test",X("unsafe-path")),
        G("REF-G10-query-literal","../README.md?raw=1#part",L("README.md?raw=1")),
        G("REF-G11-rooted","/etc/passwd",X("rooted-path")),
        G("REF-G12-drive","C:/Windows/file.md",X("windows-drive")),
        G("REF-G13-unc","//server/share/file.md",X("rooted-path")),
        G("REF-G14-backslash","sub\\file.md",X("backslash")),
        G("REF-G15-parent-escape","../../outside.md",X("namespace-escape")),
        G("REF-G16-repeated-separator","sub//page.md",L("docs/sub/page.md")),
        P("REF-P01-parent","../README.md",L("README.md",true)),
        P("REF-P02-fragment","#section",I("empty")),
        P("REF-P03-angle-fragment","<../README.md#section>",L("README.md",true)),
        P("REF-P04-percent-decoded","A%20B.md",L("docs/A B.md",true)),
        P("REF-P05-plus-retained","A+B.md",L("docs/A+B.md",true)),
        P("REF-P06-mailto-exempt","mailto:owner@example.test",I("uri")),
        P("REF-P07-urn-exempt","urn:example:document",I("uri")),
        P("REF-P08-query-literal","../README.md?raw=1#part",L("README.md?raw=1",true)),
        P("REF-P09-root-directory","../",L("",true)),
        P("REF-P10-rooted","/etc/passwd",X("rooted-path")),
        P("REF-P11-encoded-parent-escape","%2e%2e/%2e%2e/outside.md",X("namespace-escape")),
        P("REF-P12-encoded-separator","sub%2Fpage.md",L("docs/sub/page.md",true)),
        P("REF-P13-invalid-utf8-replacement","bad%FF.md",L("docs/bad\uFFFD.md",true)),
        P("REF-P14-encoded-root","%2Fetc/passwd",X("rooted-path")),
        P("REF-P15-encoded-backslash","sub%5Cfile.md",X("backslash")),
        P("REF-P16-decode-once","file%2520name.md",L("docs/file%20name.md",true)),
        H("REF-HG01-https","https://example.test/a",R()),
        H("REF-HG02-uppercase","HTTP://example.test/a",R()),
        H("REF-HG03-protocol-relative","//example.test/a",R()),
        H("REF-HG04-no-entity-decode","&#104;ttps://example.test/a",I("asset-not-remote")),
        H("REF-HG05-data","data:image/svg+xml;base64,AA==",I("asset-not-remote")),
        H("REF-HG06-no-slash-http","http:asset",I("asset-not-remote")),
        H("REF-HP01-https","https://example.test/a",R(),true),
        H("REF-HP02-legacy-value-case","HTTP://example.test/a",I("asset-not-remote"),true),
        H("REF-HP03-protocol-relative","//example.test/a",R(),true),
        H("REF-HP04-shared-entity-decode","&#104;ttps://example.test/a",R(),true,"https://example.test/a"),
        H("REF-HP05-data","data:image/svg+xml;base64,AA==",I("asset-not-remote"),true),
        H("REF-HP06-other-scheme","ftp://example.test/a",I("asset-not-remote"),true),
        H("REF-HP07-relative-asset-not-existence-check","local.js",I("asset-not-remote"),true),
        H("REF-HP08-empty","",I("asset-not-remote"),true),
        new("REF-X01-unsafe-base",new("docs/../guide.md","page.md",ArtifactReferenceProfile.GradleMarkdown),X("invalid-base")),
        new("REF-X02-unknown-profile",new(Source,"page.md",(ArtifactReferenceProfile)999),X("unknown-profile"))
    ];
    public static IEnumerable<object[]> Cases=>All.Select(test=>new object[]{test.Id});
    public static ArtifactReferenceCase Get(string id)=>All.Single(test=>test.Id==id);
    public static void AssertContract(ArtifactReferenceCase test,ArtifactReferenceRig rig)
    {
        var before=test.Request;Assert.Equal(test.Expected,rig.Resolver.Resolve(test.Request));Assert.Equal(before,test.Request);
        Assert.Equal(test.Request.Profile==ArtifactReferenceProfile.RepositoryOfflineAsset?new[]{test.Request.Reference}:Array.Empty<string>(),rig.HtmlDecoderCalls);
    }
}
