using SkiaSharp;
using SereniaBLPLib;

namespace BLPTest
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if(!Directory.Exists("in"))
            {
                Console.WriteLine("Please create an 'input' directory and place your BLP files in it.");
                return;
            }

            var testFiles = Directory.GetFiles("in");

            if(Directory.Exists("out"))
                Directory.Delete("out", true);

            Directory.CreateDirectory("out");
            foreach (var testFile in testFiles)
            {
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
            }
        }

    }
}
