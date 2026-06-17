using System.Diagnostics;
using UVBake.Core;
using UVBake.Core.Meshes;
using UVBake.Tests.Corpus;
using Xunit;

namespace UVBake.Tests;

/// <summary>P6 performance smoke gate: the whole corpus bakes well under a generous time budget.</summary>
public class PerfTests
{
    [Fact]
    public void Corpus_BakesWithinTimeBudget()
    {
        // Warm up the JIT so the measured pass reflects steady-state work, not first-call compilation.
        _ = UVBaker.Generate(Primitives.Cube(), new UVBakeOptions());

        var stopwatch = Stopwatch.StartNew();
        foreach ((string _, Mesh source) in Primitives.All())
        {
            UvMesh result = UVBaker.Generate(source, new UVBakeOptions());
            Assert.True(result.Validation.IsValid);
        }

        stopwatch.Stop();

        // Generous regression gate: catches hangs / blow-ups without being flaky on slow CI.
        Assert.True(stopwatch.Elapsed.TotalSeconds < 30, $"corpus bake took {stopwatch.Elapsed.TotalSeconds:F1}s");
    }
}
