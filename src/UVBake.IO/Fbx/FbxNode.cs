namespace UVBake.IO;

/// <summary>
/// A node in a parsed FBX document tree: a name, a list of typed properties (boxed primitives, typed
/// arrays, or strings), and child nodes. The parser is clean-room — it reads the documented binary FBX
/// record format, not the Autodesk SDK.
/// </summary>
internal sealed class FbxNode
{
    public FbxNode(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public List<object> Properties { get; } = new();

    public List<FbxNode> Children { get; } = new();

    /// <summary>The first child with the given name, or null.</summary>
    public FbxNode? Child(string name)
    {
        foreach (FbxNode c in Children)
        {
            if (c.Name == name)
            {
                return c;
            }
        }

        return null;
    }

    /// <summary>All children with the given name.</summary>
    public IEnumerable<FbxNode> ChildrenNamed(string name)
    {
        foreach (FbxNode c in Children)
        {
            if (c.Name == name)
            {
                yield return c;
            }
        }
    }

    /// <summary>A string property (FBX stores names/types as byte strings), or null.</summary>
    public string? AsString(int index) => index < Properties.Count ? Properties[index] as string : null;

    /// <summary>A numeric property widened to double (covers FBX D/F/I/L/Y/C), or a fallback.</summary>
    public double AsDouble(int index, double fallback = 0)
    {
        if (index >= Properties.Count)
        {
            return fallback;
        }

        return Properties[index] switch
        {
            double d => d,
            long l => l,
            bool b => b ? 1 : 0,
            _ => fallback,
        };
    }

    /// <summary>A long property (FBX I/L/Y), or a fallback.</summary>
    public long AsLong(int index, long fallback = 0) =>
        index < Properties.Count && Properties[index] is long l ? l : fallback;

    /// <summary>A double-array property, widening int/long/float arrays as needed, or null.</summary>
    public double[]? AsDoubleArray(int index)
    {
        if (index >= Properties.Count)
        {
            return null;
        }

        return Properties[index] switch
        {
            double[] d => d,
            float[] f => Array.ConvertAll(f, v => (double)v),
            _ => null,
        };
    }

    /// <summary>An int-array property, narrowing a long array if needed, or null.</summary>
    public int[]? AsIntArray(int index)
    {
        if (index >= Properties.Count)
        {
            return null;
        }

        return Properties[index] switch
        {
            int[] i => i,
            long[] l => Array.ConvertAll(l, v => (int)v),
            _ => null,
        };
    }
}
