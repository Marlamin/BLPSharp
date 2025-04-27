BLPSharp (formerly known as SereniaBLPLib)
=============
.NET 8.0+ library for decoding BLP textures. Uses TinyBCSharp for fast texture decompression.

Initially created under the name of SereniaBLPLib by Xalcon & later maintained by hazzik & tomrus88. 

Available on [NuGet](https://www.nuget.org/packages/BLPSharp/).

## Notes
Due to not relying on any specific image library anymore, support for BLPs containing JPEG-encoded data was removed and will throw a NYI exception. If someone wants to add this back without relying on a specific image library, feel free to open a PR.

## Sample usage
```csharp
    using (var fs = File.OpenRead("test.blp"))
    {
        var blp = new BLPFile(fs);
        var pixels = blp.GetPixels(mipmapLevel, out int w, out int h);
        // Pixel handling code, see below!
    }
```

### System.Drawing.Bitmap
```csharp
    Bitmap bmp = new Bitmap(w, h);
    BitmapData bmpdata = bmp.LockBits(new System.Drawing.Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
    Marshal.Copy(pixels, 0, bmpdata.Scan0, pic.Length);
    bmp.UnlockBits(bmpdata);
```

### ImageSharp
```csharp
    var image = SixLabors.ImageSharp.Image.LoadPixelData<Rgba32>(pixels, w, h);
```

### SkiaSharp
```csharp
    var bitmap = new SKBitmap(w, h);
    using var pixmap = bitmap.PeekPixels();
    var data = pixmap.GetPixelSpan<byte>();
    pixels.CopyTo(data);
```
