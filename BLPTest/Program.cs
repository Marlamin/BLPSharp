using SkiaSharp;
using BLPSharp;
using System.Diagnostics;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp;
using System.Runtime.InteropServices;

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
                    var blp = new BLPFile(fs);

                    var pixels = blp.GetPixels(0, out var w, out var h);

                    // SkiaSharp
                    var bitmap = new SKBitmap(w, h, SKColorType.Bgra8888, SKAlphaType.Unpremul);

                    using var pixmap = bitmap.PeekPixels();
                    var data = pixmap.GetPixelSpan<byte>();
                    pixels.CopyTo(data);

                    using (var image = bitmap.Encode(SKEncodedImageFormat.Png, 100))
                    using (var stream = File.OpenWrite($"out/{Path.GetFileNameWithoutExtension(testFile)}_skiasharp.png"))
                    {
                        image.SaveTo(stream);
                    }

                    // ImageSharp
                    var imageSharp = SixLabors.ImageSharp.Image.LoadPixelData<Bgra32>(pixels, w, h);
                    using (var stream = File.OpenWrite($"out/{Path.GetFileNameWithoutExtension(testFile)}_imagesharp.png"))
                    {
                        imageSharp.SaveAsPng(stream);
                    }

                    // System.Drawing
                    using (var bitmap2 = new System.Drawing.Bitmap(w, h))
                    {
                        var bitmapData = bitmap2.LockBits(new System.Drawing.Rectangle(0, 0, bitmap2.Width, bitmap2.Height), System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                        Marshal.Copy(pixels, 0, bitmapData.Scan0, pixels.Length);
                        bitmap2.UnlockBits(bitmapData);
                        bitmap2.Save($"out/{Path.GetFileNameWithoutExtension(testFile)}_sysdraw.png");
                    }
                }

                sw.Stop();
                Console.WriteLine($"Processed {Path.GetFileName(testFile)} in {sw.ElapsedMilliseconds}ms");
            }
        }

    }
}
