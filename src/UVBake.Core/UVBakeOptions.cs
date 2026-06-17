namespace UVBake.Core;

/// <summary>Mesh + UV output container format.</summary>
public enum MeshFormat
{
    /// <summary>Wavefront OBJ — UVs as <c>vt</c> with per-wedge <c>f v/vt/vn</c> indices (seams = split vt).</summary>
    Obj,

    /// <summary>glTF 2.0 — TEXCOORD_0 is per-vertex, so seams require vertex duplication.</summary>
    Gltf,
}

/// <summary>How seams (UV cuts) are seeded before chart growth.</summary>
public enum SeamSource
{
    /// <summary>Detect hard edges from the dihedral-angle threshold.</summary>
    DihedralAngle,

    /// <summary>Use crease / sharp-edge tags provided on the input mesh.</summary>
    ProvidedTags,

    /// <summary>Union of provided tags and dihedral-angle detection.</summary>
    Both,
}

/// <summary>
/// Options controlling UV atlas generation. Defaults target a bake-ready atlas: bounded distortion,
/// unique non-overlapping islands grown as large as the distortion budget allows, consistent texel
/// density, and seams on hard edges. See <c>Agents/Documents</c> for the rationale behind each setting.
/// </summary>
public sealed class UVBakeOptions
{
    /// <summary>Atlas resolution in texels (square). The packing / texel-density reference; multiple of 4 recommended.</summary>
    public int Resolution { get; init; } = 2048;

    /// <summary>Target texel density (texels per unit surface length); null = auto-scale charts to fill the atlas.</summary>
    public double? TexelsPerUnit { get; init; }

    /// <summary>Inter-island gutter in texels. Should be at least twice a downstream baker's dilation padding.</summary>
    public int GutterPixels { get; init; } = 8;

    /// <summary>Distortion budget for chart growth: a normalized stretch threshold (lower = more, smaller charts).</summary>
    public double MaxStretch { get; init; } = 0.25;

    /// <summary>Allow 90-degree chart rotation during packing for tighter layouts.</summary>
    public bool AllowRotation { get; init; } = true;

    /// <summary>Round the atlas up to a power-of-two square.</summary>
    public bool PowerOfTwo { get; init; } = true;

    /// <summary>Dihedral angle (degrees) above which an edge is a hard edge that forces a seam.</summary>
    public double HardEdgeAngleDegrees { get; init; } = 45.0;

    /// <summary>How seams are seeded before chart growth.</summary>
    public SeamSource Seams { get; init; } = SeamSource.Both;

    /// <summary>Output mesh + UV container format.</summary>
    public MeshFormat Format { get; init; } = MeshFormat.Obj;

    /// <summary>Packing strategy: fast bounding-box skyline, or tighter silhouette ("Tetris") packing.</summary>
    public PackerKind Packer { get; init; } = PackerKind.BoundingBox;

    /// <summary>Worker thread count; -1 uses all available cores.</summary>
    public int Threads { get; init; } = -1;
}

/// <summary>How charts are packed into the atlas.</summary>
public enum PackerKind
{
    /// <summary>Skyline bottom-left packing of chart bounding boxes — fast, the default.</summary>
    BoundingBox,

    /// <summary>Silhouette-aware skyline packing of the rasterized chart shape — tighter fills.</summary>
    Silhouette,
}
