using System.Numerics;

namespace UVBake.IO;

/// <summary>
/// An FBX geometry layer (normals or UVs) with its mapping/reference modes resolved. Supports the common
/// combinations: mapping <c>ByPolygonVertex</c> / <c>ByControlPoint</c> (a.k.a. ByVertice) / <c>AllSame</c>,
/// and reference <c>Direct</c> / <c>IndexToDirect</c>.
/// </summary>
internal sealed class FbxLayer
{
    private readonly double[] _data;
    private readonly int[]? _index;
    private readonly bool _byControlPoint;
    private readonly bool _allSame;
    private readonly int _components;

    private FbxLayer(double[] data, int[]? index, bool byControlPoint, bool allSame, int components)
    {
        _data = data;
        _index = index;
        _byControlPoint = byControlPoint;
        _allSame = allSame;
        _components = components;
    }

    public static FbxLayer? From(FbxNode geo, string layerName, string dataName, string indexName, int components)
    {
        FbxNode? layer = geo.Child(layerName);
        double[]? data = layer?.Child(dataName)?.AsDoubleArray(0);
        if (layer is null || data is null)
        {
            return null;
        }

        string mapping = layer.Child("MappingInformationType")?.AsString(0) ?? "ByPolygonVertex";
        string reference = layer.Child("ReferenceInformationType")?.AsString(0) ?? "Direct";
        int[]? index = reference.Contains("IndexToDirect", StringComparison.Ordinal)
            ? layer.Child(indexName)?.AsIntArray(0)
            : null;
        bool byControlPoint = mapping.Contains("ControlPoint", StringComparison.Ordinal)
            || mapping.Contains("Vertice", StringComparison.Ordinal);
        bool allSame = mapping.Contains("AllSame", StringComparison.Ordinal);
        return new FbxLayer(data, index, byControlPoint, allSame, components);
    }

    /// <summary>The resolved index into the data array (after IndexToDirect) — used as a dedup key.</summary>
    public int RawIndex(int polygonVertex, int controlPoint) => Resolve(polygonVertex, controlPoint);

    public Vector3 Get3(int polygonVertex, int controlPoint)
    {
        int r = Resolve(polygonVertex, controlPoint);
        int d = r * _components;
        if (r < 0 || d + 2 >= _data.Length)
        {
            return new Vector3(0f, 0f, 1f); // unmapped (FBX index -1) or out of range
        }

        return new Vector3((float)_data[d], (float)_data[d + 1], (float)_data[d + 2]);
    }

    public Vector2 Get2(int polygonVertex, int controlPoint)
    {
        int r = Resolve(polygonVertex, controlPoint);
        int d = r * _components;
        if (r < 0 || d + 1 >= _data.Length)
        {
            return Vector2.Zero; // unmapped (FBX index -1) or out of range
        }

        return new Vector2((float)_data[d], (float)_data[d + 1]);
    }

    private int Resolve(int polygonVertex, int controlPoint)
    {
        int reference = _allSame ? 0 : _byControlPoint ? controlPoint : polygonVertex;
        if (_index is null)
        {
            return reference;
        }

        return (uint)reference < (uint)_index.Length ? _index[reference] : -1;
    }
}
