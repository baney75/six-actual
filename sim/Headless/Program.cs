using SixActual.Sim;
using SixActual.Sim.Battle;

namespace SixActual.Headless;

public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length == 0 || args.Contains("--help"))
        {
            Console.WriteLine("usage: SixActual.Headless --selftest | --determinism | --scenario <file-or-dir>...");
            return args.Length == 0 ? 2 : 0;
        }

        if (args.Contains("--selftest"))
        {
            Console.WriteLine($"selftest hash={DeterminismReport.SelfTest()}");
            return 0;
        }

        if (args.Contains("--determinism"))
        {
            foreach (var line in DeterminismReport.Lines())
            {
                Console.WriteLine(line);
            }

            return 0;
        }

        if (args[0] == "--soak")
        {
            return Soak.Run(args.Skip(1).ToArray());
        }

        if (args[0] == "--bench")
        {
            return Bench.Run(args.Skip(1).ToArray());
        }

        if (args[0] == "--gs-soak")
        {
            return GsRunner.Soak(args.Skip(1).ToArray());
        }

        if (args[0] == "--scenario")
        {
            return ScenarioRunner.Run(args.Skip(1).ToArray());
        }

        Console.Error.WriteLine("Unknown arguments. Use --selftest, --determinism or --scenario.");
        return 2;
    }
}
