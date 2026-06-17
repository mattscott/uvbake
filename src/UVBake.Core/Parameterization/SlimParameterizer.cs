using UVBake.Core.Geometry;

namespace UVBake.Core.Parameterization;

/// <summary>
/// SLIM-style polish (Rabinovich et al. 2017, Documents/04 §3.2): minimize the <b>symmetric Dirichlet</b>
/// energy <c>‖J‖²_F + ‖J⁻¹‖²_F = σ₁²+σ₂²+1/σ₁²+1/σ₂²</c>, which → ∞ as any triangle degenerates
/// (det J → 0⁺) — a built-in flip barrier. From a locally-injective seed (Tutte or a flip-free LSCM),
/// each iteration takes a gradient-descent direction and a <b>flip-preventing line search</b>: the
/// closed-form maximum step before any triangle's signed area reaches zero, then Armijo backtracking on
/// the energy. Because no step ever crosses det J = 0, the map stays locally injective for every
/// iteration. Fixed iteration count + sequential reductions ⇒ deterministic (Documents/06 §5).
/// <para>This is plain gradient descent under the SLIM energy and flip barrier; the full SLIM
/// reweighted-proxy Newton direction is a convergence-speed refinement, not a correctness one.</para>
/// </summary>
public static class SlimParameterizer
{
    private const double ArmijoC1 = 1e-4;

    /// <summary>Polishes a flip-free seed layout, returning a lower-distortion (still injective) layout.</summary>
    public static ChartUv Polish(ChartUv seed, int iterations = 40)
    {
        ArgumentNullException.ThrowIfNull(seed);

        int[] tris = seed.Triangles;
        var frames = new TriangleFrame[tris.Length / 3];
        for (int t = 0; t < frames.Length; t++)
        {
            frames[t] = TriangleFrame.From(seed.Position3D[tris[3 * t]], seed.Position3D[tris[(3 * t) + 1]], seed.Position3D[tris[(3 * t) + 2]]);
        }

        var uv = (Vector2[])seed.Uv.Clone();
        int n = uv.Length;
        var gradU = new double[n];
        var gradV = new double[n];

        for (int iteration = 0; iteration < iterations; iteration++)
        {
            double energy = EvaluateEnergyAndGradient(frames, tris, uv, gradU, gradV);
            if (double.IsInfinity(energy))
            {
                break; // seed was not flip-free; nothing safe to do
            }

            // Descent direction d = −grad; directional derivative grad·d = −‖grad‖².
            double gradNormSq = 0;
            for (int i = 0; i < n; i++)
            {
                gradNormSq += (gradU[i] * gradU[i]) + (gradV[i] * gradV[i]);
            }

            if (gradNormSq < 1e-20)
            {
                break; // converged
            }

            double maxStep = MaxFlipFreeStep(frames, tris, uv, gradU, gradV);
            double trial = double.IsInfinity(maxStep) ? 1.0 : Math.Min(1.0, 0.9 * maxStep);

            double directional = -gradNormSq; // grad · d
            bool advanced = false;
            var candidate = new Vector2[n];
            while (trial > 1e-14)
            {
                for (int i = 0; i < n; i++)
                {
                    candidate[i] = new Vector2(uv[i].X - (trial * gradU[i]), uv[i].Y - (trial * gradV[i]));
                }

                double candidateEnergy = EvaluateEnergy(frames, tris, candidate);
                if (candidateEnergy <= energy + (ArmijoC1 * trial * directional))
                {
                    Array.Copy(candidate, uv, n);
                    advanced = true;
                    break;
                }

                trial *= 0.5;
            }

            if (!advanced)
            {
                break; // line search stalled
            }
        }

        return new ChartUv((int[])seed.LocalToGlobal.Clone(), (Vector3[])seed.Position3D.Clone(), uv, (int[])tris.Clone());
    }

    /// <summary>Symmetric-Dirichlet energy of the whole chart (∞ if any triangle is non-positively oriented).</summary>
    internal static double EvaluateEnergy(TriangleFrame[] frames, int[] tris, Vector2[] uv)
    {
        double total = 0;
        for (int t = 0; t < frames.Length; t++)
        {
            TriangleFrame frame = frames[t];
            if (frame.IsDegenerate)
            {
                continue;
            }

            (double j11, double j12, double j21, double j22) = frame.Jacobian(uv[tris[3 * t]], uv[tris[(3 * t) + 1]], uv[tris[(3 * t) + 2]]);
            double g = (j11 * j22) - (j12 * j21);
            if (g <= 0)
            {
                return double.PositiveInfinity;
            }

            double f = (j11 * j11) + (j12 * j12) + (j21 * j21) + (j22 * j22);
            total += frame.Area * (f + (f / (g * g)));
        }

        return total;
    }

    /// <summary>Energy and its gradient w.r.t. every vertex UV. Returns the energy.</summary>
    internal static double EvaluateEnergyAndGradient(TriangleFrame[] frames, int[] tris, Vector2[] uv, double[] gradU, double[] gradV)
    {
        Array.Clear(gradU);
        Array.Clear(gradV);
        double total = 0;

        for (int t = 0; t < frames.Length; t++)
        {
            TriangleFrame frame = frames[t];
            if (frame.IsDegenerate)
            {
                continue;
            }

            int l0 = tris[3 * t], l1 = tris[(3 * t) + 1], l2 = tris[(3 * t) + 2];
            (double j11, double j12, double j21, double j22) = frame.Jacobian(uv[l0], uv[l1], uv[l2]);
            double g = (j11 * j22) - (j12 * j21);
            if (g <= 0)
            {
                return double.PositiveInfinity;
            }

            double f = (j11 * j11) + (j12 * j12) + (j21 * j21) + (j22 * j22);
            double invG2 = 1.0 / (g * g);
            total += frame.Area * (f + (f * invG2));

            // ∂E/∂J = 2A[ (1 + 1/g²) J − (f/g³) cof(J) ],  cof(J) = [[j22,−j21],[−j12,j11]].
            double coef1 = 1.0 + invG2;
            double coefC = f * invG2 / g;
            double twoA = frame.DetD; // = 2·area
            double g11 = twoA * ((coef1 * j11) - (coefC * j22));
            double g12 = twoA * ((coef1 * j12) + (coefC * j21));
            double g21 = twoA * ((coef1 * j21) + (coefC * j12));
            double g22 = twoA * ((coef1 * j22) - (coefC * j11));

            double dinv11 = frame.P2y / frame.DetD;
            double dinv12 = -frame.P2x / frame.DetD;
            double dinv22 = frame.Len1 / frame.DetD;

            // Chain rule through J(uv); row 1 (j11,j12) depends on u, row 2 (j21,j22) on v.
            gradU[l0] += (g11 * -dinv11) + (g12 * (-dinv12 - dinv22));
            gradU[l1] += (g11 * dinv11) + (g12 * dinv12);
            gradU[l2] += g12 * dinv22;

            gradV[l0] += (g21 * -dinv11) + (g22 * (-dinv12 - dinv22));
            gradV[l1] += (g21 * dinv11) + (g22 * dinv12);
            gradV[l2] += g22 * dinv22;
        }

        return total;
    }

    /// <summary>
    /// Largest step <c>s ≥ 0</c> along <c>−grad</c> before any triangle's signed area hits zero. The
    /// signed UV area is quadratic in <c>s</c> (<c>α + βs + γs²</c>); the cap is the smallest positive
    /// root over all triangles, or +∞ if none can flip.
    /// </summary>
    private static double MaxFlipFreeStep(TriangleFrame[] frames, int[] tris, Vector2[] uv, double[] gradU, double[] gradV)
    {
        double maxStep = double.PositiveInfinity;
        for (int t = 0; t < frames.Length; t++)
        {
            int l0 = tris[3 * t], l1 = tris[(3 * t) + 1], l2 = tris[(3 * t) + 2];

            double a = uv[l1].X - uv[l0].X, b = uv[l2].X - uv[l0].X;
            double c = uv[l1].Y - uv[l0].Y, d = uv[l2].Y - uv[l0].Y;
            // Direction is −grad.
            double da = -(gradU[l1] - gradU[l0]), db = -(gradU[l2] - gradU[l0]);
            double dc = -(gradV[l1] - gradV[l0]), dd = -(gradV[l2] - gradV[l0]);

            double alpha = (a * d) - (b * c);
            double beta = (a * dd) + (da * d) - (b * dc) - (db * c);
            double gamma = (da * dd) - (db * dc);

            double root = SmallestPositiveRoot(gamma, beta, alpha);
            if (root < maxStep)
            {
                maxStep = root;
            }
        }

        return maxStep;
    }

    /// <summary>Smallest positive root of <c>γs² + βs + α = 0</c> with <c>α &gt; 0</c>, or +∞ if none.</summary>
    private static double SmallestPositiveRoot(double gamma, double beta, double alpha)
    {
        const double eps = 1e-300;
        if (Math.Abs(gamma) < eps)
        {
            // Linear βs + α = 0. With α > 0, a positive root needs β < 0.
            return beta < 0 ? -alpha / beta : double.PositiveInfinity;
        }

        double disc = (beta * beta) - (4 * gamma * alpha);
        if (disc < 0)
        {
            return double.PositiveInfinity; // never reaches zero area
        }

        double sqrt = Math.Sqrt(disc);
        double r1 = (-beta - sqrt) / (2 * gamma);
        double r2 = (-beta + sqrt) / (2 * gamma);

        double best = double.PositiveInfinity;
        if (r1 > 0 && r1 < best)
        {
            best = r1;
        }

        if (r2 > 0 && r2 < best)
        {
            best = r2;
        }

        return best;
    }
}
