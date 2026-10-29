using System.Runtime.CompilerServices;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Tests.Core;

internal static class TestGateExecutingModuleOrigin
{
    // No origin request means no metadata/store read and no evidence write.
#pragma warning disable CA2255 // Required test-host module provenance bootstrap.
    [ModuleInitializer]
    internal static void Initialize()
    {
        var tests=typeof(TestGateExecutingModuleOrigin).Assembly;
        var host=typeof(NativeTestGateEvidenceProducer).Assembly;
        new NativeTestGateOriginBootstrap(new RuntimeTestGateOriginBootstrapSource(tests),
            new SharedStoreTestGateOriginRuntimeWriterFactory(tests,host))
            .InitializeAsync().GetAwaiter().GetResult();
    }
#pragma warning restore CA2255
}
