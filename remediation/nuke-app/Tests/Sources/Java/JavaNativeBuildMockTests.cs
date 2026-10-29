using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources.Java;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources.Java;

public static class JavaNativeBuildCases
{
    public const string RuntimeCommit="6e0618704ad758ba2ea5822faa80bcd36fbeba3d";
    public const string GrammarCommit="94703d5a6bed02b98e438d7cad1136c01a60ba2c";
    public static IReadOnlyList<string> Arguments(JavaNativeBuildRequest request) => ["-std=c11","-D_DEFAULT_SOURCE","-fPIC","-shared","-O2",
        "-I"+Path.Combine(request.SourceRoot,"runtime/lib/include"),"-I"+Path.Combine(request.SourceRoot,"runtime/lib/src"),
        "-I"+Path.Combine(request.SourceRoot,"java/src"),Path.Combine(request.SourceRoot,"runtime/lib/src/lib.c"),
        Path.Combine(request.SourceRoot,"java/src/parser.c"),"-o",Path.Combine(request.WorkingDirectory,"libandroidmcp_java_parser.so")];
    public static async Task AssertContract(IJavaNativeBuilder provider,JavaNativeBuildRequest request)
    {
        var result=await provider.BuildAsync(request);
        Assert.Equal(RuntimeCommit,result.RuntimeCommit);Assert.Equal(GrammarCommit,result.GrammarCommit);Assert.Equal(14,result.LanguageAbi);
        Assert.Equal(request.ArtifactRelativePath,result.Artifact.RelativePath);Assert.Matches("^[0-9a-f]{64}$",result.Artifact.Sha256);Assert.True(result.Artifact.Length>0);
        Assert.Equal("application/x-sharedlib",result.Artifact.MediaType);
        Assert.Equal(request.Compiler,result.Compilation.Tool);Assert.Equal(Arguments(request),result.Compilation.Arguments);
        Assert.Equal(request.WorkingDirectory,result.Compilation.WorkingDirectory);Assert.Equal(request.ReceiptDirectory,result.Compilation.ReceiptDirectory);
        Assert.Empty(result.Compilation.PublicEnvironment);Assert.Null(result.Compilation.StandardInputPath);
        Assert.Equal(0,result.Process.ExitCode);Assert.False(result.Process.Cancelled);Assert.True(result.Process.EndedAt>=result.Process.StartedAt);
    }
}
internal sealed class ScriptedJavaNativeBuilder(Func<JavaNativeBuildRequest,JavaNativeBuildResult> response):IJavaNativeBuilder
{
    public Task<JavaNativeBuildResult> BuildAsync(JavaNativeBuildRequest request,CancellationToken cancellationToken=default)=>Task.FromResult(response(request));
}
[Trait("Gate","Mock"),Trait("Requirement","TEST-BUILD-004")]
public sealed class JavaNativeBuildMockTests
{
    private static readonly JavaNativeBuildRequest Request=new("/fixtures/java-native",new Dictionary<string,string>(),
        new("/fixtures/cc",new string('a',64),"pinned-compiler"),"/fixtures/build","/fixtures/receipts","native/linux-x64/libandroidmcp_java_parser.so");
    private static JavaNativeBuildResult Correct()=>new(new(Request.ArtifactRelativePath,new string('b',64),1024,"application/x-sharedlib"),
        new(Request.Compiler,JavaNativeBuildCases.Arguments(Request),Request.WorkingDirectory,new Dictionary<string,string>(),Request.ReceiptDirectory),
        new(0,"/fixtures/receipts/stdout.bin","/fixtures/receipts/stderr.bin",DateTimeOffset.UnixEpoch,DateTimeOffset.UnixEpoch,false),
        JavaNativeBuildCases.RuntimeCommit,JavaNativeBuildCases.GrammarCommit,14);
    [Fact]
    public async Task CorrectContractAndIndependentNegativeControlsProveTypedCompilerBoundary()
    {
        var calls=0;await JavaNativeBuildCases.AssertContract(new ScriptedJavaNativeBuilder(request=>{Assert.Same(Request,request);calls++;return Correct();}),Request);Assert.Equal(1,calls);
        var good=Correct();
        var wrong=new[]{good with{LanguageAbi=15},good with{RuntimeCommit="wrong"},good with{GrammarCommit="wrong"},
            good with{Artifact=good.Artifact with{Sha256="unproven"}},good with{Artifact=good.Artifact with{Length=0}},
            good with{Artifact=good.Artifact with{RelativePath="other.so"}},good with{Artifact=good.Artifact with{MediaType="text/plain"}},
            good with{Process=good.Process with{ExitCode=1}},good with{Process=good.Process with{Cancelled=true}},
            good with{Compilation=good.Compilation with{Arguments=["sh","-c","compile"]}},
            good with{Compilation=good.Compilation with{Tool=Request.Compiler with{Sha256="wrong"}}},
            good with{Compilation=good.Compilation with{WorkingDirectory="/other"}},good with{Compilation=good.Compilation with{ReceiptDirectory="/other"}},
            good with{Compilation=good.Compilation with{StandardInputPath="/unrequested"}},
            good with{Compilation=good.Compilation with{PublicEnvironment=new Dictionary<string,string>{{"UNREQUESTED","1"}}}}};
        foreach(var item in wrong)Assert.NotNull(await Record.ExceptionAsync(()=>JavaNativeBuildCases.AssertContract(new ScriptedJavaNativeBuilder(_=>item),Request)));
        Assert.NotNull(await Record.ExceptionAsync(()=>JavaNativeBuildCases.AssertContract(new ScriptedJavaNativeBuilder(_=>throw new IOException("compiler failure")),Request)));
    }
}
