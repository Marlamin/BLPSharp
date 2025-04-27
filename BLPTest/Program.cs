using SkiaSharp;
using SereniaBLPLib;
using System.Diagnostics;

namespace BLPTest
{
    internal class Program
    {
        static void Main()
        {
            if(!Directory.Exists("in"))
            {
                Console.WriteLine("Please create an 'in' directory and place your BLP files in it.");
                return;
            }

            var testFiles = Directory.GetFiles("in");

            if(Directory.Exists("out"))
                Directory.Delete("out", true);

            Directory.CreateDirectory("out");

            var sw = new Stopwatch();
            foreach (var testFile in testFiles)
            {
                sw.Restart();
                using (var fs = File.OpenRead(testFile))
                {
                    var blp = new BlpFile(fs);
                        
                    var pixels = blp.GetPixels(0, out var width, out var height);
                    var bitmap = new SKBitmap((int)width, (int)height);

                    using var pixmap = bitmap.PeekPixels();
                    var data = pixmap.GetPixelSpan<byte>();
                    pixels.CopyTo(data);

                    using (var image = bitmap.Encode(SKEncodedImageFormat.Png, 100))
                    using (var stream = File.OpenWrite($"out/{Path.GetFileNameWithoutExtension(testFile)}.png"))
                    {
                        image.SaveTo(stream);
                    }
                }
                sw.Stop();
                Console.WriteLine($"Processed {Path.GetFileName(testFile)} in {sw.ElapsedMilliseconds}ms");
            }
        }

    }
}
