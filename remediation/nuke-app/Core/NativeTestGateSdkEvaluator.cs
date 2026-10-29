namespace AndroidMcp.BuildTool.Core;

// Collector-facing projection over the scoped native SDK API session. An injected
// fixture session proves this mapping, not an installed-SDK execution claim.
public sealed class NativeTestGateSdkEvaluator(TestGateNativeFactsConfiguration configuration,
    ITestGateSdkNativeSessionFactory sessions,ITestGateResolvedReferenceQuery copies):ITestGateSdkEvaluator
{
    private static readonly HashSet<string> SourceItemKinds=new(StringComparer.Ordinal)
    {
        "Compile","EmbeddedResource","AdditionalFiles","Analyzer","EditorConfigFiles",
        "Content","None"
    };
    internal static readonly HashSet<string> SourceFileProperties=new(StringComparer.Ordinal)
    {
        "ProjectAssetsFile","NuGetLockFilePath","CodeAnalysisRuleSet","AppConfig",
        "AssemblyOriginatorKeyFile","Win32Resource","Win32Manifest"
    };
    public async Task<TestGateSdkEvaluation> EvaluateAsync(TestGateSdkEvaluationRequest request,
        CancellationToken cancellationToken=default)
    {
        if(request.Configuration!=configuration.Configuration||request.TargetFramework!=configuration.TargetFramework)
            throw Error("SDK_CONFIGURATION_MISMATCH");
        ValidateInventoryRequest(request,configuration);
        if(!new CanonicalJson().Serialize(System.Text.Json.JsonSerializer.SerializeToElement(request.Sdk)).AsSpan().SequenceEqual(
            new CanonicalJson().Serialize(System.Text.Json.JsonSerializer.SerializeToElement(configuration.Sdk))))throw Error("SDK_PIN_MISMATCH");
        Admit(request.Run.HostProject,"HostProject",request.Run.HostProject);Admit(request.Run.TestsProject,"TestsProject",request.Run.TestsProject);
        var projects=new List<TestGateEvaluatedProject>();
        var pending=new Queue<string>();var seen=new HashSet<string>(StringComparer.Ordinal);
        pending.Enqueue(request.Run.HostProject);pending.Enqueue(request.Run.TestsProject);
        foreach(var project in request.ProjectInventory??[])pending.Enqueue(project);
        await using(var session=await sessions.OpenAsync(new(request,configuration.ApprovedReadRoots),cancellationToken))
        {
            while(pending.TryDequeue(out var path))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if(!seen.Add(path))continue;
                Admit(path,"Project",path);
                var native=await session.LoadProjectAsync(path,cancellationToken);
                if(native.ProjectFullPath!=path||native.EvaluationIssues.Count!=0)throw Error("SOURCE_INPUTS_UNRESOLVED");
                if(Path.GetFullPath(native.ToolsPath)!=Path.GetFullPath(request.Sdk.SdkDirectory))throw Error("SDK_PIN_MISMATCH");
                if(native.Configuration!=request.Configuration||native.TargetFramework!=request.TargetFramework)
                    throw Error("SDK_CONFIGURATION_MISMATCH");
                Admit(native.TargetPath,"TargetPath",path);
                var inputs=new List<TestGateEvaluatedInput>{new("Project",path,path)};
                var references=new List<string>();var imports=new List<string>();
                foreach(var item in native.Items)
                {
                    if(!SourceItemKinds.Contains(item.ItemType)&&item.ItemType!="ProjectReference")continue;
                    if(string.IsNullOrWhiteSpace(item.EvaluatedInclude)||string.IsNullOrWhiteSpace(item.FullPath)||
                        string.IsNullOrWhiteSpace(item.DefiningProjectFullPath))throw Error("SOURCE_INPUTS_UNRESOLVED");
                    Admit(item.FullPath,item.ItemType+".FullPath",path);Admit(item.DefiningProjectFullPath,item.ItemType+".DefiningProjectFullPath",path);
                    if(item.ItemType=="ProjectReference")
                    {
                        if(!references.Contains(item.FullPath,StringComparer.Ordinal))references.Add(item.FullPath);
                        pending.Enqueue(item.FullPath);
                    }
                    else inputs.Add(new(item.ItemType,item.FullPath,item.DefiningProjectFullPath));
                }
                foreach(var import in native.Imports)
                {
                    if(string.IsNullOrWhiteSpace(import.ImportedProjectFullPath)||string.IsNullOrWhiteSpace(import.ImportingProjectFullPath))
                        throw Error("SOURCE_INPUTS_UNRESOLVED");
                    Admit(import.ImportedProjectFullPath,"Import.ImportedProjectFullPath",path);Admit(import.ImportingProjectFullPath,"Import.ImportingProjectFullPath",path);
                    if(!imports.Contains(import.ImportedProjectFullPath,StringComparer.Ordinal))imports.Add(import.ImportedProjectFullPath);
                }
                foreach(var property in native.ParticipatingFileProperties)
                {
                    if(!SourceFileProperties.Contains(property.Key))throw Error("SOURCE_INPUTS_UNRESOLVED");
                    if(string.IsNullOrWhiteSpace(property.Value))continue;
                    Admit(property.Value,"Property."+property.Key,path);inputs.Add(new(property.Key,property.Value,path));
                }
                projects.Add(new(path,native.TargetPath,inputs,imports,references,[],[],native.TestMetadata));
            }
            ValidateInventoryResult(request.ProjectInventory,projects);
            if(request.Phase==TestGateSdkEvaluationPhase.ResolvedBuildOutputs)
            {
                var host=projects.Single(project=>project.ProjectPath==request.Run.HostProject);
                var observed=await copies.QueryAsync(new(request.Run,request.Sdk,host.TargetPath,
                    request.Configuration,request.TargetFramework),cancellationToken);
                if(observed.Code!="OK")throw Error(observed.Code);
                if(observed.Copies.Count==0)throw Error("RESOLVED_REFERENCE_MISSING");
                if(observed.Copies.Count!=1)throw Error("RESOLVED_REFERENCE_CONFLICT");
                var copy=observed.Copies[0];
                if(copy.SourcePath!=host.TargetPath||copy.ReferencedProject!=request.Run.HostProject)throw Error("RESOLVED_REFERENCE_MISMATCH");
                Admit(copy.DestinationPath,"ResolvedCopy.DestinationPath",request.Run.TestsProject);
                var index=projects.FindIndex(project=>project.ProjectPath==request.Run.TestsProject);
                projects[index]=projects[index] with {ResolvedCopies=observed.Copies};
            }
            else if(request.Phase!=TestGateSdkEvaluationPhase.SourceInputs)throw Error("SOURCE_INPUTS_UNRESOLVED");
        }
        return new(projects,request.Sdk.AssemblyFiles);
    }
    internal static void ValidateInventoryRequest(TestGateSdkEvaluationRequest request,TestGateNativeFactsConfiguration configuration)
    {
        var expected=configuration.ProjectInventory;var selected=request.ProjectInventory;
        if((expected is null)!=(selected is null)||expected is not null&&!expected.SequenceEqual(selected!,StringComparer.Ordinal))
            throw Error("SOURCE_INPUTS_UNRESOLVED");
        if(selected is null)return;
        if(selected.Count==0||selected.Distinct(StringComparer.Ordinal).Count()!=selected.Count||
            !selected.Contains(request.Run.HostProject,StringComparer.Ordinal)||!selected.Contains(request.Run.TestsProject,StringComparer.Ordinal))
            throw Error("SOURCE_INPUTS_UNRESOLVED");
        foreach(var project in selected)Admit(configuration,project,"ProjectInventory",project);
    }
    internal static void ValidateInventoryResult(IReadOnlyList<string>? inventory,IReadOnlyList<TestGateEvaluatedProject> projects)
    {
        if(inventory is null)return;
        var observed=projects.Select(project=>project.ProjectPath).ToHashSet(StringComparer.Ordinal);
        if(observed.Count!=projects.Count||!observed.SetEquals(inventory))throw Error("SOURCE_INPUTS_UNRESOLVED");
    }
    private void Admit(string path,string role,string project)=>Admit(configuration,path,role,project);
    private static void Admit(TestGateNativeFactsConfiguration configuration,string path,string role,string project)
    {
        if(string.IsNullOrWhiteSpace(path)||!Path.IsPathFullyQualified(path))throw Error("SOURCE_INPUTS_UNRESOLVED");
        foreach(var root in configuration.ApprovedReadRoots)
        {
            var relative=Path.GetRelativePath(Path.GetFullPath(root),Path.GetFullPath(path)).Replace('\\','/');
            if(!ArtifactPaths.IsRelative(relative))continue;
            try{ArtifactPaths.Resolve(root,relative);return;}
            catch(ArgumentException){throw NativeTestGateSdkSessionFactory.ScopeFailure("Native path traverses a linked or escaped input.",path,role,project);}
        }
        throw NativeTestGateSdkSessionFactory.ScopeFailure("Native path is outside the admitted participating roots.",path,role,project);
    }
    private static TestGateFactsException Error(string code)=>new(code,"Native SDK source evaluation unavailable: "+code);
}
