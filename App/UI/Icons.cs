using System.Drawing.Drawing2D;

namespace Ps2SaveImporter.UI;

internal enum IconKind
{
    File, Folder, Search, Refresh, Check, CheckCircle, Cross, XCircle,
    Info, Alert, Shield, MemoryCard, Import, Copy, Chevron, Plus,
}

/// <summary>Ícones vetoriais (grade 24x24, traço arredondado) desenhados com GDI+: nítidos em qualquer DPI.</summary>
internal static class Icons
{
    public static void Draw(Graphics g, IconKind kind, RectangleF box, Color color, float weight = 1.8f)
    {
        var state = g.Save();
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        float k = Math.Min(box.Width, box.Height) / 24f;
        float ox = box.X + (box.Width - 24f * k) / 2f;
        float oy = box.Y + (box.Height - 24f * k) / 2f;
        PointF P(float x, float y) => new(ox + x * k, oy + y * k);
        RectangleF R(float x, float y, float w, float h) => new(ox + x * k, oy + y * k, w * k, h * k);

        using var pen = new Pen(color, weight * k)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round,
        };
        using var brush = new SolidBrush(color);

        switch (kind)
        {
            case IconKind.File:
            {
                using var p = new GraphicsPath();
                p.AddLines(new[] { P(6.5f, 3), P(14, 3), P(19, 8), P(19, 21), P(6.5f, 21) });
                p.CloseFigure();
                g.DrawPath(pen, p);
                g.DrawLines(pen, new[] { P(14, 3), P(14, 8), P(19, 8) });
                break;
            }
            case IconKind.Folder:
            {
                using var p = new GraphicsPath();
                p.AddLines(new[] { P(3, 6.5f), P(9, 6.5f), P(11.2f, 9), P(21, 9), P(21, 19), P(3, 19) });
                p.CloseFigure();
                g.DrawPath(pen, p);
                break;
            }
            case IconKind.Search:
                g.DrawEllipse(pen, R(4, 4, 12, 12));
                g.DrawLine(pen, P(14.5f, 14.5f), P(20, 20));
                break;
            case IconKind.Refresh:
            {
                g.DrawArc(pen, R(4, 4, 16, 16), -40, 290);
                float a = 250f * MathF.PI / 180f;
                var tip = P(12 + 8 * MathF.Cos(a), 12 + 8 * MathF.Sin(a));
                float tx = -MathF.Sin(a), ty = MathF.Cos(a);
                foreach (float ang in new[] { 0.65f, -0.65f })
                {
                    float c = MathF.Cos(ang), s = MathF.Sin(ang);
                    float bx = -(tx * c - ty * s), by = -(tx * s + ty * c);
                    g.DrawLine(pen, tip, new PointF(tip.X + bx * 4.2f * k, tip.Y + by * 4.2f * k));
                }
                break;
            }
            case IconKind.Check:
                g.DrawLines(pen, new[] { P(5, 12.5f), P(10, 17.5f), P(19, 7) });
                break;
            case IconKind.CheckCircle:
                g.DrawEllipse(pen, R(3, 3, 18, 18));
                g.DrawLines(pen, new[] { P(8, 12.3f), P(11, 15.2f), P(16, 9.6f) });
                break;
            case IconKind.Cross:
                g.DrawLine(pen, P(6, 6), P(18, 18));
                g.DrawLine(pen, P(18, 6), P(6, 18));
                break;
            case IconKind.XCircle:
                g.DrawEllipse(pen, R(3, 3, 18, 18));
                g.DrawLine(pen, P(9, 9), P(15, 15));
                g.DrawLine(pen, P(15, 9), P(9, 15));
                break;
            case IconKind.Info:
                g.DrawEllipse(pen, R(3, 3, 18, 18));
                g.DrawLine(pen, P(12, 11), P(12, 16.5f));
                g.FillEllipse(brush, R(11, 6.9f, 2, 2));
                break;
            case IconKind.Alert:
            {
                using var p = new GraphicsPath();
                p.AddLines(new[] { P(12, 4), P(21, 19.5f), P(3, 19.5f) });
                p.CloseFigure();
                g.DrawPath(pen, p);
                g.DrawLine(pen, P(12, 10), P(12, 14));
                g.FillEllipse(brush, R(11, 16f, 2, 2));
                break;
            }
            case IconKind.Shield:
            {
                using var p = new GraphicsPath();
                p.AddLine(P(12, 3), P(19.5f, 6));
                p.AddLine(P(19.5f, 6), P(19.5f, 12));
                p.AddBezier(P(19.5f, 12), P(19.5f, 16.5f), P(16, 19.8f), P(12, 21));
                p.AddBezier(P(12, 21), P(8, 19.8f), P(4.5f, 16.5f), P(4.5f, 12));
                p.AddLine(P(4.5f, 12), P(4.5f, 6));
                p.CloseFigure();
                g.DrawPath(pen, p);
                g.DrawLines(pen, new[] { P(8.8f, 12), P(11, 14.2f), P(15.2f, 9.8f) });
                break;
            }
            case IconKind.MemoryCard:
            {
                using var p = new GraphicsPath();
                p.AddLines(new[] { P(6, 3), P(14.5f, 3), P(18.5f, 7), P(18.5f, 21), P(6, 21) });
                p.CloseFigure();
                g.DrawPath(pen, p);
                g.DrawLine(pen, P(9.5f, 15.5f), P(9.5f, 18));
                g.DrawLine(pen, P(12.25f, 15.5f), P(12.25f, 18));
                g.DrawLine(pen, P(15, 15.5f), P(15, 18));
                break;
            }
            case IconKind.Import:
                g.DrawLine(pen, P(12, 4), P(12, 14));
                g.DrawLines(pen, new[] { P(8, 10.2f), P(12, 14.2f), P(16, 10.2f) });
                g.DrawLines(pen, new[] { P(5, 16), P(5, 20), P(19, 20), P(19, 16) });
                break;
            case IconKind.Copy:
            {
                using var p = Gfx.RoundRect(R(9, 9, 11, 11), 2.4f * k);
                g.DrawPath(pen, p);
                g.DrawLines(pen, new[] { P(15, 5.5f), P(5.5f, 5.5f), P(5.5f, 15), P(9, 15) });
                break;
            }
            case IconKind.Chevron:
                g.DrawLines(pen, new[] { P(7, 10), P(12, 15), P(17, 10) });
                break;
            case IconKind.Plus:
                g.DrawLine(pen, P(12, 5), P(12, 19));
                g.DrawLine(pen, P(5, 12), P(19, 12));
                break;
        }

        g.Restore(state);
    }

    /// <summary>Indicador de atividade: anel fraco + arco que gira.</summary>
    public static void DrawSpinner(Graphics g, RectangleF box, Color color, Color track, float angle, float weight = 2f)
    {
        var state = g.Save();
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var r = RectangleF.Inflate(box, -weight / 2f, -weight / 2f);
        using (var tp = new Pen(track, weight))
            g.DrawEllipse(tp, r);
        using var pen = new Pen(color, weight) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawArc(pen, r, angle, 95);
        g.Restore(state);
    }
}
