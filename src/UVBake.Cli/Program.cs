using UVBake.Core;

namespace UVBake.Cli;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help")
        {
            PrintUsage();
            return 0;
        }

        Console.Error.WriteLine(
            "uvbake: the unwrapper is not implemented yet (P0 scaffold). See Agents/Plans for the roadmap.");
        return 1;
    }

    private static void PrintUsage()
    {
        var d = new UVBakeOptions();
        Console.WriteLine("uvbake - generate a bake-ready UV atlas for a mesh (unique, large low-distortion islands)");
        Console.WriteLine();
        Console.WriteLine("usage: uvbake <in-mesh> -o <out-mesh> [options]");
        Console.WriteLine();
        Console.WriteLine("Produces a unique, non-overlapping UV layout with seams on hard edges, for a downstream baker.");
        Console.WriteLine();
        Console.WriteLine("defaults:");
        Console.WriteLine($"  --res {d.Resolution}   --gutter {d.GutterPixels}px   --max-stretch {d.MaxStretch}");
        Console.WriteLine($"  --hard-edge {d.HardEdgeAngleDegrees}deg   --seams {d.Seams}   --format {d.Format}");
    }
}
