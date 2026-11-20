using System.Drawing.Drawing2D;
using Ps2SaveImporter.Core;
using Ps2SaveImporter.UI;

namespace Ps2SaveImporter.Views;

/// <summary>Cartão de seção: superfície arredondada com borda, número do passo e título.</summary>
internal class SectionCard : Panel
{
    public SectionCard(int number, string title)
    {
        Number = number;
        Title = title;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        DoubleBuffered = true;
        BackColor = Theme.Surface;
    }

    public int Number { get; }
    public string Title { get; }

    protected int Dp(float v) => (int)Math.Round(v * DeviceDpi / 96f);
    protected int Pad => Dp(18);
    protected int HeaderHeight => Dp(28);

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // Pintado em OnPaint.
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.Clear(Parent?.BackColor ?? Theme.Bg);

        var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        Gfx.FillRound(g, Theme.Surface, rect, Dp(14));
        Gfx.StrokeRound(g, Theme.Border, rect, Dp(14));

        int d = Dp(24);
        var badge = new RectangleF(Pad, Pad + (HeaderHeight - d) / 2f, d, d);
        Gfx.Circle(g, Theme.Accent, badge);
        Gfx.Text(g, Number.ToString(), Theme.Semi(9f), Color.White, Rectangle.Round(badge), Gfx.Line | TextFormatFlags.HorizontalCenter);
        Gfx.Text(g, Title, Theme.Semi(11f), Theme.Text,
            new Rectangle(Pad + d + Dp(10), Pad, Math.Max(0, Width - Pad * 2 - d - Dp(10)), HeaderHeight));

        PaintContent(g);
    }

    /// <summary>Desenho adicional específico de cada cartão (textos, avisos).</summary>
    protected virtual void PaintContent(Graphics g) { }
}

/// <summary>Passo 1: arquivo de save.</summary>
internal sealed class SaveCard : SectionCard
{
    private readonly SaveDropZone _zone = new();

    public SaveCard() : base(1, "Arquivo de save")
    {
        _zone.BrowseRequested += (_, _) => BrowseRequested?.Invoke(this, EventArgs.Empty);
        Controls.Add(_zone);
    }

    public event EventHandler? BrowseRequested;

    public int PreferredHeight => Pad + HeaderHeight + Dp(12) + Dp(76) + Pad;

    public string? FilePath
    {
        get => _zone.FilePath;
        set => _zone.FilePath = value;
    }

    public string? ErrorText { get => _zone.ErrorText; set => _zone.ErrorText = value; }
    public bool DragHighlight { get => _zone.DragHighlight; set => _zone.DragHighlight = value; }
    public bool BrowseEnabled { get => _zone.BrowseEnabled; set => _zone.BrowseEnabled = value; }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        _zone.SetBounds(Pad, Pad + HeaderHeight + Dp(12), Math.Max(0, Width - Pad * 2), Dp(76));
    }
}
