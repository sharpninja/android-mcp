using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Publication;

/// <summary>One projection of caller-supplied registry and pinned toolchain metadata.</summary>
public sealed class OperationHelpBuilder(ICanonicalJson canonical):IOperationHelpBuilder
{
    public OperationHelpResult Build(OperationHelpRequest request)
    {
        try
        {
            if(request.Definitions.Select(x=>x.Key).Distinct().Count()!=request.Definitions.Count)Fail("DUPLICATE_OPERATION");
            var parameters=request.GlobalRequiredParameters.Concat(request.Definitions.SelectMany(x=>x.RequiredParameters.Concat(x.OptionalParameters).Concat(x.DeclaredParameterGroups.SelectMany(group=>group))));
            if(parameters.Any(x=>!Regex.IsMatch(x,"\\A[a-z][a-z0-9-]*\\z",RegexOptions.CultureInvariant)))Fail("UNSAFE_PARAMETER_NAME");
            using var sdkDocument=JsonDocument.Parse(canonical.Serialize(request.SdkConfiguration.Bytes));
            var sdkObject=sdkDocument.RootElement.GetProperty("sdk");
            var sdk=sdkObject.TryGetProperty("version",out var version)?version.GetString()??"":"";
            var roll=sdkObject.TryGetProperty("rollForward",out var forward)?forward.GetString()??"":"";
            if(!Version.TryParse(sdk,out _) || roll!="disable")Fail("UNPINNED_SDK");
            // Project XML is data. DTD/entity resolution cannot open a file or URL.
            using var projectBytes=new MemoryStream(request.ProjectConfiguration.Bytes.ToArray(),false);
            using var reader=XmlReader.Create(projectBytes,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null});
            var project=XDocument.Load(reader);
            var framework=project.Descendants().Where(x=>x.Name.LocalName=="TargetFramework").Select(x=>x.Value.Trim()).SingleOrDefault()??"";
            using var lockDocument=JsonDocument.Parse(canonical.Serialize(request.PackageLock.Bytes));
            if(framework.Length==0)Fail("LOCK_FRAMEWORK_MISMATCH");
            if(!lockDocument.RootElement.GetProperty("dependencies").TryGetProperty(framework,out var packagesForFramework))Fail("LOCK_FRAMEWORK_MISMATCH");
            var packages=new List<HelpPackage>();
            foreach(var package in packagesForFramework.EnumerateObject().OrderBy(x=>x.Name,StringComparer.Ordinal))
            {
                if(!package.Value.TryGetProperty("type",out var type) || type.GetString()!="Direct")continue;
                var resolved=package.Value.TryGetProperty("resolved",out var value)?value.GetString()??"":"";
                if(string.IsNullOrWhiteSpace(resolved))Fail("UNRESOLVED_PACKAGE_PIN");
                packages.Add(new(package.Name,resolved));
            }
            var operations=request.Definitions.OrderBy(x=>x.Key.Category.ToString(),StringComparer.Ordinal)
                .ThenBy(x=>x.Key.Operation.ToString(),StringComparer.Ordinal).Select(x=>new HelpOperation(
                    x.Key.Category.ToString(),x.Key.Operation.ToString(),x.Description,
                    x.RequiredParameters.Order(StringComparer.Ordinal).ToArray(),x.OptionalParameters.Order(StringComparer.Ordinal).ToArray(),
                    x.DeclaredParameterGroups.Count==0?null:x.DeclaredParameterGroups.Select(group=>group.ToArray()).ToArray())).ToArray();
            var global=request.GlobalRequiredParameters.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            const string network="No network for help, Check or pure rehydration planning. Restore/acquisition prerequisites are explicit.";
            const string disk="Output/cache/archive requirements need their own measured plan; no full-corpus estimate is asserted.";
            const string execution="Registry metadata is a planning reference; root execution-adapter readiness is authoritative.";
            var json=canonical.Serialize(JsonSerializer.SerializeToUtf8Bytes(new
            {
                schema_version=1,sdk,roll_forward=roll,framework,
                packages=packages.Select(x=>new{name=x.Name,version=x.Version}),operations,global_required=global,network,disk,execution,
                sdk_sha256=ContentHash.Sha256(request.SdkConfiguration.Bytes.Span),project_sha256=ContentHash.Sha256(request.ProjectConfiguration.Bytes.Span),
                package_lock_sha256=ContentHash.Sha256(request.PackageLock.Bytes.Span)
            }));
            // Both views consume the same normalized model. There is no second
            // operation table, version table, filesystem scan or dispatch path.
            var markdown=new StringBuilder("# Native NUKE operation help\n\n");
            markdown.Append("SDK ").Append(sdk).Append("; rollForward ").Append(roll).Append("; framework ").Append(framework).Append("\n\n");
            foreach(var package in packages)markdown.Append("- ").Append(package.Name).Append(' ').Append(package.Version).Append('\n');
            markdown.Append("\nPlanning reference; root adapter readiness governs execution.\nNetwork: ").Append(network)
                .Append("\ndisk: ").Append(disk).Append("\nHistorical byte identity and full-corpus/cross-platform coverage need separate evidence.\n")
                .Append("\nRun from the build project directory. Use the same approved native host for the launcher and --dotnet-host when that parameter is declared. Replace the quoted placeholders with your paths.\n");
            foreach(var operation in operations)
            {
                markdown.Append("\n## ").Append(operation.category).Append('/').Append(operation.operation).Append("\n\n")
                    .Append(operation.description).Append("\n\n```powershell\n& '<native-dotnet>' run -- --root '<build-root>' --target ")
                    .Append(operation.category).Append(" --operation ").Append(operation.operation);
                foreach(var parameter in global.Concat(operation.required).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
                    markdown.Append(" --").Append(parameter).Append(" '<").Append(parameter).Append(">'" );
                foreach(var parameter in operation.optional)
                    markdown.Append(" [--").Append(parameter).Append(" '<").Append(parameter).Append(">']");
                markdown.Append("\n```\n");
                foreach(var group in operation.all_or_none??[])
                    markdown.Append("Supply together: ").Append(string.Join(", ",group.Select(parameter=>"--"+parameter))).Append(".\n");
            }
            return new(new("operation-help.json",json,"application/json"),new("operation-help.md",Encoding.UTF8.GetBytes(markdown.ToString()),"text/markdown"),[]);
        }
        catch(HelpFailure error){return new(null,null,[new(error.Code,"")]);}
        catch(Exception error) when(error is XmlException or JsonException or FormatException or InvalidOperationException or KeyNotFoundException or ArgumentException)
        {return new(null,null,[new("INVALID_HELP_INPUT","")]);}
    }
    private static void Fail(string code)=>throw new HelpFailure(code);
    private sealed class HelpFailure(string code):Exception(code){public string Code { get; }=code;}
    private sealed record HelpPackage(string Name,string Version);
    private sealed record HelpOperation(string category,string operation,string description,string[] required,string[] optional,
        [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] string[][]? all_or_none);
}
