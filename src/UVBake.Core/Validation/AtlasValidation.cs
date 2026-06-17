using UVBake.Core.Parameterization;

namespace UVBake.Core.Validation;

/// <summary>
/// The hard-gate report for a packed atlas (Documents/07 §1): no flips, no inter-chart overlap, full
/// coverage. <see cref="IsValid"/> must hold before an atlas is emitted — a failure is a corruption bug,
/// not a quality knob.
/// </summary>
public sealed record AtlasValidation(
    bool OverlapFree,
    int OverlapCount,
    int FlipCount,
    bool FullCoverage,
    int TriangleCount,
    double PackingEfficiency)
{
    /// <summary>True when all three hard gates pass.</summary>
    public bool IsValid => OverlapFree && FlipCount == 0 && FullCoverage;
}

/// <summary>Runs the hard gates over the packed charts.</summary>
public static class AtlasValidator
{
    public static AtlasValidation Validate(
        IReadOnlyList<ChartUv> charts,
        int expectedTriangleCount,
        double packingEfficiency)
    {
        ArgumentNullException.ThrowIfNull(charts);

        int flips = 0;
        int triangles = 0;
        foreach (ChartUv chart in charts)
        {
            flips += DistortionMetrics.ForChart(chart).FlipCount;
            triangles += chart.FaceCount;
        }

        int overlaps = OverlapValidator.CountInterChartOverlaps(charts);
        return new AtlasValidation(
            OverlapFree: overlaps == 0,
            OverlapCount: overlaps,
            FlipCount: flips,
            FullCoverage: triangles == expectedTriangleCount,
            TriangleCount: triangles,
            PackingEfficiency: packingEfficiency);
    }
}
