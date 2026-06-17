using UVBake.Core.Geometry;
using UVBake.Core.Meshes;

namespace UVBake.Core.Parameterization;

/// <summary>
/// The chart-local topology a flattener needs: a compact 0..N-1 vertex re-indexing of one chart's
/// triangles, the chart's 1-ring adjacency graph, and its single boundary loop in order. Charts are
/// disks (P2 guarantees χ = 1), so there is exactly one boundary loop, walked here in surface-winding
/// order (chart on the left of each boundary half-edge).
/// </summary>
public sealed class ChartTopology
{
    private ChartTopology(
        int[] localToGlobal,
        Vector3[] position,
        int[] triangles,
        bool[] isBoundary,
        int[] boundaryLoop,
        int[][] neighbors)
    {
        LocalToGlobal = localToGlobal;
        Position = position;
        Triangles = triangles;
        IsBoundary = isBoundary;
        BoundaryLoop = boundaryLoop;
        Neighbors = neighbors;
    }

    /// <summary>Local vertex index → global position index.</summary>
    public int[] LocalToGlobal { get; }

    /// <summary>3D position of each local vertex.</summary>
    public Vector3[] Position { get; }

    /// <summary>Triangles as local-vertex index triples (length <c>3 × FaceCount</c>).</summary>
    public int[] Triangles { get; }

    /// <summary>Whether each local vertex lies on the chart boundary.</summary>
    public bool[] IsBoundary { get; }

    /// <summary>The boundary loop as ordered local vertex indices.</summary>
    public int[] BoundaryLoop { get; }

    /// <summary>1-ring neighbours (local indices) of each local vertex, sorted.</summary>
    public int[][] Neighbors { get; }

    /// <summary>Number of local vertices.</summary>
    public int VertexCount => LocalToGlobal.Length;

    /// <summary>Number of triangles.</summary>
    public int FaceCount => Triangles.Length / 3;

    public static ChartTopology Build(HalfEdgeMesh mesh, IReadOnlyList<int> chartFaces)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(chartFaces);

        // Re-index the chart's vertices to a compact 0..N-1 range (deterministic: faces are sorted).
        var globalToLocal = new Dictionary<int, int>();
        var localToGlobal = new List<int>();
        var triangles = new int[chartFaces.Count * 3];

        for (int f = 0; f < chartFaces.Count; f++)
        {
            int face = chartFaces[f];
            for (int i = 0; i < 3; i++)
            {
                int global = mesh.Origin((3 * face) + i);
                if (!globalToLocal.TryGetValue(global, out int local))
                {
                    local = localToGlobal.Count;
                    globalToLocal[global] = local;
                    localToGlobal.Add(global);
                }

                triangles[(3 * f) + i] = local;
            }
        }

        int vertexCount = localToGlobal.Count;
        var position = new Vector3[vertexCount];
        for (int v = 0; v < vertexCount; v++)
        {
            position[v] = mesh.Positions[localToGlobal[v]];
        }

        var (isBoundary, boundaryLoop) = ExtractBoundary(triangles, vertexCount);
        int[][] neighbors = BuildAdjacency(triangles, vertexCount);

        return new ChartTopology([.. localToGlobal], position, triangles, isBoundary, boundaryLoop, neighbors);
    }

    private static (bool[] IsBoundary, int[] Loop) ExtractBoundary(int[] triangles, int vertexCount)
    {
        // An undirected edge used by exactly one chart triangle is a boundary edge; the directed edge
        // (in winding order) whose undirected key is a boundary edge points along the loop.
        var edgeCount = new Dictionary<EdgeKey, int>();
        for (int t = 0; t < triangles.Length; t += 3)
        {
            AddEdge(edgeCount, triangles[t], triangles[t + 1]);
            AddEdge(edgeCount, triangles[t + 1], triangles[t + 2]);
            AddEdge(edgeCount, triangles[t + 2], triangles[t]);
        }

        var next = new int[vertexCount];
        Array.Fill(next, -1);
        var isBoundary = new bool[vertexCount];

        for (int t = 0; t < triangles.Length; t += 3)
        {
            ConsiderDirected(triangles[t], triangles[t + 1], edgeCount, next, isBoundary);
            ConsiderDirected(triangles[t + 1], triangles[t + 2], edgeCount, next, isBoundary);
            ConsiderDirected(triangles[t + 2], triangles[t], edgeCount, next, isBoundary);
        }

        // Walk the single loop from the lowest-index boundary vertex (deterministic).
        int start = -1;
        for (int v = 0; v < vertexCount; v++)
        {
            if (isBoundary[v])
            {
                start = v;
                break;
            }
        }

        var loop = new List<int>();
        if (start >= 0)
        {
            int cur = start;
            do
            {
                loop.Add(cur);
                cur = next[cur];
            }
            while (cur != start && cur >= 0 && loop.Count <= vertexCount);
        }

        return (isBoundary, [.. loop]);
    }

    private static void ConsiderDirected(int a, int b, Dictionary<EdgeKey, int> edgeCount, int[] next, bool[] isBoundary)
    {
        if (edgeCount[new EdgeKey(a, b)] == 1)
        {
            next[a] = b;
            isBoundary[a] = true;
            isBoundary[b] = true;
        }
    }

    private static void AddEdge(Dictionary<EdgeKey, int> edgeCount, int a, int b)
    {
        var key = new EdgeKey(a, b);
        edgeCount[key] = edgeCount.TryGetValue(key, out int c) ? c + 1 : 1;
    }

    private static int[][] BuildAdjacency(int[] triangles, int vertexCount)
    {
        var sets = new HashSet<int>[vertexCount];
        for (int v = 0; v < vertexCount; v++)
        {
            sets[v] = [];
        }

        for (int t = 0; t < triangles.Length; t += 3)
        {
            int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
            Connect(sets, a, b);
            Connect(sets, b, c);
            Connect(sets, c, a);
        }

        var neighbors = new int[vertexCount][];
        for (int v = 0; v < vertexCount; v++)
        {
            neighbors[v] = [.. sets[v]];
            Array.Sort(neighbors[v]);
        }

        return neighbors;
    }

    private static void Connect(HashSet<int>[] sets, int a, int b)
    {
        sets[a].Add(b);
        sets[b].Add(a);
    }
}
