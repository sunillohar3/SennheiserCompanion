using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MakeIcon;

public class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        string inputPath = @"C:\Users\sunil\.gemini\antigravity-ide\brain\486adab8-b2f1-4f4a-8680-46391b62f3a8\sennheiser_logo_icon_1789565115089.jpg";
        string outputIco = @"f:\Projects\application\src\SennheiserMomentum4\Resources\app.ico";
        string outputPng = @"f:\Projects\application\src\SennheiserMomentum4\Resources\Images\sennheiser_logo.png";

        if (!File.Exists(inputPath))
        {
            Console.WriteLine("Input image not found: " + inputPath);
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputIco)!);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPng)!);

        var original = new BitmapImage();
        using (var stream = File.OpenRead(inputPath))
        {
            original.BeginInit();
            original.CacheOption = BitmapCacheOption.OnLoad;
            original.StreamSource = stream;
            original.EndInit();
        }

        int[] sizes = [256, 128, 64, 48, 32, 16];
        byte[][] pngBuffers = new byte[sizes.Length][];

        for (int i = 0; i < sizes.Length; i++)
        {
            int size = sizes[i];
            var renderTarget = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawImage(original, new Rect(0, 0, size, size));
            }
            renderTarget.Render(visual);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(renderTarget));
            using var ms = new MemoryStream();
            encoder.Save(ms);
            pngBuffers[i] = ms.ToArray();

            if (size == 256)
            {
                File.WriteAllBytes(outputPng, pngBuffers[i]);
                Console.WriteLine("Saved 256x256 PNG: " + outputPng);
            }
        }

        // Write .ico format containing multiple resolution PNGs
        using (var fs = File.Create(outputIco))
        using (var bw = new BinaryWriter(fs))
        {
            // ICONDIR header
            bw.Write((short)0); // reserved
            bw.Write((short)1); // type 1 = icon
            bw.Write((short)sizes.Length); // count of images

            int offset = 6 + (16 * sizes.Length);

            for (int i = 0; i < sizes.Length; i++)
            {
                int s = sizes[i];
                byte b = (byte)(s >= 256 ? 0 : s);
                bw.Write(b); // width
                bw.Write(b); // height
                bw.Write((byte)0); // color count
                bw.Write((byte)0); // reserved
                bw.Write((short)1); // color planes
                bw.Write((short)32); // bits per pixel
                bw.Write((int)pngBuffers[i].Length); // size of image data
                bw.Write((int)offset); // offset of image data

                offset += pngBuffers[i].Length;
            }

            for (int i = 0; i < sizes.Length; i++)
            {
                bw.Write(pngBuffers[i]);
            }
        }

        Console.WriteLine("Successfully created multi-res ICO: " + outputIco);
    }
}
