using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Publication;

namespace AndroidMcp.BuildTool;

public sealed partial class Build
{
    private async Task CheckPublication()
    {
        var checkout=Path.GetFullPath(GitCheckout!);
        var receipt=Path.GetFullPath(ReceiptRoot!);
        var git=await ReadToolIdentity(GitIdentity!,"Git");
        var operation=new PublicationGitCheck(new NativeProcessRunner(new SystemClock()),
            new ProcessCaptureReader(receipt),new PublicationWorkspaceReader(),
            new PublicationChecker(new CanonicalJson()));
        var request=new PublicationGitCheckRequest(checkout,git,receipt);
        if(PolicyRelativePath is { } policy)request=request with{PolicyRelativePath=policy};
        var result=await operation.CheckAsync(request);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            result.Check,result.PolicySha256,result.TrackedPathsSha256,
            Git=git,result.GitProcess,CheckoutRoot=checkout,ReceiptDirectory=receipt
        }));
        if(!result.Check.IsValid)
            throw new InvalidOperationException($"Publication check rejected {result.Check.Issues.Count} tracked file issue(s).");
    }
}
