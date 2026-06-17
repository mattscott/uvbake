using UVBake.Core.Geometry;

namespace UVBake.Core.Parameterization;

/// <summary>Per-triangle distortion: the Jacobian's singular values and the signed area ratio (det J).</summary>
public readonly record struct TriangleDistortion(double Sigma1, double Sigma2, double SignedAreaRatio)
{
    /// <summary>True if the triangle is flipped or degenerate (det J ≤ 0) — a local-injectivity violation.</summary>
    public bool IsFlipped => !(SignedAreaRatio > 0);

    /// <summary>RMS stretch L² = √((σ₁²+σ₂²)/2) (Sander et al.).</summary>
    public double L2 => Math.Sqrt(((Sigma1 * Sigma1) + (Sigma2 * Sigma2)) / 2);

    /// <summary>Worst-case stretch L∞ = σ₁.</summary>
    public double LInfinity => Sigma1;

    /// <summary>Quasi-conformal dilatation K = σ₁/σ₂ (1 = conformal).</summary>
    public double QuasiConformal => Sigma2 > 0 ? Sigma1 / Sigma2 : double.PositiveInfinity;
}

/// <summary>Aggregate distortion over a chart (Documents/04 §4).</summary>
public sealed record ChartDistortion(
    int FlipCount,
    double AreaWeightedL2,
    double MaxLInfinity,
    double MaxQuasiConformal,
    bool HasInvalidValues);

/// <summary>
/// Computes the 2×2 map Jacobian per triangle and its singular values. The 3D triangle is placed in a
/// local isometric 2D frame (edge lengths preserved); J maps that frame to the UV triangle, so its
/// singular values σ₁ ≥ σ₂ are the true local stretch and sign(det J) is the flip test (Documents/04 §4).
/// </summary>
public static class DistortionMetrics
{
    public static TriangleDistortion ForTriangle(Vector3 p0, Vector3 p1, Vector3 p2, Vector2 q0, Vector2 q1, Vector2 q2)
    {
        // Isometric local frame of the 3D triangle: p0→(0,0), p1→(len1,0), p2→(p2x,p2y).
        Vector3 e1 = p1 - p0;
        Vector3 e2 = p2 - p0;
        double len1 = e1.Length;
        if (len1 == 0)
        {
            return new TriangleDistortion(double.NaN, double.NaN, 0);
        }

        Vector3 axis = e1 / len1;
        double p2x = e2.Dot(axis);
        double perp = (e2 - (axis * p2x)).Length;
        double detD = len1 * perp; // = 2 · area3D
        if (detD == 0)
        {
            return new TriangleDistortion(double.NaN, double.NaN, 0);
        }

        // J = Quv · D⁻¹, with Quv the UV edge matrix and D the local-frame edge matrix.
        double a = q1.X - q0.X, b = q2.X - q0.X;
        double c = q1.Y - q0.Y, d = q2.Y - q0.Y;

        double j11 = a * perp / detD;
        double j12 = ((-a * p2x) + (b * len1)) / detD;
        double j21 = c * perp / detD;
        double j22 = ((-c * p2x) + (d * len1)) / detD;

        double frobenius2 = (j11 * j11) + (j12 * j12) + (j21 * j21) + (j22 * j22);
        double det = (j11 * j22) - (j12 * j21);
        double disc = Math.Sqrt(Math.Max(0, (frobenius2 * frobenius2) - (4 * det * det)));
        double sigma1 = Math.Sqrt(Math.Max(0, (frobenius2 + disc) / 2));
        double sigma2 = Math.Sqrt(Math.Max(0, (frobenius2 - disc) / 2));

        // det J = det(Quv)/det(D); det(D) > 0, so its sign is the UV-triangle orientation.
        double signedAreaRatio = det;
        return new TriangleDistortion(sigma1, sigma2, signedAreaRatio);
    }

    public static ChartDistortion ForChart(ChartUv chart)
    {
        ArgumentNullException.ThrowIfNull(chart);

        int flips = 0;
        double areaSum = 0, weightedL2Sq = 0, maxLInf = 0, maxK = 0;
        bool invalid = false;

        int[] tris = chart.Triangles;
        for (int t = 0; t < tris.Length; t += 3)
        {
            Vector3 p0 = chart.Position3D[tris[t]];
            Vector3 p1 = chart.Position3D[tris[t + 1]];
            Vector3 p2 = chart.Position3D[tris[t + 2]];
            double area3D = 0.5 * (p1 - p0).Cross(p2 - p0).Length;

            TriangleDistortion dist = ForTriangle(
                p0, p1, p2, chart.Uv[tris[t]], chart.Uv[tris[t + 1]], chart.Uv[tris[t + 2]]);

            if (dist.IsFlipped)
            {
                flips++;
            }

            if (double.IsNaN(dist.Sigma1) || double.IsInfinity(dist.Sigma1) ||
                double.IsNaN(dist.Sigma2) || double.IsInfinity(dist.Sigma2))
            {
                invalid = true;
                continue;
            }

            areaSum += area3D;
            weightedL2Sq += area3D * dist.L2 * dist.L2;
            maxLInf = Math.Max(maxLInf, dist.LInfinity);
            if (!double.IsInfinity(dist.QuasiConformal))
            {
                maxK = Math.Max(maxK, dist.QuasiConformal);
            }
        }

        double areaWeightedL2 = areaSum > 0 ? Math.Sqrt(weightedL2Sq / areaSum) : 0;
        return new ChartDistortion(flips, areaWeightedL2, maxLInf, maxK, invalid);
    }
}
