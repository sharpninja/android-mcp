using AndroidMcp.BuildTool.Modules.Review;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review;

public sealed class ReviewCoverageSignatureNativeTests
{
    [Theory,MemberData(nameof(ReviewCoverageSignatureCases.Data),MemberType=typeof(ReviewCoverageSignatureCases))]
    public void Same_original_signature_cases_bind_to_existing_Launch_and_hash_authorities(string name)
    {
        var item=ReviewCoverageSignatureCases.Find(name);
        var inputs=Describe(item.Inputs);
        if(item.Partition is { } index)
        {
            var original=item.Inputs.Single();
            var parts=ReviewLaunchService.Partition([new(original.Key!,original.File,original.FullSha,original.Start,original.End!.Value,original.Text)]);
            Assert.Equal(original.Text,string.Concat(parts.SelectMany(x=>x).Select(x=>x.Text)));
            inputs=ReviewLaunchService.CoverageSignature(parts[index]);
        }
        var reference=Describe(item.Reference);
        var rows=inputs.OrderBy(x=>x.Key,StringComparer.Ordinal).ThenBy(x=>x.Start).ThenBy(x=>x.End)
            .ThenBy(x=>x.BodySha256,StringComparer.Ordinal).Select(x=>new CoverageRow(x.Key,x.Start,x.End,x.BodySha256)).ToArray();
        ReviewCoverageSignatureCases.AssertContract(item,new(rows,inputs.SetEquals(reference)));
    }

    private static IReadOnlySet<ReviewCoverageSignature> Describe(IReadOnlyList<CoverageInput> inputs)
    {
        if(inputs.Count==1)
        {
            var item=inputs[0];
            return new HashSet<ReviewCoverageSignature>{ReviewLaunchService.CoverageSignature(item.Text,item.Key,item.Start,item.End)};
        }
        // All multi-row fixtures carry explicit offsets as actual reusable callers do.
        return ReviewLaunchService.CoverageSignature(inputs.Select(x=>new ReviewEvidenceUnit(x.Key!,x.File,x.FullSha,x.Start,x.End!.Value,x.Text)));
    }
}
