using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace Quake2TextureConverter
{
    public class TextureUtils
    {
        /// <summary>
        /// The .wal file format header.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private unsafe struct WalHeader
        {
            public fixed byte Name[32]; // 31 chars plus null terminator
            public UInt32 Width;
            public UInt32 Height;
            public fixed UInt32 Offsets[4]; // contains four mip maps (scaled down images, starting with full size)
            public fixed byte AnimName[32]; // filename, next frame in animation chain across multiple files
            public Int32 Flags;
            public Int32 Contents;
            public Int32 Value;
        }

        /// <summary>
        /// The data for a .wal file.
        /// </summary>
        public class WalData
        {
            public string FilePath { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public byte[] PixelsBGRA { get; set; }

            public WalData(string filePath, int width, int height, byte[] pixelsBGRA)
            {
                FilePath = filePath;
                Width = width;
                Height = height;
                PixelsBGRA = pixelsBGRA;
            }
        }

        /// <summary>
        /// Reads a .pcx file and outputs the color palette array.
        /// </summary>
        /// <param name="pcxBytes">Bytes of the pcx file.</param>
        /// <returns>Color palette.</returns>
        public static System.Drawing.Color[] ReadColorPalette(byte[] pcxBytes)
        {
            if (pcxBytes.Length < 768)
            {
                throw new InvalidOperationException($"The pcx file is too small.");
            }
            var palette = new System.Drawing.Color[256];
            int startIndex = pcxBytes.Length - 768; // the last 256 pixels contains the color palette
            for (int i = 0; i < palette.Length; ++i)
            {
                byte r = pcxBytes[startIndex + (i * 3)];
                byte g = pcxBytes[startIndex + (i * 3) + 1];
                byte b = pcxBytes[startIndex + (i * 3) + 2];
                palette[i] = System.Drawing.Color.FromArgb(r, g, b);
            }
            Debug.WriteLine($"Color palette:");
            for (int i = 0; i < palette.Length; ++i)
            {
                Debug.WriteLine(
                    $"{i} = {GetRGBHexString(palette[i])}, [R={palette[i].R}, G={palette[i].G}, B={palette[i].B}]");
            }
            return palette;
        }

        /// <summary>
        /// Returns an RGB hex string of a given color.  Ex: FD05E2
        /// </summary>
        /// <param name="color">Color.</param>
        /// <returns>RGB hex string.</returns>
        public static string GetRGBHexString(System.Drawing.Color color)
        {
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        /// <summary>
        /// Converts a wal file to a png file in the given output folder.
        /// </summary>
        /// <param name="palette">Color palette.</param>
        /// <param name="walFilePath">Wal file.</param>
        /// <param name="outputFolder">Output folder.</param>
        /// <param name="replaceTransparency">True to replace the transparent color with png alpha.</param>
        /// <param name="asPng">True to save as png.</param>
        /// <param name="asJpg">True to save as jpg.</param>
        /// <param name="jpgQuality">Jpg quality from 0-100.</param>
        public static void ConvertWalFile(System.Drawing.Color[] palette, string walFilePath,
            string outputFolder, bool replaceTransparency, bool asPng, bool asJpg, int jpgQuality)
        {
            var walData = ReadWalFile(palette, walFilePath, replaceTransparency);

            using (Bitmap bitmap = new Bitmap(walData.Width, walData.Height, 
                System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                Rectangle rect = new(0, 0, walData.Width, walData.Height);
                BitmapData bmpData = bitmap.LockBits(rect, ImageLockMode.WriteOnly, bitmap.PixelFormat);
                try
                {
                    Marshal.Copy(walData.PixelsBGRA, 0, bmpData.Scan0, walData.PixelsBGRA.Length);
                }
                finally
                {
                    bitmap.UnlockBits(bmpData);
                }

                _ = Directory.CreateDirectory(outputFolder);

                if (asPng)
                {
                    string filenameNoExt = Path.GetFileNameWithoutExtension(walFilePath);
                    string pngFilePath = Path.Combine(outputFolder, $"{filenameNoExt}.png");
                    bitmap.Save(pngFilePath, ImageFormat.Png);
                    Debug.WriteLine($"Saved file '{pngFilePath}'.");
                }

                if (asJpg)
                {
                    ImageCodecInfo jpegEncoderInfo = null;
                    ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();
                    foreach (ImageCodecInfo codec in codecs)
                    {
                        if (codec.FormatID == ImageFormat.Jpeg.Guid)
                        {
                            jpegEncoderInfo = codec;
                        }
                    }
                    using EncoderParameters encoderParams = new EncoderParameters(count: 1);
                    using EncoderParameter qualityParam = new EncoderParameter(
                        System.Drawing.Imaging.Encoder.Quality, jpgQuality);
                    encoderParams.Param[0] = qualityParam;

                    string filenameNoExt = Path.GetFileNameWithoutExtension(walFilePath);
                    string jpgFilePath = Path.Combine(outputFolder, $"{filenameNoExt}.jpg");
                    bitmap.Save(jpgFilePath, jpegEncoderInfo, encoderParams);
                    Debug.WriteLine($"Saved file '{jpgFilePath}'.");
                }
            }
        }

        /// <summary>
        /// Opens up a wal file and reads the pixel data.
        /// </summary>
        /// <param name="palette">Color palette.</param>
        /// <param name="walFilePath">Wal file.</param>
        /// <param name="replaceTransparency">True to replace the transparent color with png alpha.</param>
        /// <returns>WalData.</returns>
        public static WalData ReadWalFile(System.Drawing.Color[] palette, string walFilePath, 
            bool replaceTransparency)
        {
            // The transparent color is the very last item in the Q2 color palette.
            System.Drawing.Color transparentColor = palette[255];

            byte[] allBytes = File.ReadAllBytes(walFilePath);
            int headerSize = Marshal.SizeOf<WalHeader>();
            if (allBytes.Length < headerSize)
            {
                throw new InvalidOperationException(
                    $"File '{walFilePath}' is too small to contain the header.");
            }
            ReadOnlySpan<byte> headerBytes = allBytes.AsSpan(0, headerSize);
            ReadOnlySpan<WalHeader> headerSpan = MemoryMarshal.Cast<byte, WalHeader>(headerBytes);

            unsafe
            {
                uint width = headerSpan[0].Width;
                uint height = headerSpan[0].Height;
                uint offset = headerSpan[0].Offsets[0];

                byte[] pixelsBGRA = null;
                try
                {
                    pixelsBGRA = new byte[width * height * 4];
                }
                catch (Exception)
                {
                    throw new InvalidOperationException(
                        $"File '{walFilePath}' has bad width, height, or offset values.");
                }

                if (allBytes.Length < (offset + (width * height)))
                {
                    throw new InvalidOperationException(
                        $"File '{walFilePath}' is too small to contain the image data.");
                }

                for (uint i = 0; i < (width * height); ++i)
                {
                    uint currentOffset = offset + i;
                    byte walPixel = allBytes[currentOffset];
                    System.Drawing.Color lookupColor = palette[walPixel];
                    pixelsBGRA[(i * 4) + 0] = lookupColor.B;
                    pixelsBGRA[(i * 4) + 1] = lookupColor.G;
                    pixelsBGRA[(i * 4) + 2] = lookupColor.R;
                    pixelsBGRA[(i * 4) + 3] = 0xFF;
                    if (replaceTransparency)
                    {
                        if ((lookupColor.B == transparentColor.B)
                            && (lookupColor.G == transparentColor.G)
                            && (lookupColor.R == transparentColor.R))
                        {
                            pixelsBGRA[(i * 4) + 3] = 0x00;
                        }
                    }
                }

                WalData walData = new(walFilePath, (int)width, (int)height, pixelsBGRA);
                return walData;
            }
        }
    }
}
