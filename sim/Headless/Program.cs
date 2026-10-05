using SixActual.Sim;

namespace SixActual.Headless;

public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length == 0 || args.Contains("--help"))
        {
            Console.WriteLine("usage: SixActual.Headless --selftest | --determinism");
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

        Console.Error.WriteLine("Unknown arguments. Use --selftest or --determinism.");
        return 2;
    }
}
