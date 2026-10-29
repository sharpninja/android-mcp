using System.Collections.ObjectModel;
using System.Text;
using AndroidMcp.BuildTool.Core;
using static AndroidMcp.BuildTool.Publication.PublicationGitCheckErrors;

namespace AndroidMcp.BuildTool.Publication;

/// <summary>Read-only Git enumeration and metadata observations; one existing policy engine.</summary>
public sealed class PublicationGitCheck(IProcessRunner runner,IProcessCaptureReader captures,
    IPublicationWorkspaceReader workspace,IPublicationChecker checker):IPublicationGitCheck
{
    private static readonly UTF8Encoding Utf8=new(false,true);
    private static readonly IReadOnlyList<string> Arguments=Array.AsReadOnly(new[]{"-c","core.fsmonitor=false","-c","core.untrackedCache=false","ls-files","--cached","-z","--"});
    private static readonly IReadOnlyDictionary<string,string> Environment=new ReadOnlyDictionary<string,string>(new Dictionary<string,string>{{"GIT_OPTIONAL_LOCKS","0"},{"GIT_TERMINAL_PROMPT","0"}});
    public async Task<PublicationGitCheckResult> CheckAsync(PublicationGitCheckRequest request,CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if(request is null || !Path.IsPathFullyQualified(request.CheckoutRoot) || !Path.IsPathFullyQualified(request.ReceiptDirectory) ||
            request.Git is null || !Path.IsPathFullyQualified(request.Git.ExecutablePath) || !ArtifactPaths.IsRelative(request.PolicyRelativePath) || !PublicationNames.Relative(request.PolicyRelativePath))
            throw new PublicationGitCheckException("PUBLICATION_REQUEST","An admitted absolute checkout, Git tool, receipt directory and relative policy are required.");
        var root=Path.GetFullPath(request.CheckoutRoot);
        byte[] policy;
        try{policy=(await workspace.ReadPolicyAsync(root,request.PolicyRelativePath,cancellationToken)).ToArray();}
        catch(Exception error) when(IsObservationFailure(error))
        {throw new PublicationGitCheckException("PUBLICATION_POLICY","Checkout policy could not be read safely.");}
        var admission=checker.Check(policy,[]);
        if(!admission.IsValid)throw new PublicationGitCheckException("PUBLICATION_POLICY","The existing checker rejected the policy.",policyCheck:admission);
        cancellationToken.ThrowIfCancellationRequested();
        ProcessResult process;
        try{process=await runner.RunAsync(new(request.Git,Arguments,root,Environment,request.ReceiptDirectory,ClearEnvironment:true),cancellationToken);}
        catch(Exception error) when(IsObservationFailure(error) || error is System.ComponentModel.Win32Exception)
        {throw new PublicationGitCheckException("PUBLICATION_GIT","Pinned Git could not complete its read-only enumeration.");}
        if(process.Cancelled)throw new OperationCanceledException(cancellationToken);
        if(process.ExitCode!=0)throw new PublicationGitCheckException("PUBLICATION_GIT","Read-only Git enumeration failed; capture receipts are retained.",process);
        cancellationToken.ThrowIfCancellationRequested();
        byte[] output;
        try{output=(await captures.ReadAsync(process,false,cancellationToken)).ToArray();}
        catch(Exception error) when(IsObservationFailure(error))
        {throw new PublicationGitCheckException("PUBLICATION_CAPTURE","Git stdout capture could not be read safely.",process);}
        string[] paths;
        try
        {
            if(output.Length==0)paths=[];
            else
            {
                if(output[^1]!=0)throw new FormatException();
                var names=Utf8.GetString(output).Split('\0');
                paths=names[..^1];
                if(paths.Any(string.IsNullOrEmpty))throw new FormatException();
            }
        }
        catch(Exception error) when(error is DecoderFallbackException or FormatException)
        {throw new PublicationGitCheckException("PUBLICATION_CAPTURE","Git stdout is not complete UTF8 NUL-framed path data.",process);}
        foreach(var path in paths)
            if(!ArtifactPaths.IsRelative(path) || !PublicationNames.Relative(path))throw new PublicationGitCheckException("PUBLICATION_PATH","A tracked path is not a portable checkout-relative path.",process);
        PublicationFile[] files;
        try{files=(await workspace.ObserveAsync(root,Array.AsReadOnly(paths),cancellationToken)).ToArray();}
        catch(PublicationGitCheckException error)
        {throw new PublicationGitCheckException(error.Code,error.Message,process,error.PolicyCheck);}
        catch(Exception error) when(IsObservationFailure(error))
        {throw new PublicationGitCheckException("PUBLICATION_SNAPSHOT","Checkout metadata could not be observed safely.",process);}
        if(!paths.SequenceEqual(files.Select(f=>f.Path),StringComparer.Ordinal))throw new PublicationGitCheckException("PUBLICATION_SNAPSHOT","Metadata does not match the complete ordered tracked path list.",process);
        cancellationToken.ThrowIfCancellationRequested();
        var check=checker.Check(policy,Array.AsReadOnly(files));
        return new(check,Array.AsReadOnly(files),ContentHash.Sha256(policy),ContentHash.Sha256(output),process);
    }
}
internal static class PublicationGitCheckErrors
{
    public static bool IsObservationFailure(Exception error)=>error is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException;
}
