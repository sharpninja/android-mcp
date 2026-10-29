using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources.Java;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources.Java;

public static class JavaWorkflowCase
{
    public static readonly JavaNativeWorkflowRequest Request=new("/fixtures/java-generation",new("/fixtures/cc",new string('a',64),"fixture-compiler"));
    public static JsonObject SourceLock()=>JavaContractCases.Object(("schema","androidmcp-java-native-source-lock-v1"),
        ("runtime_version","0.25.2"),("runtime_tag_object","695d1530e59804279d9c739335b73b882d9f1caf"),("runtime_commit",JavaNativeBuildCases.RuntimeCommit),
        ("grammar_version","0.23.5"),("grammar_commit",JavaNativeBuildCases.GrammarCommit),("runtime_archive_sha256",new string('b',64)),("grammar_archive_sha256",new string('c',64)),
        ("runtime_archive_metadata",JavaContractCases.Array(JavaContractCases.Object(("Name","pax_global_header"),("EntryType","GlobalExtendedAttributes"),("Attributes",JavaContractCases.Object(("comment",JavaNativeBuildCases.RuntimeCommit)))))),
        ("grammar_archive_metadata",new JsonArray()),
        ("rid","linux-x64"),("compiler",JavaContractCases.Object(("ExecutablePath",Request.Compiler.ExecutablePath),("Sha256",Request.Compiler.Sha256),("Version",Request.Compiler.Version))),
        ("native_artifact",JavaContractCases.Object(("relative_path","native/linux-x64/libandroidmcp_java_parser.so"),("sha256",new string('d',64)),("language_abi",14))),
        ("source_files",JavaContractCases.Array(JavaContractCases.Object(("path","runtime/LICENSE"),("sha256",new string('e',64)),("length",100)),
            JavaContractCases.Object(("path","java/LICENSE"),("sha256",new string('f',64)),("length",101)))));
    public static JavaNativeWorkflowResult Correct()=>new(new("native-source-lock.json",new string('1',64),1024,"application/json"),
        new(new("native/linux-x64/libandroidmcp_java_parser.so",new string('d',64),4096,"application/x-sharedlib"),
            new(Request.Compiler,[],"/fixtures/work",new Dictionary<string,string>(),"/fixtures/receipts"),
            new(0,"out","err",DateTimeOffset.UnixEpoch,DateTimeOffset.UnixEpoch,false),JavaNativeBuildCases.RuntimeCommit,JavaNativeBuildCases.GrammarCommit,14),SourceLock());
    public static async Task AssertContract(IJavaNativeWorkflow provider,JavaNativeWorkflowRequest? request=null)
    {
        request??=Request;var result=await provider.RunAsync(request);
        AssertResult(request,result);
    }
    public static void AssertResult(JavaNativeWorkflowRequest request,JavaNativeWorkflowResult result)
    {
        Assert.Equal("native-source-lock.json",result.Manifest.RelativePath);Assert.Equal("application/json",result.Manifest.MediaType);Assert.Matches("^[a-f0-9]{64}$",result.Manifest.Sha256);Assert.True(result.Manifest.Length>0);
        var expected=SourceLock();expected["compiler"]=System.Text.Json.JsonSerializer.SerializeToNode(request.Compiler);
        // Archive and artifact content are fixture-specific. Their exact hashes
        // are tested at the byte transport/store/builder seams; this contract
        // requires explicit distinct identities and complete source metadata.
        foreach(var property in new[]{"runtime_archive_sha256","grammar_archive_sha256","runtime_archive_metadata","grammar_archive_metadata","source_files","native_artifact"})expected.Remove(property);
        SourceExtractionContractCases.AssertSubset(expected,result.SourceLock);
        foreach(var field in new[]{"runtime_archive_sha256","grammar_archive_sha256"})Assert.Matches("^[a-f0-9]{64}$",result.SourceLock[field]!.GetValue<string>());
        foreach(var (field,commit) in new[]{("runtime_archive_metadata",JavaNativeBuildCases.RuntimeCommit),("grammar_archive_metadata",JavaNativeBuildCases.GrammarCommit)})
        {
            var metadata=result.SourceLock[field]!.AsArray();if(field=="runtime_archive_metadata")Assert.NotEmpty(metadata);
            foreach(var entry in metadata)
            {
                Assert.False(string.IsNullOrWhiteSpace(entry!["Name"]!.GetValue<string>()));if(field=="runtime_archive_metadata")Assert.Equal("pax_global_header",entry["Name"]!.GetValue<string>());Assert.Equal("GlobalExtendedAttributes",entry["EntryType"]!.GetValue<string>());
                var attributes=entry["Attributes"]!.AsObject();Assert.Equal(commit,attributes["comment"]!.GetValue<string>());Assert.False(attributes.ContainsKey("path"));Assert.False(attributes.ContainsKey("linkpath"));
            }
        }
        Assert.Equal(result.Build.Artifact.Sha256,result.SourceLock["native_artifact"]!["sha256"]!.GetValue<string>());
        Assert.Equal(result.Build.Artifact.RelativePath,result.SourceLock["native_artifact"]!["relative_path"]!.GetValue<string>());
        Assert.Equal(14,result.SourceLock["native_artifact"]!["language_abi"]!.GetValue<int>());
        var files=result.SourceLock["source_files"]!.AsArray();Assert.NotEmpty(files);
        Assert.Equal(files.Count,files.Select(x=>x!["path"]!.GetValue<string>()).Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(files,x=>x!["path"]!.GetValue<string>()=="runtime/LICENSE");Assert.Contains(files,x=>x!["path"]!.GetValue<string>()=="java/LICENSE");
        foreach(var file in files){Assert.Matches("^[a-f0-9]{64}$",file!["sha256"]!.GetValue<string>());Assert.True(file["length"]!.GetValue<long>()>0);}
        Assert.Equal(request.Compiler,result.Build.Compilation.Tool);Assert.Equal(JavaNativeBuildCases.RuntimeCommit,result.Build.RuntimeCommit);Assert.Equal(JavaNativeBuildCases.GrammarCommit,result.Build.GrammarCommit);
        Assert.Equal(14,result.Build.LanguageAbi);Assert.Equal(0,result.Build.Process.ExitCode);Assert.False(result.Build.Process.Cancelled);
    }
}
internal sealed class ScriptedJavaWorkflow(Func<JavaNativeWorkflowRequest,JavaNativeWorkflowResult> response):IJavaNativeWorkflow
{
    public Task<JavaNativeWorkflowResult> RunAsync(JavaNativeWorkflowRequest request,CancellationToken cancellationToken=default)=>Task.FromResult(response(request));
}
[Trait("Gate","Mock"),Trait("Requirement","TEST-BUILD-004")]
public sealed class JavaWorkflowMockTests
{
    [Fact]
    public async Task NativeWorkflowCorrectFixtureAndNegativeControlsRetainAllIdentityKinds()
    {
        var calls=0;await JavaWorkflowCase.AssertContract(new ScriptedJavaWorkflow(request=>{Assert.Same(JavaWorkflowCase.Request,request);calls++;return JavaWorkflowCase.Correct();}));Assert.Equal(1,calls);
        foreach(var key in JavaWorkflowCase.SourceLock().Select(x=>x.Key))
        {
            var wrong=JavaWorkflowCase.Correct();wrong.SourceLock.Remove(key);
            Assert.NotNull(await Record.ExceptionAsync(()=>JavaWorkflowCase.AssertContract(new ScriptedJavaWorkflow(_=>wrong))));
        }
        foreach(var key in new[]{"runtime_tag_object","runtime_commit","grammar_commit","runtime_archive_sha256","grammar_archive_sha256","rid"})
        {
            var wrong=JavaWorkflowCase.Correct();wrong.SourceLock[key]="wrong";
            Assert.NotNull(await Record.ExceptionAsync(()=>JavaWorkflowCase.AssertContract(new ScriptedJavaWorkflow(_=>wrong))));
        }
        var untyped=JavaWorkflowCase.Correct();untyped.SourceLock["native_artifact"]!["sha256"]="wrong";
        Assert.NotNull(await Record.ExceptionAsync(()=>JavaWorkflowCase.AssertContract(new ScriptedJavaWorkflow(_=>untyped))));
        var empty=JavaWorkflowCase.Correct();empty.SourceLock["source_files"]=new JsonArray();
        Assert.NotNull(await Record.ExceptionAsync(()=>JavaWorkflowCase.AssertContract(new ScriptedJavaWorkflow(_=>empty))));
        var wrongMetadata=JavaWorkflowCase.Correct();wrongMetadata.SourceLock["runtime_archive_metadata"]![0]!["Attributes"]!["comment"]="wrong";
        Assert.NotNull(await Record.ExceptionAsync(()=>JavaWorkflowCase.AssertContract(new ScriptedJavaWorkflow(_=>wrongMetadata))));
        var wrongHeader=JavaWorkflowCase.Correct();wrongHeader.SourceLock["runtime_archive_metadata"]![0]!["Name"]="wrong";
        Assert.NotNull(await Record.ExceptionAsync(()=>JavaWorkflowCase.AssertContract(new ScriptedJavaWorkflow(_=>wrongHeader))));
        Assert.NotNull(await Record.ExceptionAsync(()=>JavaWorkflowCase.AssertContract(new ScriptedJavaWorkflow(_=>throw new IOException("source/build failure")))));
    }
}
