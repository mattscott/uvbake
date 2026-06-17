using UVBake.Core;
using UVBake.Tests.Corpus;
using Xunit;

namespace UVBake.Tests;

/// <summary>P7 public-API surface: progress reporting and cancellation on UVBaker.Generate.</summary>
public class ApiTests
{
    [Fact]
    public void Generate_ReportsPerPhaseProgress()
    {
        var progress = new CollectingProgress();
        UVBaker.Generate(Primitives.Cube(), new UVBakeOptions(), progress);

        Assert.Contains("Packing atlas", progress.Reports);
        Assert.Equal("Done", progress.Reports[^1]);
    }

    [Fact]
    public void Generate_HonorsCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(
            () => UVBaker.Generate(Primitives.Sphere(), new UVBakeOptions(), progress: null, cts.Token));
    }

    // Synchronous IProgress so reports are captured deterministically on the calling thread
    // (Progress<T> would marshal to the threadpool and race the assertions).
    private sealed class CollectingProgress : IProgress<string>
    {
        public List<string> Reports { get; } = [];

        public void Report(string value) => Reports.Add(value);
    }
}
