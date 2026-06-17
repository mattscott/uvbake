using UVBake.Core;
using UVBake.IO;
using Xunit;

namespace UVBake.Tests;

/// <summary>
/// P0 smoke tests: the solution builds, the public scaffold surface is reachable, and the documented
/// defaults hold. Real unwrap tests (golden UV-layout / metric diffs vs the reference oracle) arrive
/// in Phase 1.
/// </summary>
public class ScaffoldTests
{
    [Fact]
    public void DefaultOptions_MatchDocumentedDefaults()
    {
        var opt = new UVBakeOptions();

        Assert.Equal(2048, opt.Resolution);
        Assert.Equal(8, opt.GutterPixels);
        Assert.Equal(45.0, opt.HardEdgeAngleDegrees);
        Assert.Equal(SeamSource.Both, opt.Seams);
        Assert.Equal(MeshFormat.Obj, opt.Format);
        Assert.Equal(-1, opt.Threads);
    }

    [Fact]
    public void Io_StartsWithObj()
    {
        Assert.Equal(".obj", MeshIo.PrimaryMeshExtension);
    }
}
