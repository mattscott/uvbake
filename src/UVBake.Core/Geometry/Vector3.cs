namespace UVBake.Core.Geometry;

/// <summary>
/// A double-precision 3D vector / point. Double precision is deliberate: signed areas, dihedral angles,
/// and the parameterization solvers need it for robustness and deterministic results.
/// </summary>
public readonly record struct Vector3(double X, double Y, double Z)
{
    /// <summary>The zero vector.</summary>
    public static Vector3 Zero => new(0, 0, 0);

    public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    public static Vector3 operator -(Vector3 a) => new(-a.X, -a.Y, -a.Z);

    public static Vector3 operator *(Vector3 a, double s) => new(a.X * s, a.Y * s, a.Z * s);

    public static Vector3 operator *(double s, Vector3 a) => a * s;

    public static Vector3 operator /(Vector3 a, double s) => new(a.X / s, a.Y / s, a.Z / s);

    /// <summary>Dot product.</summary>
    public double Dot(Vector3 b) => (X * b.X) + (Y * b.Y) + (Z * b.Z);

    /// <summary>Cross product (right-handed).</summary>
    public Vector3 Cross(Vector3 b) => new(
        (Y * b.Z) - (Z * b.Y),
        (Z * b.X) - (X * b.Z),
        (X * b.Y) - (Y * b.X));

    /// <summary>Squared Euclidean length (no square root).</summary>
    public double LengthSquared => (X * X) + (Y * Y) + (Z * Z);

    /// <summary>Euclidean length.</summary>
    public double Length => System.Math.Sqrt(LengthSquared);

    /// <summary>
    /// Returns this vector scaled to unit length. Returns <see cref="Zero"/> for a (near-)zero vector
    /// so degenerate geometry yields a defined-but-zero normal rather than NaN.
    /// </summary>
    public Vector3 Normalized()
    {
        double len = Length;
        return len > 0 ? this / len : Zero;
    }
}
