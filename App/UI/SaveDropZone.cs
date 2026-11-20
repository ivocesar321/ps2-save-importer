using System.Drawing.Drawing2D;
using Ps2SaveImporter.Core;

namespace Ps2SaveImporter.UI;

/// <summary>Área do arquivo de save: mostra o arquivo escolhido (ou o convite para arrastar/procurar).</summary>
internal sealed class SaveDropZone : ModernControl
{
    private readonly ModernButton _browse = new() { Text = "Procurar…", Icon = IconKind.Folder, Kind = ButtonKind.Secondary };
    private readonly Animator _hover;
    private string? _path;
    private string? _error;
    private bool _dragOver;

    public event EventHandler? BrowseRequested;

    public SaveDropZone()
    {
        BackColor = Theme.Surface;
        _browse.BackColor = Theme.Inset;
        _browse.Click += (_, _) => BrowseRequested?.Invoke(this, EventArgs.Empty);
        Controls.Add(_browse);
        _hover = new Animator(Invalidate);
        Cursor = Cursors.Hand;
    }

    public string? FilePath
    {
        get => _path;
        set { _path = value; _error = null; Invalidate(); }
    }

    public string? ErrorText
    {
        get => _error;
        set { _error = value; Invalidate(); }
    }

    public bool DragHighlight
    {
        get => _dragOver;
        set { if (_dragOver != value) { _dragOver = value; Invalidate(); } }
    }

    public bool BrowseEnabled
    {
        get => _browse.Enabled;
        set { _browse.Enabled = value; Cursor = value ? Cursors.Hand : Cursors.Default; }
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        var size = _browse.GetPreferredSize(Size.Empty);
        _browse.SetBounds(Width - size.Width - Dp(14), (Height - size.Height) / 2, size.Width, size.Height);
    }

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover.To(1); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover.To(0); }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        if (_browse.Enabled) BrowseRequested?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Smooth(g);
        g.Clear(BackColor);

        var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        float radius = Dp(12);
        float h = _browse.Enabled ? _hover.Value : 0f;

        Gfx.FillRound(g, _dragOver ? Theme.AccentSoft : Theme.Inset, rect, radius);

        Color borderColor = _dragOver ? Theme.Accent
            : _error is not null ? Gfx.Blend(Theme.Border, Theme.Danger, 0.6f)
            : Gfx.Blend(Theme.BorderStrong, Theme.Accent, h * 0.7f);
        using (var pen = new Pen(borderColor, _dragOver ? 1.6f : 1.2f))
        using (var path = Gfx.RoundRect(rect, radius))
        {
            if (_path is null) pen.DashPattern = new[] { 4f, 4f };
            g.DrawPath(pen, path);
        }

        int ts = Dp(44);
        var tile = new RectangleF(Dp(14), (Height - ts) / 2f, ts, ts);
        int textX = (int)tile.Right + Dp(14);
        int textW = Math.Max(Dp(40), _browse.Left - Dp(14) - textX);
        var titleRect = new Rectangle(textX, Height / 2 - Dp(20), textW, Dp(22));
        var metaRect = new Rectangle(textX, Height / 2 + Dp(2), textW, Dp(20));

        if (_path is null)
        {
            Gfx.FillRound(g, Theme.Button, tile, Dp(11));
            Icons.Draw(g, IconKind.Import, RectangleF.Inflate(tile, -Dp(11), -Dp(11)), _dragOver ? Theme.AccentHover : Theme.TextSecondary);
            Gfx.Text(g, _dragOver ? "Solte o arquivo para selecionar" : "Arraste um save aqui ou clique em Procurar",
                Theme.Semi(10f), Theme.Text, titleRect);
            Gfx.Text(g, _error ?? "MAX · PSU · CBS · SPS · PSV · pastas PCSX2",
                Theme.Regular(8.75f), _error is null ? Theme.TextMuted : Theme.Danger, metaRect);
        }
        else
        {
            Gfx.FillRound(g, Theme.AccentSoft, tile, Dp(11));
            Gfx.StrokeRound(g, Gfx.Blend(Theme.AccentSoft, Theme.Accent, 0.45f), RectangleF.Inflate(tile, -0.5f, -0.5f), Dp(11));
            Icons.Draw(g, IconKind.File, RectangleF.Inflate(tile, -Dp(12), -Dp(12)), Theme.AccentHover);

            string meta;
            try
            {
                long len = new FileInfo(_path).Length;
                meta = $"{Path.GetExtension(_path).TrimStart('.').ToUpperInvariant()} · {Fmt.Size(len)} · {SaveFormats.Describe(_path)}";
            }
            catch
            {
                meta = SaveFormats.Describe(_path);
            }

            Gfx.Text(g, Path.GetFileName(_path), Theme.Semi(10f), Theme.Text, titleRect);
            Gfx.Text(g, _error ?? meta, Theme.Regular(8.75f), _error is null ? Theme.TextMuted : Theme.Danger, metaRect);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _hover.Dispose();
        base.Dispose(disposing);
    }
}
