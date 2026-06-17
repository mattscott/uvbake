using UVBake.Core.Geometry;
using UVBake.Core.Meshes;
using UVBake.Core.Packing;
using UVBake.Core.Parameterization;
using UVBake.Core.Segmentation;
using UVBake.Core.Solvers;
using UVBake.Core.Validation;

namespace UVBake.Core;

/// <summary>
/// The top-level orchestrator: turns a mesh with no UVs into the same mesh with a unique, bake-ready UV
/// atlas. The flow maps 1:1 to the pipeline phases (Documents/02, Plans/01):
/// triangulate → detect hard edges → segment into disk charts → parameterize each chart (injective,
/// low-distortion) → normalize texel density → pack with a reserved gutter → validate the hard gates →
/// emit per-wedge UVs. Charts are parameterized in parallel (independent, deterministic by index).
/// </summary>
public static class UVBaker
{
    /// <summary>Generates a UV atlas for <paramref name="input"/> under <paramref name="options"/>.</summary>
    public static UvMesh Generate(Mesh input, UVBakeOptions options)
        => Generate(input, options, progress: null, cancellationToken: default);

    /// <summary>
    /// Generates a UV atlas, reporting per-phase progress to <paramref name="progress"/> and honoring
    /// <paramref name="cancellationToken"/> (checked at phase boundaries and during the parallel chart
    /// parameterization). Cancellation throws <see cref="OperationCanceledException"/>.
    /// </summary>
    public static UvMesh Generate(
        Mesh input,
        UVBakeOptions options,
        IProgress<string>? progress,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(options);

        var solver = new ConjugateGradientSolver();

        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report("Triangulating");
        Mesh triangulated = Triangulator.Triangulate(input);
        HalfEdgeMesh mesh = HalfEdgeMesh.Build(triangulated);

        progress?.Report("Detecting hard edges");
        HardEdgeDetector.Detect(mesh, input.Creases, options.HardEdgeAngleDegrees, options.Seams);

        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report("Segmenting into charts");
        SegmentationResult segmentation = ChartSegmenter.Segment(mesh, options);

        // Charts are independent ⇒ parameterize across charts in parallel. Results are written by index,
        // so the output is order-independent; each chart's work is deterministic (no RNG, fixed iteration
        // counts, sequential reductions), so the parallel result is bit-identical to the serial one.
        IReadOnlyList<Chart> chartList = segmentation.Charts;
        progress?.Report($"Parameterizing {chartList.Count} charts");
        var charts = new ChartUv[chartList.Count];
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = options.Threads <= 0 ? -1 : options.Threads,
            CancellationToken = cancellationToken,
        };
        Parallel.For(0, chartList.Count, parallelOptions, i =>
        {
            charts[i] = ChartParameterizer.Parameterize(mesh, chartList[i].Faces, solver);
        });

        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report("Normalizing texel density");
        TexelDensityNormalizer.Normalize(charts);

        // A fixed target density may overflow one 0–1 tile → spill across UDIM tiles; otherwise auto-fit one tile.
        progress?.Report("Packing atlas");
        PackResult pack = options.TexelsPerUnit.HasValue
            ? AtlasPacker.PackUdim(charts, options)
            : options.Packer == PackerKind.Silhouette
                ? SilhouettePacker.Pack(charts, options)
                : AtlasPacker.Pack(charts, options);

        progress?.Report("Validating");
        AtlasValidation validation = AtlasValidator.Validate(charts, triangulated.Faces.Count, pack.Efficiency);

        Mesh output = BuildPerWedgeMesh(triangulated.Positions, charts);
        progress?.Report("Done");
        return new UvMesh(output, pack.Resolution, validation, pack.TileCount);
    }

    private static Mesh BuildPerWedgeMesh(IReadOnlyList<Vector3> positions, IReadOnlyList<ChartUv> charts)
    {
        var texCoords = new List<Vector2>();
        var faces = new List<Face>();

        foreach (ChartUv chart in charts)
        {
            int texOffset = texCoords.Count;
            texCoords.AddRange(chart.Uv);

            int[] tris = chart.Triangles;
            for (int t = 0; t < tris.Length; t += 3)
            {
                int l0 = tris[t], l1 = tris[t + 1], l2 = tris[t + 2];
                faces.Add(new Face(
                [
                    new Corner(chart.LocalToGlobal[l0], texOffset + l0, Corner.None),
                    new Corner(chart.LocalToGlobal[l1], texOffset + l1, Corner.None),
                    new Corner(chart.LocalToGlobal[l2], texOffset + l2, Corner.None),
                ]));
            }
        }

        return new Mesh(positions, texCoords, Array.Empty<Vector3>(), faces);
    }
}
