using UVBake.Core.Geometry;

namespace UVBake.Core.Meshes;

/// <summary>
/// An OBJ-faithful polygon mesh: independent position / texcoord / normal lists plus polygonal
/// <see cref="Face"/>s whose corners index into them (per-wedge attributes, so seams round-trip). This
/// is the canonical I/O container — what the OBJ reader produces and the OBJ writer consumes. The
/// working connectivity structure (built by triangulating this) is <see cref="HalfEdgeMesh"/>.
/// </summary>
public sealed class Mesh
{
    private static readonly IReadOnlySet<EdgeKey> NoCreases = new HashSet<EdgeKey>();

    /// <summary>Vertex positions (the OBJ <c>v</c> list).</summary>
    public IReadOnlyList<Vector3> Positions { get; }

    /// <summary>Texture coordinates (the OBJ <c>vt</c> list); empty when the mesh carries no UVs.</summary>
    public IReadOnlyList<Vector2> TexCoords { get; }

    /// <summary>Vertex normals (the OBJ <c>vn</c> list); empty when the mesh carries no normals.</summary>
    public IReadOnlyList<Vector3> Normals { get; }

    /// <summary>The polygon faces, each indexing the lists above per corner.</summary>
    public IReadOnlyList<Face> Faces { get; }

    /// <summary>
    /// Edges flagged as creases / sharp by the source (e.g. OBJ smoothing-group boundaries). Keyed by
    /// position index, so they survive triangulation. Consumed by hard-edge detection under
    /// <see cref="SeamSource.ProvidedTags"/> / <see cref="SeamSource.Both"/>.
    /// </summary>
    public IReadOnlySet<EdgeKey> Creases { get; }

    public Mesh(
        IReadOnlyList<Vector3> positions,
        IReadOnlyList<Vector2> texCoords,
        IReadOnlyList<Vector3> normals,
        IReadOnlyList<Face> faces,
        IReadOnlySet<EdgeKey>? creases = null)
    {
        Positions = positions ?? throw new ArgumentNullException(nameof(positions));
        TexCoords = texCoords ?? throw new ArgumentNullException(nameof(texCoords));
        Normals = normals ?? throw new ArgumentNullException(nameof(normals));
        Faces = faces ?? throw new ArgumentNullException(nameof(faces));
        Creases = creases ?? NoCreases;
    }

    /// <summary>True when the mesh carries texture coordinates.</summary>
    public bool HasTexCoords => TexCoords.Count > 0;

    /// <summary>True when the mesh carries vertex normals.</summary>
    public bool HasNormals => Normals.Count > 0;
}
