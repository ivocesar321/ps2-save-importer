using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Ps2SaveImporter.UI;

// Uso: IconGenerator <caminho-do-.ico> [caminho-da-prévia.png]
string icoPath = args.Length > 0 ? args[0] : "AppIcon.ico";
string previewPath = args.Length > 1 ? args[1] : Path.ChangeExtension(icoPath, null) + "-preview.png";

int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 128, 256 };
var images = sizes.Select(s => (Size: s, Bitmap: Render(s))).ToList();

WriteIco(icoPath, images);
WritePreview(previewPath, images);
Console.WriteLine($"Ícone gerado: {icoPath} ({new FileInfo(icoPath).Length} bytes)");
Console.WriteLine($"Prévia: {previewPath}");

static int DetailFor(int size) => size <= 24 ? 0 : size < 48 ? 1 : 2;

static Bitmap Render(int size)
{
    int ss = size >= 128 ? 2 : 4; // supersampling para bordas limpas
    int big = size * ss;
    using var hi = new Bitmap(big, big, PixelFormat.Format32bppPArgb);
    using (var g = Graphics.FromImage(hi))
    {
        g.Clear(Color.Transparent);
        BrandIcon.Draw(g, new RectangleF(0, 0, big, big), true, DetailFor(size));
    }

    var lo = new Bitmap(size, size, PixelFormat.Format32bppArgb);
    using (var g = Graphics.FromImage(lo))
    using (var attrs = new ImageAttributes())
    {
        attrs.SetWrapMode(WrapMode.TileFlipXY);
        g.Clear(Color.Transparent);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.DrawImage(hi, new Rectangle(0, 0, size, size), 0, 0, big, big, GraphicsUnit.Pixel, attrs);
    }
    return lo;
}

static byte[] ToPng(Bitmap bmp)
{
    using var ms = new MemoryStream();
    bmp.Save(ms, ImageFormat.Png);
    return ms.ToArray();
}

static byte[] ToDib(Bitmap bmp)
{
    int w = bmp.Width, h = bmp.Height;
    using var ms = new MemoryStream();
    using var bw = new BinaryWriter(ms);
    bw.Write(40);            // BITMAPINFOHEADER
    bw.Write(w);
    bw.Write(h * 2);         // altura dobrada (imagem + máscara AND)
    bw.Write((short)1);
    bw.Write((short)32);
    bw.Write(0);
    bw.Write(w * h * 4);
    bw.Write(0); bw.Write(0); bw.Write(0); bw.Write(0);

    var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
    try
    {
        var row = new byte[w * 4];
        for (int y = h - 1; y >= 0; y--)
        {
            Marshal.Copy(data.Scan0 + y * data.Stride, row, 0, row.Length);
            bw.Write(row);
        }
    }
    finally { bmp.UnlockBits(data); }

    bw.Write(new byte[((w + 31) / 32) * 4 * h]); // máscara AND (transparência vem do canal alfa)
    bw.Flush();
    return ms.ToArray();
}

static void WriteIco(string path, List<(int Size, Bitmap Bitmap)> images)
{
    var blobs = images.Select(i => i.Size >= 256 ? ToPng(i.Bitmap) : ToDib(i.Bitmap)).ToList();
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
    using var fs = File.Create(path);
    using var bw = new BinaryWriter(fs);
    bw.Write((short)0);
    bw.Write((short)1);
    bw.Write((short)images.Count);

    int offset = 6 + 16 * images.Count;
    for (int i = 0; i < images.Count; i++)
    {
        int s = images[i].Size;
        bw.Write((byte)(s >= 256 ? 0 : s));
        bw.Write((byte)(s >= 256 ? 0 : s));
        bw.Write((byte)0);
        bw.Write((byte)0);
        bw.Write((short)1);
        bw.Write((short)32);
        bw.Write(blobs[i].Length);
        bw.Write(offset);
        offset += blobs[i].Length;
    }
    foreach (var b in blobs) bw.Write(b);
}

static void WritePreview(string path, List<(int Size, Bitmap Bitmap)> images)
{
    int[] row = { 256, 128, 48, 32, 24, 16 };
    int pad = 24;
    int width = row.Sum() + pad * (row.Length + 1);
    int height = 2 * (pad + 256 + pad + 128 + pad);
    using var canvas = new Bitmap(width, height, PixelFormat.Format32bppArgb);
    using var g = Graphics.FromImage(canvas);

    // Faixa escura (tema escuro) e faixa clara (barra de tarefas clara).
    g.Clear(Color.FromArgb(27, 31, 39));
    using (var light = new SolidBrush(Color.FromArgb(243, 243, 243)))
        g.FillRectangle(light, 0, height / 2, width, height / 2);

    void Strip(int top)
    {
        int x = pad;
        foreach (int s in row)
        {
            var img = images.First(i => i.Size == s).Bitmap;
            g.DrawImage(img, x, top + (256 - s) / 2, s, s);
            x += s + pad;
        }
        // Ampliações (pixel a pixel) de 16 e 32 px para avaliar a legibilidade.
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.DrawImage(images.First(i => i.Size == 16).Bitmap, new Rectangle(pad, top + 256 + pad, 128, 128));
        g.DrawImage(images.First(i => i.Size == 32).Bitmap, new Rectangle(pad * 2 + 128, top + 256 + pad, 128, 128));
        g.InterpolationMode = InterpolationMode.Default;
    }

    Strip(pad);
    Strip(height / 2 + pad);
    canvas.Save(path, ImageFormat.Png);
}
