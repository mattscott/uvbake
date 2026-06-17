using UVBake.Core.Meshes;
using UVBake.IO;
using UVBake.Tests.Corpus;
using Xunit;

namespace UVBake.Tests;

/// <summary>
/// Writes the procedural corpus OBJ files into <c>tests/corpus/</c>. Gated on the environment variable
/// <c>UVBAKE_WRITE_CORPUS=1</c> so a normal/CI test run never writes into the source tree — the files
/// are deterministic and committed, regenerated on demand with:
/// <code>UVBAKE_WRITE_CORPUS=1 dotnet test --filter CorpusGeneration</code>
/// </summary>
public class CorpusGenerationTests
{
    [Fact]
    public void WritesProceduralCorpusToRepo()
    {
        if (Environment.GetEnvironmentVariable("UVBAKE_WRITE_CORPUS") != "1")
        {
            return; // opt-in only
        }

        string? root = TestSupport.FindRepoRoot();
        Assert.NotNull(root);

        string dir = Path.Combine(root, "tests", "corpus");
        Directory.CreateDirectory(dir);

        foreach ((string name, Mesh mesh) in Primitives.All())
        {
            string path = Path.Combine(dir, name + ".obj");
            ObjWriter.Write(mesh, path);

            // The committed file must re-read identically.
            TestSupport.AssertMeshEqual(mesh, ObjReader.Read(path));
        }
    }
}
