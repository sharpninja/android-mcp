using System.Reflection;

namespace AndroidMcp.BuildTool.Core;

public sealed class NativeTestGateOriginBootstrap(ITestGateOriginBootstrapSource source,
    ITestGateOriginRuntimeWriterFactory writers):ITestGateOriginBootstrap
{
    public async Task<TestGateOriginRuntimeResult> InitializeAsync(CancellationToken cancellationToken=default)
    {
        var challenge=source.ReadChallengePath();
        if(string.IsNullOrEmpty(challenge))return new("NO_ORIGIN_REQUEST",null);
        var root=source.ReadCompiledWorkspaceRoot();
        if(string.IsNullOrWhiteSpace(root)||!Path.IsPathRooted(root))
            throw new TestGateFactsException("ORIGIN_COMPILED_SCOPE_INVALID","Compiled origin scope is missing or relative.");
        var result=await writers.CreateForCompiledRoot(root).WriteIfRequestedAsync(new(root,challenge),cancellationToken);
        if(result.Code!="RECORDED"||result.Reply is null)
            throw new TestGateFactsException(result.Code=="RECORDED" ? "ORIGIN_REPLY_MISSING" : result.Code,
                "The requested executing-test origin proof was not recorded.");
        return result;
    }
}
public sealed class RuntimeTestGateOriginBootstrapSource(Assembly tests):ITestGateOriginBootstrapSource
{
    public const string ChallengeKey="ANDROIDMCP_TEST_GATE_ORIGIN_REQUEST";
    public const string MetadataKey="AndroidMcp.TestGate.WorkspaceRoot";
    public string? ReadChallengePath()=>Environment.GetEnvironmentVariable(ChallengeKey);
    public string ReadCompiledWorkspaceRoot()
    {
        var values=tests.GetCustomAttributes<AssemblyMetadataAttribute>().Where(value=>value.Key==MetadataKey).ToArray();
        var root=values.Length==1 ? values[0].Value : null;
        if(string.IsNullOrWhiteSpace(root)||!Path.IsPathFullyQualified(root))
            throw new TestGateFactsException("ORIGIN_COMPILED_SCOPE_INVALID","Exactly one absolute compiled Tests scope is required.");
        return root;
    }
}
public sealed class SharedStoreTestGateOriginRuntimeWriterFactory(Assembly tests,Assembly host):ITestGateOriginRuntimeWriterFactory
{
    public ITestGateOriginRuntimeWriter CreateForCompiledRoot(string compiledWorkspaceRoot)
    {
        // Reuse the one file/store/path/hash authority and proven strict CreateNew
        // adapter. No scope is taken from the challenge body or environment root.
        var store=new FileArtifactStore(compiledWorkspaceRoot,bytes=>ContentHash.Sha256(bytes.Span));
        return new NativeTestGateOriginRuntimeWriter(store,new ScopedTestGateNativeFileReader([compiledWorkspaceRoot]),
            new SharedStoreTestGateOriginArtifactWriter(store),new RuntimeTestGateLoadedAssemblyPairSource(tests,host));
    }
}
