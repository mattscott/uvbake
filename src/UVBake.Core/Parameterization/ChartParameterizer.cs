using UVBake.Core.Meshes;
using UVBake.Core.Solvers;

namespace UVBake.Core.Parameterization;

/// <summary>
/// The per-chart flattening pipeline (Documents/04 §5): <b>LSCM</b> conformal init → <b>Tutte</b>
/// guaranteed-injective fallback if LSCM produced a flip → <b>SLIM</b> symmetric-Dirichlet polish for
/// low, near-isometric distortion. The result is locally injective (no flips); the final validation is
/// a defensive check that falls back to the injective seed if the polish ever regressed.
/// </summary>
public static class ChartParameterizer
{
    /// <summary>Flattens one chart to an injective, low-distortion UV layout.</summary>
    public static ChartUv Parameterize(
        HalfEdgeMesh mesh,
        IReadOnlyList<int> chartFaces,
        ISparseSolver? solver = null,
        int slimIterations = 40)
    {
        ChartTopology topology = ChartTopology.Build(mesh, chartFaces);
        return Parameterize(topology, solver, slimIterations);
    }

    /// <summary>Flattens a chart from its precomputed topology.</summary>
    public static ChartUv Parameterize(ChartTopology topology, ISparseSolver? solver = null, int slimIterations = 40)
    {
        ArgumentNullException.ThrowIfNull(topology);
        solver ??= new ConjugateGradientSolver();

        ChartUv seed = LscmParameterizer.Parameterize(topology, solver);
        if (!InjectivityValidator.IsLocallyInjective(seed))
        {
            seed = TutteParameterizer.Parameterize(topology, solver); // guaranteed flip-free
        }

        ChartUv polished = SlimParameterizer.Polish(seed, slimIterations);

        // The barrier should keep the polish injective; if not, keep the injective seed.
        return InjectivityValidator.IsLocallyInjective(polished) ? polished : seed;
    }
}
