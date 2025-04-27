﻿using System;
using System.IO;
using System.Text;
using TinyBCSharp;

namespace SereniaBLPLib
{
    // Some Helper Struct to store Color-Data
    public struct ARGBColor8
    {
        public byte R;
        public byte G;
        public byte B;
        public byte A;

        /// <summary>
        /// Converts the given Pixel-Array into the BGRA-Format
        /// This will also work vice versa
        /// </summary>
        /// <param name="pixel"></param>
        public static void ConvertToBGRA(byte[] pixel)
        {
            for (int i = 0; i < pixel.Length; i += 4)
            {
                byte tmp = pixel[i]; // store red
                pixel[i] = pixel[i + 2]; // write blue into red
                pixel[i + 2] = tmp; // write stored red into blue
            }
        }
    }

    public enum BlpColorEncoding : byte
    {
        Jpeg = 0,
        Palette = 1,
        Dxt = 2,
        Argb8888 = 3,
        Argb8888_dup = 4
    }

    public enum BlpPixelFormat : byte
    {
        Dxt1 = 0,
        Dxt3 = 1,
        Argb8888 = 2,
        Argb1555 = 3,
        Argb4444 = 4,
        Rgb565 = 5,
        A8 = 6,
        Dxt5 = 7,
        Unspecified = 8,
        Argb2565 = 9,
        Bc5 = 11
    }

    public sealed class BlpFile : IDisposable
    {
        private readonly uint formatVersion; // compression: 0 = JPEG Compression, 1 = Uncompressed or DirectX Compression
        private readonly BlpColorEncoding colorEncoding; // 1 = Uncompressed, 2 = DirectX Compressed
        public readonly byte alphaSize; // 0 = no alpha, 1 = 1 Bit, 4 = Bit (only DXT3), 8 = 8 Bit Alpha
        public readonly BlpPixelFormat preferredFormat; // 0: DXT1 alpha (0 or 1 Bit alpha), 1 = DXT2/3 alpha (4 Bit), 7: DXT4/5 (interpolated alpha)
        private readonly byte hasMips; // If true (1), then there are Mipmaps
        private readonly int width; // X Resolution of the biggest Mipmap
        private readonly int height; // Y Resolution of the biggest Mipmap

        private readonly uint[] mipOffsets = new uint[16]; // Offset for every Mipmap level. If 0 = no more mitmap level
        private readonly uint[] mipSizes = new uint[16]; // Size for every level
        private readonly ARGBColor8[] paletteBGRA = new ARGBColor8[256]; // The color-palette for non-compressed pictures
        private readonly byte[] jpegHeader;

        private Stream stream; // Reference of the stream

        /// <summary>
        /// Extracts the palettized Image-Data from the given Mipmap and returns a byte-Array in the 32Bit RGBA-Format
        /// </summary>
        /// <param name="mipmap">The desired Mipmap-Level. If the given level is invalid, the smallest available level is choosen</param>
        /// <param name="w"></param>
        /// <param name="h"></param>
        /// <param name="data"></param>
        /// <returns>Pixel-data</returns>
        private byte[] GetPictureUncompressedByteArray(int w, int h, byte[] data)
        {
            int length = w * h;
            byte[] pic = new byte[length * 4];
            for (int i = 0; i < length; i++)
            {
                pic[i * 4] = paletteBGRA[data[i]].R;
                pic[i * 4 + 1] = paletteBGRA[data[i]].G;
                pic[i * 4 + 2] = paletteBGRA[data[i]].B;
                pic[i * 4 + 3] = GetAlpha(data, i, length);
            }
            return pic;
        }

        private byte GetAlpha(byte[] data, int index, int alphaStart)
        {
            switch (alphaSize)
            {
                default:
                    return 0xFF;
                case 1:
                    {
                        byte b = data[alphaStart + (index / 8)];
                        return (byte)((b & (0x01 << (index % 8))) == 0 ? 0x00 : 0xff);
                    }
                case 4:
                    {
                        byte b = data[alphaStart + (index / 2)];
                        return (byte)(index % 2 == 0 ? (b & 0x0F) << 4 : b & 0xF0);
                    }
                case 8:
                    return data[alphaStart + index];
            }
        }

        /// <summary>
        /// Returns the raw Mipmap-Image Data. This data can either be compressed or uncompressed, depending on the Header-Data
        /// </summary>
        /// <param name="mipmapLevel"></param>
        /// <returns></returns>
        private byte[] GetPictureData(int mipmapLevel, int width = 0, int height = 0)
        {
            if (stream != null)
            {
                var mipSize = mipSizes[mipmapLevel];

                // Calculate size of mipmap ourselves since included sizes can be wrong
                if ((preferredFormat == BlpPixelFormat.Dxt5) || (preferredFormat == BlpPixelFormat.Dxt3))
                {
                    mipSize = (uint)(Math.Floor((double)(width + 3) / 4) * Math.Floor((double)(height + 3) / 4) * 16);
                }
                else if (preferredFormat == BlpPixelFormat.Dxt1)
                {
                    mipSize = (uint)(Math.Floor((double)(width + 3) / 4) * Math.Floor((double)(height + 3) / 4) * 8);
                }

                byte[] data = new byte[mipSize];
                stream.Position = mipOffsets[mipmapLevel];
                stream.ReadExactly(data, 0, data.Length);
                return data;
            }
            return null;
        }

        /// <summary>
        /// Returns the amount of Mipmaps in this BLP-File
        /// </summary>
        public int MipMapCount
        {
            get
            {
                int i = 0;
                while (mipOffsets[i] != 0) i++;
                return i;
            }
        }

        private const int BLP0Magic = 0x30504c42;
        private const int BLP1Magic = 0x31504c42;
        private const int BLP2Magic = 0x32504c42;

        public BlpFile(Stream stream)
        {
            this.stream = stream;

            using (BinaryReader br = new BinaryReader(stream, Encoding.ASCII, true))
            {
                // Checking for correct Magic-Code
                int format = br.ReadInt32();
                if (format != BLP0Magic && format != BLP1Magic && format != BLP2Magic)
                    throw new Exception("Invalid BLP Format");

                switch (format)
                {
                    case BLP0Magic:
                    case BLP1Magic:
                        // Reading encoding, alphaBitDepth, alphaEncoding and hasMipmaps
                        colorEncoding = (BlpColorEncoding)br.ReadInt32();
                        alphaSize = (byte)br.ReadInt32();
                        // Reading width and height
                        width = br.ReadInt32();
                        height = br.ReadInt32();
                        preferredFormat = (BlpPixelFormat)br.ReadInt32();
                        hasMips = (byte)br.ReadInt32();
                        break;
                    case BLP2Magic:
                        formatVersion = br.ReadUInt32();// Reading type
                        if (formatVersion != 1)
                            throw new Exception("Invalid BLP-Type! Should be 1 but " + formatVersion + " was found");
                        // Reading encoding, alphaBitDepth, alphaEncoding and hasMipmaps
                        colorEncoding = (BlpColorEncoding)br.ReadByte();
                        alphaSize = br.ReadByte();
                        preferredFormat = (BlpPixelFormat)br.ReadByte();
                        hasMips = br.ReadByte();
                        // Reading width and height
                        width = br.ReadInt32();
                        height = br.ReadInt32();
                        break;
                }

                // Reading MipmapOffset Array
                for (int i = 0; i < 16; i++)
                    mipOffsets[i] = br.ReadUInt32();

                // Reading MipmapSize Array
                for (int i = 0; i < 16; i++)
                    mipSizes[i] = br.ReadUInt32();

                // When encoding is 1, there is no image compression and we have to read a color palette
                if (colorEncoding == BlpColorEncoding.Palette)
                {
                    // Reading palette
                    for (int i = 0; i < 256; i++)
                    {
                        int color = br.ReadInt32();
                        paletteBGRA[i].B = (byte)((color >> 0) & 0xFF);
                        paletteBGRA[i].G = (byte)((color >> 8) & 0xFF);
                        paletteBGRA[i].R = (byte)((color >> 16) & 0xFF);
                        paletteBGRA[i].A = (byte)((color >> 24) & 0xFF);
                    }
                }
                else if (colorEncoding == BlpColorEncoding.Jpeg)
                {
                    int jpegHeaderSize = br.ReadInt32();
                    jpegHeader = br.ReadBytes(jpegHeaderSize);
                    // what do we do with this header?
                }
            }
        }

        /// <summary>
        /// Returns the uncompressed image as a byte array in the 32pppRGBA-Format
        /// </summary>
        private byte[] GetImageBytes(int w, int h, byte[] data)
        {
            switch (colorEncoding)
            {
                case BlpColorEncoding.Jpeg:
                    throw new Exception("JPEG support not implemented yet");
                case BlpColorEncoding.Palette:
                    return GetPictureUncompressedByteArray(w, h, data);
                case BlpColorEncoding.Dxt:
                    var decoder = BlockDecoder.Create((alphaSize > 1) ? ((preferredFormat == BlpPixelFormat.Dxt5) ? BlockFormat.BC3 : BlockFormat.BC2) : BlockFormat.BC1);
                    return decoder.Decode(w, h, data);
                case BlpColorEncoding.Argb8888:
                    return data;
                default:
                    return [];
            }
        }

        /// <summary>
        /// Returns array of pixels in BGRA or RGBA order
        /// </summary>
        /// <param name="mipmapLevel"></param>
        /// <returns></returns>
        public byte[] GetPixels(int mipmapLevel, out int w, out int h)
        {
            if (mipmapLevel >= MipMapCount)
                mipmapLevel = MipMapCount - 1;
            if (mipmapLevel < 0)
                mipmapLevel = 0;

            int scale = (int)Math.Pow(2, mipmapLevel);
            w = width / scale;
            h = height / scale;

            byte[] data = GetPictureData(mipmapLevel, w, h);
            byte[] pic = GetImageBytes(w, h, data); // This bytearray stores the Pixel-Data

            if (colorEncoding != BlpColorEncoding.Argb8888) // when we want to copy the pixeldata directly into the bitmap, we have to convert them into BGRA before doing so
                ARGBColor8.ConvertToBGRA(pic);

            return pic;
        }

        /// <summary>
        /// Runs close()
        /// </summary>
        public void Dispose()
        {
            Close();
        }

        /// <summary>
        /// Closes the Memorystream
        /// </summary>
        public void Close()
        {
            if (stream != null)
            {
                stream.Close();
                stream = null;
            }
        }
    }
}
