namespace UVBake.Core.Meshes;

/// <summary>
/// One corner (wedge) of a polygon face: independent indices into a mesh's position, texcoord, and
/// normal lists — the Wavefront OBJ <c>f v/vt/vn</c> model. Per-wedge UVs fall out of this directly: a
/// seam is two corners that share a <see cref="Position"/> but carry different <see cref="TexCoord"/>.
/// </summary>
public readonly record struct Corner(int Position, int TexCoord, int Normal)
{
    /// <summary>Sentinel for an absent texcoord/normal index.</summary>
    public const int None = -1;

    /// <summary>True when this corner references a texcoord.</summary>
    public bool HasTexCoord => TexCoord >= 0;

    /// <summary>True when this corner references a normal.</summary>
    public bool HasNormal => Normal >= 0;
}
