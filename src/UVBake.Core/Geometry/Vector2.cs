namespace UVBake.Core.Geometry;

/// <summary>
/// A double-precision 2D vector / UV coordinate. Double precision matches <see cref="Vector3"/> so the
/// parameterization and packing math stays consistent and deterministic.
/// </summary>
public readonly record struct Vector2(double X, double Y)
{
    /// <summary>The zero vector.</summary>
    public static Vector2 Zero => new(0, 0);

    public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.X + b.X, a.Y + b.Y);

    public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.X - b.X, a.Y - b.Y);

    public static Vector2 operator -(Vector2 a) => new(-a.X, -a.Y);

    public static Vector2 operator *(Vector2 a, double s) => new(a.X * s, a.Y * s);

    public static Vector2 operator *(double s, Vector2 a) => a * s;

    public static Vector2 operator /(Vector2 a, double s) => new(a.X / s, a.Y / s);

    /// <summary>Dot product.</summary>
    public double Dot(Vector2 b) => (X * b.X) + (Y * b.Y);

    /// <summary>2D cross product (z-component of the 3D cross); signed area of the parallelogram.</summary>
    public double Cross(Vector2 b) => (X * b.Y) - (Y * b.X);

    /// <summary>Squared Euclidean length (no square root).</summary>
    public double LengthSquared => (X * X) + (Y * Y);

    /// <summary>Euclidean length.</summary>
    public double Length => System.Math.Sqrt(LengthSquared);
}
