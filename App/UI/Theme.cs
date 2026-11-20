using System.Drawing.Drawing2D;

namespace Ps2SaveImporter.UI;

/// <summary>Paleta e tipografia do PS2 Save Importer (tema escuro, azul elétrico).</summary>
internal static class Theme
{
    // Superfícies
    public static readonly Color Bg = Color.FromArgb(15, 17, 21);            // grafite quase preto
    public static readonly Color Surface = Color.FromArgb(23, 27, 34);       // cinza azulado escuro
    public static readonly Color Inset = Color.FromArgb(17, 21, 27);         // áreas rebaixadas
    public static readonly Color Hover = Color.FromArgb(29, 34, 43);
    public static readonly Color Button = Color.FromArgb(35, 42, 54);
    public static readonly Color ButtonHover = Color.FromArgb(46, 55, 71);
    public static readonly Color Border = Color.FromArgb(38, 44, 55);
    public static readonly Color BorderStrong = Color.FromArgb(54, 63, 79);

    // Cor principal
    public static readonly Color Accent = Color.FromArgb(47, 123, 255);      // azul elétrico
    public static readonly Color AccentHover = Color.FromArgb(76, 141, 255);
    public static readonly Color AccentPressed = Color.FromArgb(31, 102, 224);
    public static readonly Color AccentSoft = Color.FromArgb(20, 32, 54);

    // Texto
    public static readonly Color Text = Color.FromArgb(242, 245, 250);
    public static readonly Color TextSecondary = Color.FromArgb(163, 173, 189);
    public static readonly Color TextMuted = Color.FromArgb(124, 135, 153);

    // Estados
    public static readonly Color Success = Color.FromArgb(63, 182, 122);
    public static readonly Color SuccessSoft = Color.FromArgb(20, 43, 34);
    public static readonly Color Danger = Color.FromArgb(229, 83, 75);
    public static readonly Color DangerSoft = Color.FromArgb(46, 25, 28);

    private static readonly Dictionary<string, Font> Fonts = new();

    public static Font Font(string family, float pt)
    {
        string key = family + "|" + pt.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (!Fonts.TryGetValue(key, out var f))
            Fonts[key] = f = new Font(family, pt, FontStyle.Regular, GraphicsUnit.Point);
        return f;
    }

    public static Font Regular(float pt) => Font("Segoe UI", pt);
    public static Font Semi(float pt) => Font("Segoe UI Semibold", pt);
    public static Font Mono(float pt) => Font("Consolas", pt);
}

internal static class UiExt
{
    public static int Dp(this Control c, float v) => (int)Math.Round(v * c.DeviceDpi / 96f);
}

/// <summary>Helpers de desenho (retângulos arredondados, mistura de cores, texto).</summary>
internal static class Gfx
{
    public const TextFormatFlags Line =
        TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter |
        TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine;

    public const TextFormatFlags PathLine =
        TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter |
        TextFormatFlags.Left | TextFormatFlags.PathEllipsis | TextFormatFlags.SingleLine;

    public const TextFormatFlags Wrap =
        TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak | TextFormatFlags.Left | TextFormatFlags.Top;

    public static GraphicsPath RoundRect(RectangleF r, float radius)
    {
        float d = Math.Min(radius * 2f, Math.Min(r.Width, r.Height));
        var p = new GraphicsPath();
        if (d <= 0.5f)
        {
            p.AddRectangle(r);
            return p;
        }
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public static void FillRound(Graphics g, Color color, RectangleF r, float radius)
    {
        using var brush = new SolidBrush(color);
        using var path = RoundRect(r, radius);
        g.FillPath(brush, path);
    }

    public static void StrokeRound(Graphics g, Color color, RectangleF r, float radius, float width = 1f)
    {
        using var pen = new Pen(color, width);
        using var path = RoundRect(r, radius);
        g.DrawPath(pen, path);
    }

    public static void Circle(Graphics g, Color color, RectangleF r)
    {
        using var brush = new SolidBrush(color);
        g.FillEllipse(brush, r);
    }

    public static Color Blend(Color a, Color b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return Color.FromArgb(
            (int)(a.R + (b.R - a.R) * t),
            (int)(a.G + (b.G - a.G) * t),
            (int)(a.B + (b.B - a.B) * t));
    }

    public static void Text(Graphics g, string text, Font font, Color color, Rectangle bounds, TextFormatFlags flags = Line) =>
        TextRenderer.DrawText(g, text, font, bounds, color, flags);

    public static int TextWidth(Graphics g, string text, Font font) =>
        TextRenderer.MeasureText(g, text, font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width;

    public static int TextHeight(Graphics g, string text, Font font, int width) =>
        TextRenderer.MeasureText(g, text, font, new Size(Math.Max(1, width), int.MaxValue), Wrap).Height;
}
