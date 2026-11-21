using System.Numerics;

namespace UVBake.IO;

/// <summary>
/// Resolves FBX scene-graph transforms: it maps each Geometry to its owning Model (via the Connections
/// OO links) and composes a mesh-to-world matrix from the Model's Lcl Translation/Rotation/Scaling +
/// PreRotation + geometric transform, walking up the parent chain. Pivots/offsets are not applied (these
/// game exports don't use them) — alignment of the high and low bounds is the correctness check.
/// </summary>
internal sealed class FbxScene
{
    private readonly Dictionary<long, FbxNode> _models = new();
    private readonly Dictionary<long, long> _parentOf = new();

    public FbxScene(FbxNode root)
    {
        foreach (FbxNode objects in root.ChildrenNamed("Objects"))
        {
            foreach (FbxNode model in objects.ChildrenNamed("Model"))
            {
                _models[model.AsLong(0)] = model;
            }
        }

        foreach (FbxNode connections in root.ChildrenNamed("Connections"))
        {
            foreach (FbxNode c in connections.ChildrenNamed("C"))
            {
                if (c.AsString(0) == "OO")
                {
                    long child = c.AsLong(1);
                    if (!_parentOf.ContainsKey(child))
                    {
                        _parentOf[child] = c.AsLong(2);
                    }
                }
            }
        }
    }

    /// <summary>The mesh-to-world matrix for the geometry with the given id (identity if untransformed).</summary>
    public Matrix4x4 MeshToWorld(long geometryId)
    {
        if (!_parentOf.TryGetValue(geometryId, out long modelId) || !_models.TryGetValue(modelId, out FbxNode? model))
        {
            return Matrix4x4.Identity;
        }

        Matrix4x4 world = GeometricMatrix(model);
        long id = modelId;
        while (_models.TryGetValue(id, out FbxNode? node))
        {
            world *= LocalMatrix(node);
            if (!_parentOf.TryGetValue(id, out id))
            {
                break;
            }
        }

        return world;
    }

    private static Matrix4x4 LocalMatrix(FbxNode model)
    {
        Vector3 t = Vec3(model, "Lcl Translation", Vector3.Zero);
        Vector3 r = Vec3(model, "Lcl Rotation", Vector3.Zero);
        Vector3 s = Vec3(model, "Lcl Scaling", Vector3.One);
        Vector3 pre = Vec3(model, "PreRotation", Vector3.Zero);
        int order = (int)PValue(model, "RotationOrder", 0);

        return Matrix4x4.CreateScale(s)
            * Euler(r, order)
            * Euler(pre, 0)
            * Matrix4x4.CreateTranslation(t);
    }

    private static Matrix4x4 GeometricMatrix(FbxNode model)
    {
        Vector3 t = Vec3(model, "GeometricTranslation", Vector3.Zero);
        Vector3 r = Vec3(model, "GeometricRotation", Vector3.Zero);
        Vector3 s = Vec3(model, "GeometricScaling", Vector3.One);
        if (t == Vector3.Zero && r == Vector3.Zero && s == Vector3.One)
        {
            return Matrix4x4.Identity;
        }

        return Matrix4x4.CreateScale(s) * Euler(r, 0) * Matrix4x4.CreateTranslation(t);
    }

    private static Matrix4x4 Euler(Vector3 degrees, int order)
    {
        if (degrees == Vector3.Zero)
        {
            return Matrix4x4.Identity;
        }

        Matrix4x4 x = Matrix4x4.CreateRotationX(Rad(degrees.X));
        Matrix4x4 y = Matrix4x4.CreateRotationY(Rad(degrees.Y));
        Matrix4x4 z = Matrix4x4.CreateRotationZ(Rad(degrees.Z));
        return order switch
        {
            1 => x * z * y,
            2 => y * z * x,
            3 => y * x * z,
            4 => z * x * y,
            5 => z * y * x,
            _ => x * y * z, // 0 = XYZ
        };
    }

    private static float Rad(double degrees) => (float)(degrees * Math.PI / 180.0);

    private static Vector3 Vec3(FbxNode model, string name, Vector3 fallback)
    {
        FbxNode? p = FindP(model, name);
        return p is null
            ? fallback
            : new Vector3((float)p.AsDouble(4), (float)p.AsDouble(5), (float)p.AsDouble(6));
    }

    private static double PValue(FbxNode model, string name, double fallback)
    {
        FbxNode? p = FindP(model, name);
        return p?.AsDouble(4, fallback) ?? fallback;
    }

    private static FbxNode? FindP(FbxNode model, string name)
    {
        FbxNode? props = model.Child("Properties70");
        if (props is null)
        {
            return null;
        }

        foreach (FbxNode p in props.ChildrenNamed("P"))
        {
            if (p.AsString(0) == name)
            {
                return p;
            }
        }

        return null;
    }
}
