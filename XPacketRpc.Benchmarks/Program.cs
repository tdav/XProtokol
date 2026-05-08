using BenchmarkDotNet.Running;

namespace XPacketRpc.Benchmarks;

public static class Program
{
    public static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--wire-size")
        {
            System.Console.WriteLine(WireSizeReport.Generate());
            return;
        }
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, new BenchConfig());
    }
}
