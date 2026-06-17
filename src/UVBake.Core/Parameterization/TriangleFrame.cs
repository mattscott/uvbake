using UVBake.Core.Geometry;

namespace UVBake.Core.Parameterization;

/// <summary>
/// A triangle's 3D shape expressed in a local isometric 2D frame: <c>p0 → (0,0)</c>,
/// <c>p1 → (Len1, 0)</c>, <c>p2 → (P2x, P2y)</c>. This is the reference domain for the map Jacobian —
/// LSCM assembles its Cauchy-Riemann rows from it, SLIM differentiates the energy through it, and the
/// distortion metrics read singular values off it. <c>DetD = 2·area</c>.
/// </summary>
public readonly struct TriangleFrame
{
    public TriangleFrame(double len1, double p2x, double p2y)
    {
        Len1 = len1;
        P2x = p2x;
        P2y = p2y;
    }

    public double Len1 { get; }

    public double P2x { get; }

    public double P2y { get; }

    /// <summary>Determinant of the local edge matrix = twice the 3D area.</summary>
    public double DetD => Len1 * P2y;

    /// <summary>3D area of the triangle.</summary>
    public double Area => 0.5 * DetD;

    /// <summary>True if the triangle is degenerate (zero area), so the frame is unusable.</summary>
    public bool IsDegenerate => DetD <= 0;

    public static TriangleFrame From(Vector3 p0, Vector3 p1, Vector3 p2)
    {
        Vector3 e1 = p1 - p0;
        Vector3 e2 = p2 - p0;
        double len1 = e1.Length;
        if (len1 == 0)
        {
            return new TriangleFrame(0, 0, 0);
        }

        Vector3 axis = e1 / len1;
        double p2x = e2.Dot(axis);
        double p2y = (e2 - (axis * p2x)).Length;
        return new TriangleFrame(len1, p2x, p2y);
    }

    /// <summary>
    /// The 2×2 map Jacobian J = Quv · D⁻¹ for the UV triangle (<paramref name="uv0"/>, <paramref name="uv1"/>,
    /// <paramref name="uv2"/>), returned row-major (j11, j12, j21, j22). Row 1 is ∇u, row 2 is ∇v.
    /// </summary>
    public (double J11, double J12, double J21, double J22) Jacobian(Vector2 uv0, Vector2 uv1, Vector2 uv2)
    {
        // D⁻¹ = (1/DetD) [[P2y, -P2x], [0, Len1]].
        double dinv11 = P2y / DetD;
        double dinv12 = -P2x / DetD;
        double dinv22 = Len1 / DetD;

        double a = uv1.X - uv0.X, b = uv2.X - uv0.X;
        double c = uv1.Y - uv0.Y, d = uv2.Y - uv0.Y;

        double j11 = a * dinv11;
        double j12 = (a * dinv12) + (b * dinv22);
        double j21 = c * dinv11;
        double j22 = (c * dinv12) + (d * dinv22);
        return (j11, j12, j21, j22);
    }
}
