using System.Collections;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text.RegularExpressions;

namespace AndroidMcp.BuildTool.Core;

// Bound to the selected installed SDK and the existing two-project native gate.
// Normal callers retain the private collectible context. Only the internal
// one-shot monolith SDK entry selects the dedicated-child Default binding.
public sealed class NativeTestGateSdkSessionFactory(string sdkDirectory,IPinnedArtifactReader pins)
    : ITestGateSdkNativeSessionFactory
{
    private static readonly string[] RequiredPins=["Microsoft.Build.dll","Microsoft.Build.Framework.dll",
        "Microsoft.Build.Utilities.Core.dll","Microsoft.Build.Tasks.Core.dll","Microsoft.Common.CurrentVersion.targets",
        "Microsoft.NET.StringTools.dll"];

    public async Task<ITestGateSdkNativeSession> OpenAsync(TestGateSdkNativeOpenRequest request,CancellationToken cancellationToken=default)
    {
        var paths=await AdmitSdkAsync(request,cancellationToken);
        var context=new SdkContext(paths);
        return CreateSession(request,()=>context.LoadFromAssemblyPath(paths["Microsoft.Build"]),context.Unload);
    }

    internal async Task<ITestGateSdkNativeSession> OpenChildWithBinderAsync(TestGateSdkNativeOpenRequest request,
        IReadOnlyCollection<string> loadedDefaultNames,Func<IReadOnlyDictionary<string,string>,ITestGateSdkNativeSession> bind,
        CancellationToken cancellationToken=default)
    {
        var paths=await AdmitSdkAsync(request,cancellationToken);
        if(loadedDefaultNames.Any(paths.ContainsKey))throw Error("SDK_CHILD_CONTEXT_CONFLICT","Selected SDK identity already exists in Default.");
        try{return bind(paths);}
        catch(Exception error){throw Translate(error);}
    }

    // Root's internal --sdk-evaluation-once dispatch is the only composition
    // caller. This factory must never be selected by the long-lived producer.
    internal static ITestGateSdkNativeSessionFactory CreateForDedicatedChild(string sdkDirectory,IPinnedArtifactReader pins)
        =>new DedicatedChild(new(sdkDirectory,pins));
    private sealed class DedicatedChild(NativeTestGateSdkSessionFactory owner):ITestGateSdkNativeSessionFactory
    {
        public Task<ITestGateSdkNativeSession> OpenAsync(TestGateSdkNativeOpenRequest request,CancellationToken cancellationToken=default)
            =>owner.OpenChildWithBinderAsync(request,AssemblyLoadContext.Default.Assemblies
                .Select(assembly=>assembly.GetName().Name).OfType<string>().ToArray(),paths=>OpenDefault(request,paths),cancellationToken);
        private static ITestGateSdkNativeSession OpenDefault(TestGateSdkNativeOpenRequest request,IReadOnlyDictionary<string,string> paths)
        {
            Assembly? Resolve(AssemblyLoadContext context,AssemblyName identity)
                =>identity.Name is {} name&&paths.TryGetValue(name,out var path) ? context.LoadFromAssemblyPath(path) : null;
            AssemblyLoadContext.Default.Resolving+=Resolve;
            return CreateSession(request,()=>
            {
                // MSBuild's resolver plugin context explicitly delegates this
                // well-known contract to Default. Load its admitted bytes first.
                AssemblyLoadContext.Default.LoadFromAssemblyPath(paths["Microsoft.Build.Framework"]);
                return AssemblyLoadContext.Default.LoadFromAssemblyPath(paths["Microsoft.Build"]);
            },()=>AssemblyLoadContext.Default.Resolving-=Resolve);
        }
    }

    private async Task<Dictionary<string,string>> AdmitSdkAsync(TestGateSdkNativeOpenRequest request,CancellationToken cancellationToken)
    {
        if(Full(request.Evaluation.Sdk.SdkDirectory)!=Full(sdkDirectory))throw Error("SDK_PIN_MISMATCH","Configured SDK directory differs.");
        if(!Simple(request.Evaluation.Configuration)||!Simple(request.Evaluation.TargetFramework))throw Error("SDK_CONFIGURATION_MISMATCH","Unsupported property value.");
        if(RequiredPins.Any(required=>!request.Evaluation.Sdk.AssemblyFiles.Any(pin=>pin.RelativePath==required)))
            throw Error("SDK_PIN_MISSING","The fixed SDK manifest is incomplete.");
        var paths=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach(var pin in request.Evaluation.Sdk.AssemblyFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string path;
            try{path=ArtifactPaths.Resolve(sdkDirectory,pin.RelativePath);}
            catch(ArgumentException){throw Error("SDK_PIN_MISMATCH","SDK pin is outside the selected directory.");}
            try{await pins.ReadAsync(pin,cancellationToken);}
            catch(PinnedArtifactException){throw Error("SDK_PIN_MISMATCH","Pinned SDK bytes differ.");}
            if(!string.Equals(Path.GetExtension(path),".dll",StringComparison.OrdinalIgnoreCase))continue;
            if(!paths.TryAdd(Path.GetFileNameWithoutExtension(path),path))throw Error("SDK_PIN_MISMATCH","Duplicate SDK assembly identity.");
        }
        return paths;
    }

    private static ITestGateSdkNativeSession CreateSession(TestGateSdkNativeOpenRequest request,Func<Assembly> loadBuild,Action release)
    {
        object? collection=null;
        try
        {
            var build=loadBuild();
            var type=build.GetType("Microsoft.Build.Evaluation.ProjectCollection",throwOnError:true)!;
            collection=Activator.CreateInstance(type)??throw Error("SDK_API_UNSUPPORTED","ProjectCollection construction returned null.");
            var load=type.GetMethod("LoadProject",[typeof(string),typeof(IDictionary<string,string>),typeof(string)])
                ??throw Error("SDK_API_UNSUPPORTED","Required LoadProject overload is unavailable.");
            return new Session(request,collection,load,release);
        }
        catch(Exception error)
        {
            try{(collection as IDisposable)?.Dispose();}finally{release();}
            throw Translate(error);
        }
    }
    private sealed class SdkContext(IReadOnlyDictionary<string,string> paths)
        : AssemblyLoadContext("AndroidMcp scoped SDK "+Guid.NewGuid().ToString("N"),isCollectible:true)
    {
        // Framework sharing is CLR runtime binding, not an SDK fallback. The
        // trusted-platform list is filtered to this runtime's own directory;
        // app/package/SDK assemblies in that list do not become fallbacks.
        private readonly HashSet<string> runtimeNames=RuntimeNames();
        protected override Assembly? Load(AssemblyName identity)
        {
            if(identity.Name is {} name&&paths.TryGetValue(name,out var path))return LoadFromAssemblyPath(path);
            if(identity.Name is {} framework&&runtimeNames.Contains(framework))return null;
            throw Error("SDK_PIN_MISSING","Required SDK dependency is not pinned: "+identity.FullName);
        }
        private static HashSet<string> RuntimeNames()
        {
            var root=Full(RuntimeEnvironment.GetRuntimeDirectory());
            return ((AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string)??"")
                .Split(Path.PathSeparator,StringSplitOptions.RemoveEmptyEntries)
                .Where(path=>Full(Path.GetDirectoryName(path)!)==root)
                .Select(Path.GetFileNameWithoutExtension).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
    }
    private sealed class Session(TestGateSdkNativeOpenRequest request,object projectCollection,MethodInfo projectLoader,Action release)
        : ITestGateSdkNativeSession
    {
        private object? collection=projectCollection;
        private MethodInfo? load=projectLoader;
        private bool disposed;
        public Task<TestGateSdkNativeProject> LoadProjectAsync(string absoluteProjectPath,CancellationToken cancellationToken=default)
        {
            ObjectDisposedException.ThrowIf(disposed,this);cancellationToken.ThrowIfCancellationRequested();
            Admit(absoluteProjectPath,"Project",absoluteProjectPath);
            try
            {
                var properties=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase)
                    {{"Configuration",request.Evaluation.Configuration},{"TargetFramework",request.Evaluation.TargetFramework}};
                var project=load!.Invoke(collection,[absoluteProjectPath,properties,null])
                    ??throw Error("SDK_API_UNSUPPORTED","Native project evaluation returned null.");
                var projectPath=Text(project,"FullPath");
                if(Full(projectPath)!=Full(absoluteProjectPath))throw Error("SOURCE_INPUTS_UNRESOLVED","Native project identity differs.");
                var tools=Property(project,"MSBuildToolsPath");
                if(string.IsNullOrEmpty(tools)||Full(tools)!=Full(request.Evaluation.Sdk.SdkDirectory))
                    throw Error("SDK_PIN_MISMATCH","Native toolset did not select the pinned SDK.");
                var items=new List<TestGateSdkNativeItem>();
                foreach(var item in Sequence(project,"Items"))
                {
                    var kind=Text(item,"ItemType");
                    if(kind=="Reference"&&!string.IsNullOrEmpty(Metadata(item,"HintPath")))
                        throw Error("SOURCE_INPUTS_UNRESOLVED","Manual binary references are outside this gate's supported source shapes.");
                    items.Add(new(kind,Text(item,"EvaluatedInclude"),Metadata(item,"FullPath"),Metadata(item,"DefiningProjectFullPath")));
                }
                var imports=new List<TestGateSdkNativeImport>();
                foreach(var import in Sequence(project,"Imports"))
                {
                    var imported=Text(Member(import,"ImportedProject"),"FullPath");
                    var importing=Text(Member(Member(import,"ImportingElement"),"ContainingProject"),"FullPath");
                    Admit(imported,"ImportedProject",absoluteProjectPath);Admit(importing,"ImportingProject",absoluteProjectPath);imports.Add(new(imported,importing));
                }
                var inputs=new Dictionary<string,string>(StringComparer.Ordinal);
                foreach(var role in NativeTestGateSdkEvaluator.SourceFileProperties)
                {
                    var value=Property(project,role);if(string.IsNullOrEmpty(value))continue;
                    var path=Full(value);Admit(path,role,absoluteProjectPath);inputs.Add(role,path);
                }
                if(request.Evaluation.Phase==TestGateSdkEvaluationPhase.ResolvedBuildOutputs&&
                    Full(projectPath)==Full(request.Evaluation.Run.TestsProject))VerifyReferenceTarget(project);
                var target=Property(project,"TargetPath");
                if(string.IsNullOrEmpty(target))throw Error("SOURCE_INPUTS_UNRESOLVED","Native TargetPath is missing.");
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(new TestGateSdkNativeProject(Full(projectPath),Full(tools),Property(project,"Configuration"),
                    Property(project,"TargetFramework"),Full(target),items,imports,inputs,[],ObserveTestMetadata(project)));
            }
            catch(Exception error)when(error is not OperationCanceledException){throw Translate(error);}
        }
        private void VerifyReferenceTarget(object project)
        {
            var expected=ArtifactPaths.Resolve(request.Evaluation.Sdk.SdkDirectory,"Microsoft.Common.CurrentVersion.targets");
            VerifyNamedReferenceTarget(project,expected);
        }
        private void Admit(string path,string role,string project)
        {
            if(string.IsNullOrEmpty(path)||!Path.IsPathFullyQualified(path))throw ScopeFailure("Native path is not absolute.",path,role,project);
            foreach(var root in request.ApprovedReadRoots)
            {
                var relative=Path.GetRelativePath(Full(root),Full(path)).Replace('\\','/');
                if(!ArtifactPaths.IsRelative(relative))continue;
                try{ArtifactPaths.Resolve(root,relative);return;}
                catch(ArgumentException){throw ScopeFailure("Native path traverses a linked or escaped input.",path,role,project);}
            }
            throw ScopeFailure("Native path is outside the admitted participating roots.",path,role,project);
        }
        public ValueTask DisposeAsync()
        {
            if(disposed)return ValueTask.CompletedTask;disposed=true;
            try
            {
                if(collection is not null)
                {
                    collection.GetType().GetMethod("UnloadAllProjects",Type.EmptyTypes)?.Invoke(collection,null);
                    (collection as IDisposable)?.Dispose();
                }
            }
            finally{collection=null;load=null;release();}
            return ValueTask.CompletedTask;
        }
    }
    internal static void VerifyNamedReferenceTarget(object project,string expected)
    {
        const string targetName="ReferenceCopyLocalPathsOutputGroup";
        var property=project.GetType().GetProperty("Targets")
            ??throw Error("SDK_API_UNSUPPORTED","Required declared Targets property is unavailable.");
        var dictionaryType=property.PropertyType;
        if(!dictionaryType.IsGenericType||dictionaryType.GetGenericTypeDefinition()!=typeof(IDictionary<,>)||dictionaryType.GenericTypeArguments[0]!=typeof(string))
            throw Error("SDK_API_UNSUPPORTED","Targets does not declare the required named dictionary API.");
        var targets=property.GetValue(project)??throw Error("SDK_API_UNSUPPORTED","Targets returned null.");
        var contains=dictionaryType.GetMethod("ContainsKey",[typeof(string)])
            ??throw Error("SDK_API_UNSUPPORTED","Declared target dictionary ContainsKey is unavailable.");
        var item=dictionaryType.GetProperty("Item",[typeof(string)])
            ??throw Error("SDK_API_UNSUPPORTED","Declared target dictionary Item[string] is unavailable.");
        if(contains.Invoke(targets,[targetName]) is not true)
            throw Error("SDK_RESOLUTION_TARGET_UNSUPPORTED","Required reference-copy target is absent.");
        var selected=item.GetValue(targets,[targetName]);
        if(selected is null||Full(Text(Member(selected,"Location"),"File"))!=Full(expected))
            throw Error("SDK_RESOLUTION_TARGET_UNSUPPORTED","Reference-copy target does not originate in the pinned SDK definition.");
    }
    internal static TestGateFactsException ScopeFailure(string reason,string path,string role,string project)
        =>Error("INPUT_SCOPE_VIOLATION",reason+" EvaluatingProject="+project+"; InputRole="+role+"; InputPath="+path);
    private static object Member(object instance,string name)=>instance.GetType().GetProperty(name)?.GetValue(instance)
        ??throw Error("SDK_API_UNSUPPORTED","Required native member is unavailable: "+name);
    private static string Text(object instance,string name)=>Member(instance,name) as string
        ??throw Error("SDK_API_UNSUPPORTED","Required native string member is unavailable: "+name);
    private static IEnumerable<object> Sequence(object instance,string name)=>(Member(instance,name) as IEnumerable)?.Cast<object>()
        ??throw Error("SDK_API_UNSUPPORTED","Required native collection member is unavailable: "+name);
    internal static TestGateProjectTestMetadata? ObserveTestMetadata(object project)
    {
        var isTest=Property(project,"IsTestProject");var kind=Property(project,"AndroidMcpTestKind");
        return isTest.Length==0&&kind.Length==0?null:new(isTest,kind);
    }
    private static string Property(object project,string name)=>(project.GetType().GetMethod("GetPropertyValue",[typeof(string)])?.Invoke(project,[name]) as string)
        ??throw Error("SDK_API_UNSUPPORTED","Native property lookup is unavailable.");
    private static string Metadata(object item,string name)=>(item.GetType().GetMethod("GetMetadataValue",[typeof(string)])?.Invoke(item,[name]) as string)
        ??throw Error("SDK_API_UNSUPPORTED","Native item metadata lookup is unavailable.");
    private static string Full(string value)=>Path.TrimEndingDirectorySeparator(Path.GetFullPath(value));
    private static bool Simple(string value)=>!string.IsNullOrEmpty(value)&&Regex.IsMatch(value,"^[A-Za-z0-9._-]+$");
    internal static TestGateFactsException Translate(Exception error)
    {
        for(Exception? current=error;current is not null;current=current.InnerException)
            if(current is TestGateFactsException known)return known;
        var inner=error is TargetInvocationException {InnerException:{} cause} ? cause : error;
        return Error("SDK_EVALUATION_FAILED",inner.ToString());
    }
    private static TestGateFactsException Error(string code,string detail)=>new(code,"Native SDK session rejected: "+detail);
}
