using UVBake.Core.Geometry;
using UVBake.Core.Meshes;
using Xunit;

namespace UVBake.Tests;

/// <summary>Shared test helpers: repo-relative paths and mesh equality assertions.</summary>
internal static class TestSupport
{
    /// <summary>Walks up from the test assembly to the directory containing <c>UVBake.sln</c>.</summary>
    public static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "UVBake.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return null;
    }

    /// <summary>Asserts that two meshes are identical in positions, texcoords, normals, and face corners.</summary>
    public static void AssertMeshEqual(Mesh expected, Mesh actual)
    {
        Assert.Equal(expected.Positions.Count, actual.Positions.Count);
        for (int i = 0; i < expected.Positions.Count; i++)
        {
            Assert.Equal(expected.Positions[i], actual.Positions[i]);
        }

        Assert.Equal(expected.TexCoords.Count, actual.TexCoords.Count);
        for (int i = 0; i < expected.TexCoords.Count; i++)
        {
            Assert.Equal(expected.TexCoords[i], actual.TexCoords[i]);
        }

        Assert.Equal(expected.Normals.Count, actual.Normals.Count);
        for (int i = 0; i < expected.Normals.Count; i++)
        {
            Assert.Equal(expected.Normals[i], actual.Normals[i]);
        }

        Assert.Equal(expected.Faces.Count, actual.Faces.Count);
        for (int f = 0; f < expected.Faces.Count; f++)
        {
            Face ef = expected.Faces[f];
            Face af = actual.Faces[f];
            Assert.Equal(ef.Count, af.Count);
            for (int c = 0; c < ef.Count; c++)
            {
                Assert.Equal(ef[c], af[c]);
            }
        }
    }

    /// <summary>A sorted (i,j,k) position-index triple, for comparing triangulations as sets.</summary>
    public static (int, int, int) SortedTriple(Face triangle)
    {
        Span<int> v = [triangle[0].Position, triangle[1].Position, triangle[2].Position];
        v.Sort();
        return (v[0], v[1], v[2]);
    }
}
