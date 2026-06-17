using UVBake.Core.Geometry;
using UVBake.Core.Meshes;

namespace UVBake.Core.Segmentation;

/// <summary>
/// Segments a mesh into the largest near-developable disk-topology charts the budget allows, with seams
/// forced onto hard edges (Documents/03). Bottom-up hierarchical face clustering (Garland et al. 2001):
/// start with one chart per triangle and greedily merge adjacent charts, subject to three rules —
/// <list type="number">
/// <item>never merge across a hard edge (so every hard edge stays a seam);</item>
/// <item>the merged region must stay within the developability budget (every face within a normal-cone
/// angle of the chart's area-weighted proxy normal);</item>
/// <item>the merged region must stay a topological disk.</item>
/// </list>
/// <para>
/// The disk test is exact and cheap: a connected triangulated surface is a disk iff its Euler
/// characteristic χ = 1. Merging two disks <c>A</c>, <c>B</c> gives
/// <c>χ(A∪B) = χ(A) + χ(B) − χ(A∩B) = 2 − (sharedVertices − sharedEdges)</c>, so the union is a disk iff
/// <c>sharedVertices − sharedEdges = 1</c> (they meet along a single boundary arc, not two arcs or a
/// pinch vertex). Because the invariant is enforced on every merge, charts are disks by construction —
/// genus &gt; 0 inputs (e.g. a torus) are simply covered by several disks, with no separate handle-cutting.
/// </para>
/// Seam routing (Seamster-style hiding of the non-hard cuts) and stretch-exact budgeting are later
/// refinements; this establishes the disk + hard-edge-seam + coverage guarantees the pipeline relies on.
/// </summary>
public sealed class ChartSegmenter
{
    private readonly HalfEdgeMesh _mesh;
    private readonly double _maxAngleRadians;

    // Per-chart working state, indexed by chart id (initially one per triangle).
    private readonly int[] _faceChart;
    private readonly List<int>[] _chartFaces;
    private readonly double[] _chartArea;
    private readonly Vector3[] _chartNormalAccum; // Σ area·normal (proxy = normalized)
    private readonly HashSet<int>[] _chartVertices;

    private ChartSegmenter(HalfEdgeMesh mesh, double maxAngleRadians)
    {
        _mesh = mesh;
        _maxAngleRadians = maxAngleRadians;

        int triCount = mesh.TriangleCount;
        _faceChart = new int[triCount];
        _chartFaces = new List<int>[triCount];
        _chartArea = new double[triCount];
        _chartNormalAccum = new Vector3[triCount];
        _chartVertices = new HashSet<int>[triCount];

        for (int t = 0; t < triCount; t++)
        {
            _faceChart[t] = t;
            _chartFaces[t] = [t];
            _chartArea[t] = mesh.FaceArea(t);
            _chartNormalAccum[t] = mesh.FaceNormal(t) * mesh.FaceArea(t);
            _chartVertices[t] = [mesh.Origin(3 * t), mesh.Origin((3 * t) + 1), mesh.Origin((3 * t) + 2)];
        }
    }

    /// <summary>
    /// Segments <paramref name="mesh"/> (hard edges already flagged) using a developability cone of
    /// <paramref name="maxAngleRadians"/> half-angle.
    /// </summary>
    public static SegmentationResult Segment(HalfEdgeMesh mesh, double maxAngleRadians)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        var segmenter = new ChartSegmenter(mesh, maxAngleRadians);
        segmenter.MergePasses();
        return segmenter.BuildResult();
    }

    /// <summary>
    /// Segments using <see cref="UVBakeOptions"/>. The developability cone is taken from
    /// <see cref="UVBakeOptions.MaxStretch"/> (radians) as a growth proxy; the exact stretch bound is
    /// enforced and measured once charts are parameterized (P3/P4).
    /// </summary>
    public static SegmentationResult Segment(HalfEdgeMesh mesh, UVBakeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return Segment(mesh, options.MaxStretch);
    }

    private void MergePasses()
    {
        bool changed = true;
        while (changed)
        {
            changed = false;
            for (int h = 0; h < _mesh.HalfEdgeCount; h++)
            {
                int twin = _mesh.Twin(h);
                if (twin <= h)
                {
                    continue; // boundary (-1) or the non-canonical half of an interior edge
                }

                if (_mesh.IsHard(h))
                {
                    continue; // a hard edge must remain a seam
                }

                int a = _faceChart[_mesh.TriangleOf(h)];
                int b = _faceChart[_mesh.TriangleOf(twin)];
                if (a != b && CanMerge(a, b))
                {
                    Merge(a, b);
                    changed = true;
                }
            }
        }
    }

    private bool CanMerge(int a, int b)
    {
        // Iterate the smaller chart's interface with the other to count shared edges (and reject if any
        // is hard — that would bury a hard edge inside the merged chart).
        int small = _chartFaces[a].Count <= _chartFaces[b].Count ? a : b;
        int other = small == a ? b : a;

        int sharedEdges = 0;
        foreach (int face in _chartFaces[small])
        {
            for (int i = 0; i < 3; i++)
            {
                int h = (3 * face) + i;
                int twin = _mesh.Twin(h);
                if (twin < 0 || _faceChart[_mesh.TriangleOf(twin)] != other)
                {
                    continue;
                }

                if (_mesh.IsHard(h))
                {
                    return false;
                }

                sharedEdges++;
            }
        }

        // Shared vertices (includes any pinch vertex not on the interface, which χ must catch).
        int sharedVertices = 0;
        foreach (int v in _chartVertices[small])
        {
            if (_chartVertices[other].Contains(v))
            {
                sharedVertices++;
            }
        }

        // Disk iff χ(A∪B) = 2 − (sharedVertices − sharedEdges) = 1.
        if (sharedVertices - sharedEdges != 1)
        {
            return false;
        }

        return WithinBudget(a, b);
    }

    private bool WithinBudget(int a, int b)
    {
        Vector3 proxy = (_chartNormalAccum[a] + _chartNormalAccum[b]).Normalized();
        if (proxy == Vector3.Zero)
        {
            return false; // fully degenerate accumulation — keep them apart
        }

        double cosLimit = Math.Cos(_maxAngleRadians);
        return AllFacesWithin(a, proxy, cosLimit) && AllFacesWithin(b, proxy, cosLimit);
    }

    private bool AllFacesWithin(int chart, Vector3 proxy, double cosLimit)
    {
        foreach (int face in _chartFaces[chart])
        {
            // dot ≥ cos(limit) ⇔ angle ≤ limit. A degenerate (zero) face normal has dot 0 → excluded.
            if (_mesh.FaceNormal(face).Dot(proxy) < cosLimit)
            {
                return false;
            }
        }

        return true;
    }

    private void Merge(int a, int b)
    {
        // Merge the smaller chart into the larger to keep the per-face relabel cheap.
        int target = _chartFaces[a].Count >= _chartFaces[b].Count ? a : b;
        int source = target == a ? b : a;

        foreach (int face in _chartFaces[source])
        {
            _faceChart[face] = target;
            _chartFaces[target].Add(face);
        }

        _chartArea[target] += _chartArea[source];
        _chartNormalAccum[target] += _chartNormalAccum[source];
        _chartVertices[target].UnionWith(_chartVertices[source]);

        // Retire the source chart.
        _chartFaces[source] = [];
        _chartVertices[source] = [];
        _chartArea[source] = 0;
        _chartNormalAccum[source] = Vector3.Zero;
    }

    private SegmentationResult BuildResult()
    {
        // Number final charts by their lowest member face index, so ids are deterministic.
        var liveRoots = new List<int>();
        for (int t = 0; t < _mesh.TriangleCount; t++)
        {
            if (_chartFaces[t].Count > 0)
            {
                liveRoots.Add(t);
            }
        }

        liveRoots.Sort((x, y) => Min(_chartFaces[x]).CompareTo(Min(_chartFaces[y])));

        var charts = new List<Chart>(liveRoots.Count);
        var finalFaceChart = new int[_mesh.TriangleCount];
        for (int newId = 0; newId < liveRoots.Count; newId++)
        {
            int root = liveRoots[newId];
            List<int> faces = _chartFaces[root];
            faces.Sort();
            foreach (int face in faces)
            {
                finalFaceChart[face] = newId;
            }

            charts.Add(new Chart(newId, faces, _chartArea[root], _chartNormalAccum[root].Normalized()));
        }

        return new SegmentationResult(_mesh, finalFaceChart, charts);
    }

    private static int Min(List<int> values)
    {
        int min = values[0];
        foreach (int v in values)
        {
            if (v < min)
            {
                min = v;
            }
        }

        return min;
    }
}
