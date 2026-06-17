namespace UVBake.Core.Parameterization;

/// <summary>
/// The hard injectivity gate at the chart level: counts flipped/degenerate triangles (det J ≤ 0). A
/// locally injective chart has zero flips. Tutte guarantees this by construction; the validator is the
/// post-hoc check the research mandates regardless of the method used (Documents/04 §1, 07). Inter-chart
/// overlap and boundary self-intersection are validated globally at packing time (P4).
/// </summary>
public static class InjectivityValidator
{
    /// <summary>Number of triangles with det J ≤ 0 (flipped or degenerate).</summary>
    public static int CountFlips(ChartUv chart) => DistortionMetrics.ForChart(chart).FlipCount;

    /// <summary>True if the chart has no flipped or degenerate triangles.</summary>
    public static bool IsLocallyInjective(ChartUv chart) => CountFlips(chart) == 0;
}
