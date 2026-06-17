using UVBake.Core.Meshes;
using UVBake.Core.Validation;

namespace UVBake.Core;

/// <summary>
/// The output of <see cref="UVBaker.Generate"/>: the input mesh, triangulated, carrying a unique
/// bake-ready UV atlas as <b>per-wedge UVs</b> (a seam is one position referenced by corners with
/// different texcoords). <see cref="Mesh"/> is ready to write as OBJ; <see cref="Validation"/> records
/// the hard-gate result (overlap = 0, no flips, full coverage).
/// </summary>
public sealed class UvMesh
{
    public UvMesh(Mesh mesh, int resolution, AtlasValidation validation)
    {
        Mesh = mesh ?? throw new ArgumentNullException(nameof(mesh));
        Resolution = resolution;
        Validation = validation ?? throw new ArgumentNullException(nameof(validation));
    }

    /// <summary>The triangulated mesh with per-wedge atlas UVs in the [0,1] tile.</summary>
    public Mesh Mesh { get; }

    /// <summary>Atlas resolution (texels, square).</summary>
    public int Resolution { get; }

    /// <summary>Hard-gate validation report for the produced atlas.</summary>
    public AtlasValidation Validation { get; }
}
