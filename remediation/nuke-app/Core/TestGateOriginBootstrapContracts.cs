namespace AndroidMcp.BuildTool.Core;

// Runtime source reads only the single public origin environment key. Compiled
// scope is obtained from the actual Tests assembly's exact AssemblyMetadata key
// AndroidMcp.TestGate.WorkspaceRoot, generated from validated project directory.
// No caller path, environment root, parent walk or challenge body grants scope.
public interface ITestGateOriginBootstrapSource
{
    string? ReadChallengePath();
    string ReadCompiledWorkspaceRoot();
}
public interface ITestGateOriginRuntimeWriterFactory
{
    // Thin composition of existing shared store, file reader, strict-new writer
    // and actual loaded test/host assembly pair, restricted to this compiled root.
    ITestGateOriginRuntimeWriter CreateForCompiledRoot(string compiledWorkspaceRoot);
}
public interface ITestGateOriginBootstrap
{
    Task<TestGateOriginRuntimeResult> InitializeAsync(CancellationToken cancellationToken=default);
}
