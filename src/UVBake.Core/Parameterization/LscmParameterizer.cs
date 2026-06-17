using UVBake.Core.Geometry;
using UVBake.Core.Meshes;
using UVBake.Core.Solvers;

namespace UVBake.Core.Parameterization;

/// <summary>
/// Least-Squares Conformal Maps (Lévy et al. 2002, Documents/04 §2.2). Minimizes the area-weighted
/// Cauchy-Riemann residual — quadratic in the vertex UVs — as a single sparse least-squares solve with a
/// <b>free boundary</b> (its advantage over Tutte). The 4-DOF similarity null space is removed by pinning
/// two boundary vertices to distinct points (which also rules out the trivial collapse). The least-squares
/// problem is solved through the <see cref="ISparseSolver"/> normal equations MᵀM x = −MᵀM_pin·x_pin.
/// <para>Conformal ⇒ near-zero angle distortion, but no area control and (rarely) a flip on a bad
/// triangle — so it is an initializer; SLIM polishes it and Tutte is the guaranteed-injective fallback.</para>
/// </summary>
public static class LscmParameterizer
{
    public static ChartUv Parameterize(HalfEdgeMesh mesh, IReadOnlyList<int> chartFaces, ISparseSolver? solver = null)
        => Parameterize(ChartTopology.Build(mesh, chartFaces), solver);

    public static ChartUv Parameterize(ChartTopology topology, ISparseSolver? solver = null)
    {
        ArgumentNullException.ThrowIfNull(topology);
        solver ??= new ConjugateGradientSolver();

        int v = topology.VertexCount;
        (int pin0, int pin1) = ChoosePins(topology);
        double pinScale = (topology.Position[pin1] - topology.Position[pin0]).Length;
        if (pinScale == 0)
        {
            pinScale = 1;
        }

        // DOF layout: u_i → i, v_i → V + i. Pin (u,v) of both pins → 4 fixed values.
        // pin0 → (0,0), pin1 → (pinScale, 0).
        int Dof(int vertex, bool isV) => isV ? v + vertex : vertex;
        var pinnedValue = new Dictionary<int, double>
        {
            [Dof(pin0, false)] = 0,
            [Dof(pin0, true)] = 0,
            [Dof(pin1, false)] = pinScale,
            [Dof(pin1, true)] = 0,
        };

        var freeIndex = new int[2 * v];
        int nFree = 0;
        for (int dof = 0; dof < 2 * v; dof++)
        {
            freeIndex[dof] = pinnedValue.ContainsKey(dof) ? -1 : nFree++;
        }

        var builder = new SparseMatrixBuilder(nFree);
        var rhs = new double[nFree];
        var row = new List<(int Dof, double Coeff)>(6);

        int[] tris = topology.Triangles;
        for (int t = 0; t < tris.Length; t += 3)
        {
            int l0 = tris[t], l1 = tris[t + 1], l2 = tris[t + 2];
            TriangleFrame frame = TriangleFrame.From(topology.Position[l0], topology.Position[l1], topology.Position[l2]);
            if (frame.IsDegenerate)
            {
                continue;
            }

            double inv = 1.0 / (2.0 * Math.Sqrt(frame.Area));
            // Gradient coefficients of a linear field over the local frame.
            double yx0 = -frame.P2y, yx1 = frame.P2y, yx2 = 0;
            double yy0 = frame.P2x - frame.Len1, yy1 = -frame.P2x, yy2 = frame.Len1;

            // Equation 1: ∂u/∂x − ∂v/∂y = 0.
            row.Clear();
            AddTerm(row, Dof(l0, false), yx0 * inv);
            AddTerm(row, Dof(l1, false), yx1 * inv);
            AddTerm(row, Dof(l2, false), yx2 * inv);
            AddTerm(row, Dof(l0, true), -yy0 * inv);
            AddTerm(row, Dof(l1, true), -yy1 * inv);
            AddTerm(row, Dof(l2, true), -yy2 * inv);
            AccumulateRow(row, freeIndex, pinnedValue, builder, rhs);

            // Equation 2: ∂u/∂y + ∂v/∂x = 0.
            row.Clear();
            AddTerm(row, Dof(l0, false), yy0 * inv);
            AddTerm(row, Dof(l1, false), yy1 * inv);
            AddTerm(row, Dof(l2, false), yy2 * inv);
            AddTerm(row, Dof(l0, true), yx0 * inv);
            AddTerm(row, Dof(l1, true), yx1 * inv);
            AddTerm(row, Dof(l2, true), yx2 * inv);
            AccumulateRow(row, freeIndex, pinnedValue, builder, rhs);
        }

        double[] solution = solver.Factor(builder.Build()).Solve(rhs);

        var uv = new Vector2[v];
        for (int i = 0; i < v; i++)
        {
            double uValue = Value(Dof(i, false), freeIndex, pinnedValue, solution);
            double vValue = Value(Dof(i, true), freeIndex, pinnedValue, solution);
            uv[i] = new Vector2(uValue, vValue);
        }

        UvOrientation.MakePositive(tris, uv);
        return new ChartUv((int[])topology.LocalToGlobal.Clone(), (Vector3[])topology.Position.Clone(), uv, (int[])tris.Clone());
    }

    private static (int Pin0, int Pin1) ChoosePins(ChartTopology topology)
    {
        // Pin the lowest-index boundary vertex and the boundary vertex farthest from it (in 3D).
        int pin0 = -1;
        for (int b = 0; b < topology.BoundaryLoop.Length; b++)
        {
            int candidate = topology.BoundaryLoop[b];
            if (pin0 < 0 || candidate < pin0)
            {
                pin0 = candidate;
            }
        }

        if (pin0 < 0)
        {
            return (0, Math.Min(1, topology.VertexCount - 1)); // no boundary (shouldn't happen for a disk)
        }

        int pin1 = pin0;
        double best = -1;
        foreach (int candidate in topology.BoundaryLoop)
        {
            double dist = (topology.Position[candidate] - topology.Position[pin0]).LengthSquared;
            if (dist > best)
            {
                best = dist;
                pin1 = candidate;
            }
        }

        return (pin0, pin1);
    }

    private static void AddTerm(List<(int Dof, double Coeff)> row, int dof, double coeff)
    {
        if (coeff != 0)
        {
            row.Add((dof, coeff));
        }
    }

    private static void AccumulateRow(
        List<(int Dof, double Coeff)> row,
        int[] freeIndex,
        Dictionary<int, double> pinnedValue,
        SparseMatrixBuilder builder,
        double[] rhs)
    {
        // c = contribution of pinned columns; folds into the RHS of the normal equations.
        double c = 0;
        foreach ((int dof, double coeff) in row)
        {
            if (pinnedValue.TryGetValue(dof, out double pinned))
            {
                c += coeff * pinned;
            }
        }

        foreach ((int dofA, double coeffA) in row)
        {
            int a = freeIndex[dofA];
            if (a < 0)
            {
                continue;
            }

            rhs[a] -= coeffA * c;
            foreach ((int dofB, double coeffB) in row)
            {
                int b = freeIndex[dofB];
                if (b >= 0)
                {
                    builder.Add(a, b, coeffA * coeffB); // accumulates MᵀM
                }
            }
        }
    }

    private static double Value(int dof, int[] freeIndex, Dictionary<int, double> pinnedValue, double[] solution)
    {
        int free = freeIndex[dof];
        return free >= 0 ? solution[free] : pinnedValue[dof];
    }
}
