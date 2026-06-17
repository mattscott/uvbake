using System.Globalization;
using UVBake.Core;
using UVBake.Core.Meshes;
using UVBake.IO;

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

        try
        {
            return Run(args);
        }
        catch (Exception ex) when (ex is IOException or FormatException or NotSupportedException or ArgumentException or InvalidOperationException)
        {
            Console.Error.WriteLine($"uvbake: {ex.Message}");
            return 1;
        }
    }

    private static int Run(string[] args)
    {
        var d = new UVBakeOptions();
        string? input = null;
        string? output = null;
        int resolution = d.Resolution;
        int gutter = d.GutterPixels;
        double maxStretch = d.MaxStretch;
        double hardEdge = d.HardEdgeAngleDegrees;
        SeamSource seams = d.Seams;
        bool allowRotation = d.AllowRotation;
        bool powerOfTwo = d.PowerOfTwo;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            switch (arg)
            {
                case "-o" or "--output":
                    output = RequireValue(args, ref i, arg);
                    break;
                case "--res" or "--resolution":
                    resolution = int.Parse(RequireValue(args, ref i, arg), CultureInfo.InvariantCulture);
                    break;
                case "--gutter":
                    gutter = int.Parse(RequireValue(args, ref i, arg), CultureInfo.InvariantCulture);
                    break;
                case "--max-stretch":
                    maxStretch = double.Parse(RequireValue(args, ref i, arg), CultureInfo.InvariantCulture);
                    break;
                case "--hard-edge":
                    hardEdge = double.Parse(RequireValue(args, ref i, arg), CultureInfo.InvariantCulture);
                    break;
                case "--seams":
                    seams = Enum.Parse<SeamSource>(RequireValue(args, ref i, arg), ignoreCase: true);
                    break;
                case "--no-rotate":
                    allowRotation = false;
                    break;
                case "--no-pot":
                    powerOfTwo = false;
                    break;
                default:
                    if (arg.StartsWith('-'))
                    {
                        throw new ArgumentException($"unknown option '{arg}'");
                    }

                    input = arg;
                    break;
            }
        }

        if (input is null)
        {
            throw new ArgumentException("no input mesh given (see --help)");
        }

        var options = new UVBakeOptions
        {
            Resolution = resolution,
            GutterPixels = gutter,
            MaxStretch = maxStretch,
            HardEdgeAngleDegrees = hardEdge,
            Seams = seams,
            AllowRotation = allowRotation,
            PowerOfTwo = powerOfTwo,
        };

        Mesh mesh = MeshIo.Read(input);
        UvMesh result = UVBaker.Generate(mesh, options);

        Console.WriteLine($"charts packed into {result.Resolution}x{result.Resolution}; " +
            $"packing {result.Validation.PackingEfficiency * 100:F1}%; " +
            $"overlap={(result.Validation.OverlapFree ? 0 : result.Validation.OverlapCount)}, " +
            $"flips={result.Validation.FlipCount}, coverage={(result.Validation.FullCoverage ? "full" : "partial")}");

        if (!result.Validation.IsValid)
        {
            Console.Error.WriteLine("uvbake: atlas failed a hard gate (overlap/flip/coverage); not writing output.");
            return 2;
        }

        if (output is not null)
        {
            MeshIo.Write(result.Mesh, output);
            Console.WriteLine($"wrote {output}");
        }

        return 0;
    }

    private static string RequireValue(string[] args, ref int i, string option)
    {
        if (i + 1 >= args.Length)
        {
            throw new ArgumentException($"option '{option}' needs a value");
        }

        return args[++i];
    }

    private static void PrintUsage()
    {
        var d = new UVBakeOptions();
        Console.WriteLine("uvbake - generate a bake-ready UV atlas for a mesh (unique, large low-distortion islands)");
        Console.WriteLine();
        Console.WriteLine("usage: uvbake <in.obj> [-o <out.obj>] [options]");
        Console.WriteLine();
        Console.WriteLine("Produces a unique, non-overlapping UV layout with seams on hard edges, for a downstream baker.");
        Console.WriteLine();
        Console.WriteLine("options:");
        Console.WriteLine($"  -o, --output <path>   write the atlas OBJ (per-wedge UVs); omit for a dry run");
        Console.WriteLine($"  --res <n>             atlas resolution (default {d.Resolution})");
        Console.WriteLine($"  --gutter <n>          inter-island gutter in texels (default {d.GutterPixels})");
        Console.WriteLine($"  --max-stretch <x>     distortion budget for chart growth (default {d.MaxStretch})");
        Console.WriteLine($"  --hard-edge <deg>     dihedral hard-edge threshold (default {d.HardEdgeAngleDegrees})");
        Console.WriteLine($"  --seams <mode>        {nameof(SeamSource.DihedralAngle)}|{nameof(SeamSource.ProvidedTags)}|{nameof(SeamSource.Both)} (default {d.Seams})");
        Console.WriteLine("  --no-rotate           disable 90-degree chart rotation when packing");
        Console.WriteLine("  --no-pot              do not round the atlas up to a power of two");
    }
}
