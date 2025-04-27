using BenchmarkDotNet.Attributes;

namespace BLPBench
{
    [MemoryDiagnoser]
    public class Decomp
    {
        private static byte[] data = File.ReadAllBytes("D:\\Downloads\\azeroth_32_32.blp");
        [Benchmark]
        public void Decode()
        {
            using(var stream = new MemoryStream(data))
            {
                using (var blp = new SereniaBLPLib.BlpFile(stream))
                {
                    blp.GetPixels(0, out var width, out var height);
                }
            }
        }
    }
}