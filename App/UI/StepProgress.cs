using System.Drawing.Drawing2D;
using Ps2SaveImporter.Core;

namespace Ps2SaveImporter.UI;

/// <summary>
/// Progresso por etapas. Só mostra dados reais: o número de etapas concluídas e a etapa em andamento
/// (o mymc.exe não informa porcentagem, então nenhuma porcentagem é exibida nem inventada).
/// </summary>
internal sealed class StepProgress : ModernControl
{
    private sealed class Item
    {
        public string Title = string.Empty;
        public StepState State;
        public string? Detail;
    }

    private readonly List<Item> _items = new();
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 16 };
    private float _angle;
    private float _phase;
    private string _summary = "Aguardando";

    public StepProgress(IEnumerable<string> titles)
    {
        foreach (var t in titles) _items.Add(new Item { Title = t });
        _timer.Tick += (_, _) =>
        {
            _angle = (_angle + 9f) % 360f;
            _phase = (_phase + 0.014f) % 1f;
            Invalidate();
        };
        AccessibleRole = AccessibleRole.ProgressBar;
    }

    public string Summary
    {
        get => _summary;
        set { _summary = value; Invalidate(); }
    }

    public int StepCount => _items.Count;
    public int PreferredHeight => Dp(52) + _items.Count * Dp(27);

    public override Size GetPreferredSize(Size proposedSize) => new(Math.Max(Dp(200), proposedSize.Width), PreferredHeight);

    public void Reset()
    {
        foreach (var i in _items) { i.State = StepState.Pending; i.Detail = null; }
        _summary = "Aguardando";
        UpdateTimer();
        Invalidate();
    }

    public void Apply(StepUpdate update)
    {
        var item = _items[(int)update.Step];
        item.State = update.State;
        if (update.Detail is not null || update.State is StepState.Done or StepState.Failed) item.Detail = update.Detail;
        UpdateTimer();
        Invalidate();
    }

    private void UpdateTimer()
    {
        bool running = _items.Any(i => i.State == StepState.Running);
        if (running && !_timer.Enabled) _timer.Start();
        else if (!running && _timer.Enabled) _timer.Stop();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Smooth(g);
        g.Clear(BackColor);

        // Cabeçalho.
        Gfx.Text(g, "Progresso", Theme.Semi(9.5f), Theme.TextSecondary, new Rectangle(0, 0, Width / 2, Dp(22)));
        Gfx.Text(g, _summary, Theme.Regular(9f), Theme.TextMuted,
            new Rectangle(Width / 2, 0, Width - Width / 2, Dp(22)), Gfx.Line | TextFormatFlags.Right);

        // Barra segmentada: um segmento por etapa.
        int barY = Dp(30), barH = Dp(6), gap = Dp(5);
        float segW = (Width - gap * (_items.Count - 1)) / (float)_items.Count;
        for (int i = 0; i < _items.Count; i++)
            DrawSegment(g, _items[i], new RectangleF(i * (segW + gap), barY, segW, barH));

        // Lista de etapas.
        int rowH = Dp(27);
        int y = Dp(52);
        var detailFont = Theme.Regular(8.5f);
        foreach (var it in _items)
        {
            int ic = Dp(18);
            var icon = new RectangleF(Dp(1), y + (rowH - ic) / 2f, ic, ic);
            DrawStateIcon(g, it.State, icon);

            var titleColor = it.State switch
            {
                StepState.Pending or StepState.Skipped => Theme.TextMuted,
                StepState.Failed => Theme.Danger,
                _ => Theme.Text,
            };
            var titleFont = it.State == StepState.Running ? Theme.Semi(9.5f) : Theme.Regular(9.5f);

            int detailW = it.Detail is null ? 0 : Math.Min(Width * 2 / 5, Gfx.TextWidth(g, it.Detail, detailFont) + 2);
            int textX = Dp(30);
            Gfx.Text(g, it.Title, titleFont, titleColor, new Rectangle(textX, y, Math.Max(0, Width - textX - detailW - Dp(10)), rowH));
            if (detailW > 0)
                Gfx.Text(g, it.Detail!, detailFont, Theme.TextMuted,
                    new Rectangle(Width - detailW, y, detailW, rowH), Gfx.Line | TextFormatFlags.Right);
            y += rowH;
        }
    }

    private void DrawSegment(Graphics g, Item it, RectangleF r)
    {
        float radius = r.Height / 2f;
        switch (it.State)
        {
            case StepState.Done:
                Gfx.FillRound(g, Theme.Accent, r, radius);
                break;
            case StepState.Failed:
                Gfx.FillRound(g, Theme.Danger, r, radius);
                break;
            case StepState.Skipped:
                Gfx.FillRound(g, Theme.BorderStrong, r, radius);
                break;
            case StepState.Running:
            {
                float pulse = 0.5f + 0.5f * MathF.Sin(_phase * MathF.PI * 2f);
                Gfx.FillRound(g, Gfx.Blend(Theme.AccentPressed, Theme.AccentHover, pulse), r, radius);

                // Brilho que percorre o segmento: indica atividade sem sugerir uma porcentagem.
                var state = g.Save();
                using var clip = Gfx.RoundRect(r, radius);
                g.SetClip(clip, CombineMode.Intersect);
                float bw = r.Width * 0.55f;
                float bx = r.X - bw + (r.Width + bw) * _phase;
                using var br = new LinearGradientBrush(new RectangleF(bx, r.Y, bw, r.Height), Color.Transparent, Color.Transparent, 0f);
                br.InterpolationColors = new ColorBlend
                {
                    Positions = new[] { 0f, 0.5f, 1f },
                    Colors = new[] { Color.FromArgb(0, 255, 255, 255), Color.FromArgb(120, 255, 255, 255), Color.FromArgb(0, 255, 255, 255) },
                };
                g.FillRectangle(br, bx, r.Y, bw, r.Height);
                g.Restore(state);
                break;
            }
            default:
                Gfx.FillRound(g, Theme.Border, r, radius);
                break;
        }
    }

    private void DrawStateIcon(Graphics g, StepState state, RectangleF r)
    {
        switch (state)
        {
            case StepState.Done:
                Gfx.Circle(g, Theme.SuccessSoft, r);
                Icons.Draw(g, IconKind.Check, RectangleF.Inflate(r, -Dp(4), -Dp(4)), Theme.Success, 2.6f);
                break;
            case StepState.Failed:
                Gfx.Circle(g, Theme.DangerSoft, r);
                Icons.Draw(g, IconKind.Cross, RectangleF.Inflate(r, -Dp(4), -Dp(4)), Theme.Danger, 2.6f);
                break;
            case StepState.Running:
                Icons.DrawSpinner(g, r, Theme.AccentHover, Theme.Border, _angle, Dp(2));
                break;
            case StepState.Skipped:
            {
                using var pen = new Pen(Theme.BorderStrong, 1.5f);
                g.DrawEllipse(pen, RectangleF.Inflate(r, -0.75f, -0.75f));
                g.DrawLine(pen, r.X + r.Width * 0.3f, r.Y + r.Height / 2f, r.Right - r.Width * 0.3f, r.Y + r.Height / 2f);
                break;
            }
            default:
            {
                using var pen = new Pen(Theme.BorderStrong, 1.5f);
                g.DrawEllipse(pen, RectangleF.Inflate(r, -0.75f, -0.75f));
                break;
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _timer.Dispose();
        base.Dispose(disposing);
    }
}
