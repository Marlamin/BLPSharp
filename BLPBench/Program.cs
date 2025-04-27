using BenchmarkDotNet.Running;

namespace BLPBench
{
    internal class Program
    {
        public static void Main(string[] args)
         => BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}
