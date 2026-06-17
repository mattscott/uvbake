using UVBake.Core.Geometry;
using UVBake.Core.Meshes;
using UVBake.Core.Solvers;

namespace UVBake.Core.Parameterization;

/// <summary>
/// Tutte / barycentric embedding (Documents/04 §2.1): fix the boundary to a convex polygon and place
/// each interior vertex at a convex combination of its neighbours. With positive weights and a convex
/// boundary, Tutte's theorem guarantees a <b>provably bijective</b> (flip- and overlap-free) embedding —
/// the project's only free injectivity guarantee, and the flip-free seed the SLIM polish builds on.
/// Uniform weights are used here (always positive ⇒ guaranteed bijective); they trade some distortion
/// for that guarantee, which LSCM/SLIM then reduce. The boundary maps to a circle by 3D chord length.
/// </summary>
public static class TutteParameterizer
{
    /// <summary>Flattens the chart given by <paramref name="chartFaces"/> into an injective UV layout.</summary>
    public static ChartUv Parameterize(HalfEdgeMesh mesh, IReadOnlyList<int> chartFaces, ISparseSolver? solver = null)
    {
        ChartTopology topology = ChartTopology.Build(mesh, chartFaces);
        return Parameterize(topology, solver);
    }

    /// <summary>Flattens a chart from its precomputed <see cref="ChartTopology"/>.</summary>
    public static ChartUv Parameterize(ChartTopology topology, ISparseSolver? solver = null)
    {
        ArgumentNullException.ThrowIfNull(topology);
        solver ??= new ConjugateGradientSolver();

        var uv = new Vector2[topology.VertexCount];
        PlaceBoundaryOnCircle(topology, uv);
        SolveInterior(topology, uv, solver);
        OrientPositively(topology, uv);

        return new ChartUv((int[])topology.LocalToGlobal.Clone(), (Vector3[])topology.Position.Clone(), uv, (int[])topology.Triangles.Clone());
    }

    private static void PlaceBoundaryOnCircle(ChartTopology topology, Vector2[] uv)
    {
        int[] loop = topology.BoundaryLoop;
        if (loop.Length == 0)
        {
            return;
        }

        const double radius = 0.5;
        var cumulative = new double[loop.Length + 1];
        for (int k = 0; k < loop.Length; k++)
        {
            int a = loop[k];
            int b = loop[(k + 1) % loop.Length];
            cumulative[k + 1] = cumulative[k] + (topology.Position[b] - topology.Position[a]).Length;
        }

        double total = cumulative[loop.Length];
        for (int k = 0; k < loop.Length; k++)
        {
            double t = total > 0 ? cumulative[k] / total : (double)k / loop.Length;
            double angle = 2 * Math.PI * t;
            uv[loop[k]] = new Vector2(radius * Math.Cos(angle), radius * Math.Sin(angle));
        }
    }

    private static void SolveInterior(ChartTopology topology, Vector2[] uv, ISparseSolver solver)
    {
        // Map interior local vertices to a contiguous solve index.
        var interiorIndex = new int[topology.VertexCount];
        Array.Fill(interiorIndex, -1);
        int m = 0;
        for (int v = 0; v < topology.VertexCount; v++)
        {
            if (!topology.IsBoundary[v])
            {
                interiorIndex[v] = m++;
            }
        }

        if (m == 0)
        {
            return; // every vertex is on the boundary (already placed)
        }

        var builder = new SparseMatrixBuilder(m);
        var rhsU = new double[m];
        var rhsV = new double[m];

        for (int v = 0; v < topology.VertexCount; v++)
        {
            int row = interiorIndex[v];
            if (row < 0)
            {
                continue;
            }

            foreach (int neighbor in topology.Neighbors[v])
            {
                builder.Add(row, row, 1.0); // diagonal accumulates Σ weights (uniform weight 1)
                int col = interiorIndex[neighbor];
                if (col >= 0)
                {
                    builder.Add(row, col, -1.0);
                }
                else
                {
                    rhsU[row] += uv[neighbor].X;
                    rhsV[row] += uv[neighbor].Y;
                }
            }
        }

        IFactorization factorization = solver.Factor(builder.Build()); // factor once, solve both coordinates
        double[] solvedU = factorization.Solve(rhsU);
        double[] solvedV = factorization.Solve(rhsV);

        for (int v = 0; v < topology.VertexCount; v++)
        {
            int row = interiorIndex[v];
            if (row >= 0)
            {
                uv[v] = new Vector2(solvedU[row], solvedV[row]);
            }
        }
    }

    private static void OrientPositively(ChartTopology topology, Vector2[] uv)
    {
        // Tutte yields a consistent orientation; reflect if it came out mirrored so det J > 0 everywhere.
        double signedArea = 0;
        int[] tris = topology.Triangles;
        for (int t = 0; t < tris.Length; t += 3)
        {
            Vector2 a = uv[tris[t]];
            Vector2 b = uv[tris[t + 1]];
            Vector2 c = uv[tris[t + 2]];
            signedArea += (b - a).Cross(c - a);
        }

        if (signedArea < 0)
        {
            for (int v = 0; v < uv.Length; v++)
            {
                uv[v] = new Vector2(-uv[v].X, uv[v].Y);
            }
        }
    }
}
