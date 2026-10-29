namespace AndroidMcp.BuildTool.Sources;

// Observed from typeof(SourcesRuntimeFactory).Assembly and the loaded runtime,
// never selected from the editable pipeline request. Core's existing managed
// PE identity primitive verifies these bytes against LoadedModuleId.
public sealed record SourceProducerAssemblyObservation(string AssemblyPath, Guid LoadedModuleId,
    ReadOnlyMemory<byte> AssemblyBytes, string RuntimeIdentity);
public interface ISourceProducerAssemblySource
{
    SourceProducerAssemblyObservation Capture();
}

// JavaAdmission pins the exact already evaluated manifest for the subsequent
// factory admission. Separate admissions are allowed; a substitution fails
// before source outputs and never causes an implicit parser fallback.
public sealed record SourceProducerIdentityAdmission(SourceExtractProducerIdentity Identity, SourceJavaAdmission? JavaAdmission);
public interface ISourceProducerIdentityBuilder
{
    SourceProducerIdentityAdmission Create(SourceJavaAdmission? java = null);
}
