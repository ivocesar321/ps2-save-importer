using System.Drawing.Drawing2D;

namespace Ps2SaveImporter.UI;

/// <summary>
/// Marca do PS2 Save Importer: um Memory Card (canto chanfrado, contatos dourados) com uma seta de
/// importação em pixel art, sobre um bloco grafite. Desenho 100% original e vetorial; a mesma rotina
/// gera o ícone .ico (tools/IconGenerator) e o logo exibido no cabeçalho.
/// </summary>
internal static class BrandIcon
{
    private static readonly Color CardTop = Color.FromArgb(98, 164, 255);
    private static readonly Color CardBottom = Color.FromArgb(36, 102, 235);
    private static readonly Color Gold = Color.FromArgb(233, 198, 108);
    private static readonly Color Window = Color.FromArgb(236, 11, 16, 28);
    private static readonly Color Arrow = Color.FromArgb(196, 222, 255);

    // Cada linha da seta em pixel art (5x5).
    private static readonly string[] PixelArrow = { "..#..", "..#..", "#.#.#", ".###.", "..#.." };

    /// <param name="detail">0 = muito pequeno (≤24 px), 1 = pequeno/médio, 2 = grande (≥48 px). Se nulo, é deduzido do tamanho.</param>
    public static void Draw(Graphics g, RectangleF bounds, bool tile = true, int? detail = null)
    {
        float size = Math.Min(bounds.Width, bounds.Height);
        int level = detail ?? (size < 28 ? 0 : size < 48 ? 1 : 2);

        var state = g.Save();
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TranslateTransform(bounds.X, bounds.Y);
        float s = size / 256f;
        g.ScaleTransform(s, s);

        if (tile) DrawTile(g);
        if (level == 2) DrawGlow(g);
        DrawCard(g, level);

        g.Restore(state);
    }

    private static void DrawTile(Graphics g)
    {
        var rect = new RectangleF(0, 0, 256, 256);
        using (var path = Gfx.RoundRect(rect, 58))
        using (var brush = new LinearGradientBrush(new PointF(0, 0), new PointF(0, 256),
                   Color.FromArgb(32, 40, 57), Color.FromArgb(12, 15, 21)))
            g.FillPath(brush, path);

        using var border = Gfx.RoundRect(new RectangleF(1.5f, 1.5f, 253, 253), 57);
        using var pen = new Pen(Color.FromArgb(46, 255, 255, 255), 3f);
        g.DrawPath(pen, border);
    }

    private static void DrawGlow(Graphics g)
    {
        using var gp = new GraphicsPath();
        gp.AddEllipse(24, 36, 208, 208);
        using var pg = new PathGradientBrush(gp)
        {
            CenterColor = Color.FromArgb(64, 47, 123, 255),
            SurroundColors = new[] { Color.FromArgb(0, 47, 123, 255) },
        };
        g.FillPath(pg, gp);
    }

    private static void DrawCard(Graphics g, int level)
    {
        float x, y, w, h, c, r;
        if (level == 0) { x = 46; y = 28; w = 164; h = 200; c = 46; r = 20; }
        else { x = 60; y = 38; w = 136; h = 180; c = 40; r = 18; }

        using var card = new GraphicsPath();
        card.AddArc(x, y, r * 2, r * 2, 180, 90);
        card.AddLine(x + r, y, x + w - c, y);
        card.AddLine(x + w - c, y, x + w, y + c);
        card.AddArc(x + w - r * 2, y + h - r * 2, r * 2, r * 2, 0, 90);
        card.AddArc(x, y + h - r * 2, r * 2, r * 2, 90, 90);
        card.CloseFigure();

        using (var fill = new LinearGradientBrush(new PointF(0, y), new PointF(0, y + h), CardTop, CardBottom))
            g.FillPath(fill, card);

        if (level >= 1)
        {
            using var hl = new Pen(Color.FromArgb(70, 255, 255, 255), 3f) { LineJoin = LineJoin.Round };
            g.DrawLines(hl, new[] { new PointF(x + r * 0.35f, y + r * 0.9f), new PointF(x + r, y + 2f), new PointF(x + w - c, y + 2f), new PointF(x + w - 2f, y + c) });
        }

        // Janela de etiqueta.
        var win = new RectangleF(x + w * 0.15f, y + h * 0.14f, w * 0.70f, h * 0.42f);
        using (var wp = Gfx.RoundRect(win, level == 0 ? 10 : 12))
        using (var wb = new SolidBrush(Window))
            g.FillPath(wb, wp);

        if (level == 0) return;

        // Seta de importação.
        if (level == 2) DrawPixelArrow(g, win); else DrawVectorArrow(g, win);

        // Contatos dourados.
        int pads = level == 2 ? 4 : 3;
        float padW = level == 2 ? 15f : 20f;
        float span = w * 0.70f;
        float gap = (span - pads * padW) / (pads - 1);
        float py = y + h * 0.72f, ph = h * 0.15f;
        using var gold = new SolidBrush(Gold);
        for (int i = 0; i < pads; i++)
        {
            var pad = new RectangleF(x + w * 0.15f + i * (padW + gap), py, padW, ph);
            using var pp = Gfx.RoundRect(pad, 3f);
            g.FillPath(gold, pp);
        }
    }

    private static void DrawPixelArrow(Graphics g, RectangleF win)
    {
        float cell = Math.Min(win.Width / 7.2f, win.Height / 6.2f);
        float total = cell * 5f;
        float ox = win.X + (win.Width - total) / 2f;
        float oy = win.Y + (win.Height - total) / 2f;
        using var brush = new SolidBrush(Arrow);
        for (int row = 0; row < 5; row++)
            for (int col = 0; col < 5; col++)
                if (PixelArrow[row][col] == '#')
                    g.FillRectangle(brush, ox + col * cell, oy + row * cell, cell + 0.4f, cell + 0.4f);
    }

    private static void DrawVectorArrow(Graphics g, RectangleF win)
    {
        float cx = win.X + win.Width / 2f, cy = win.Y + win.Height / 2f;
        float u = Math.Min(win.Width, win.Height) * 0.32f;
        using var brush = new SolidBrush(Arrow);
        g.FillRectangle(brush, cx - u * 0.28f, cy - u * 1.0f, u * 0.56f, u * 1.1f);
        g.FillPolygon(brush, new[]
        {
            new PointF(cx - u * 0.95f, cy - u * 0.05f),
            new PointF(cx + u * 0.95f, cy - u * 0.05f),
            new PointF(cx, cy + u * 0.95f),
        });
    }
}
