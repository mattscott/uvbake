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
/// emit per-wedge UVs. Charts are parameterized serially for determinism; parallel-across-charts is P6.
/// </summary>
public static class UVBaker
{
    /// <summary>Generates a UV atlas for <paramref name="input"/> under <paramref name="options"/>.</summary>
    public static UvMesh Generate(Mesh input, UVBakeOptions options)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(options);

        var solver = new ConjugateGradientSolver();

        Mesh triangulated = Triangulator.Triangulate(input);
        HalfEdgeMesh mesh = HalfEdgeMesh.Build(triangulated);
        HardEdgeDetector.Detect(mesh, input.Creases, options.HardEdgeAngleDegrees, options.Seams);
        SegmentationResult segmentation = ChartSegmenter.Segment(mesh, options);

        var charts = new List<ChartUv>(segmentation.Charts.Count);
        foreach (Chart chart in segmentation.Charts)
        {
            charts.Add(ChartParameterizer.Parameterize(mesh, chart.Faces, solver));
        }

        TexelDensityNormalizer.Normalize(charts);
        PackResult pack = options.Packer == PackerKind.Silhouette
            ? SilhouettePacker.Pack(charts, options)
            : AtlasPacker.Pack(charts, options);
        AtlasValidation validation = AtlasValidator.Validate(charts, triangulated.Faces.Count, pack.Efficiency);

        Mesh output = BuildPerWedgeMesh(triangulated.Positions, charts);
        return new UvMesh(output, pack.Resolution, validation);
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
