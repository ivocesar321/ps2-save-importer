namespace Ps2SaveImporter.UI;

/// <summary>Seletor segmentado com "pílula" deslizante (ex.: Automático | Manual).</summary>
internal sealed class SegmentedControl : ModernControl
{
    private readonly Animator _slide;
    private string[] _items = Array.Empty<string>();
    private int _selected;

    public event EventHandler? SelectedIndexChanged;

    public SegmentedControl()
    {
        TabStop = true;
        SetStyle(ControlStyles.Selectable, true);
        Cursor = Cursors.Hand;
        AccessibleRole = AccessibleRole.PageTabList;
        _slide = new Animator(Invalidate) { Speed = 0.28f };
    }

    public string[] Items
    {
        get => _items;
        set { _items = value; Invalidate(); }
    }

    public int SelectedIndex
    {
        get => _selected;
        set
        {
            if (value < 0 || value >= _items.Length || value == _selected) return;
            _selected = value;
            _slide.To(value);
            Invalidate();
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Define a seleção sem disparar o evento (uso na inicialização).</summary>
    public void SetSelectedSilently(int index)
    {
        _selected = Math.Clamp(index, 0, Math.Max(0, _items.Length - 1));
        _slide.Jump(_selected);
    }

    private int SegmentWidth
    {
        get
        {
            int max = 0;
            foreach (var s in _items)
                max = Math.Max(max, TextRenderer.MeasureText(s, Theme.Semi(9f), new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding).Width);
            return max + Dp(30);
        }
    }

    public override Size GetPreferredSize(Size proposedSize) =>
        new(SegmentWidth * Math.Max(1, _items.Length) + Dp(6), Dp(34));

    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Left or Keys.Right || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Left) { SelectedIndex = _selected - 1; e.Handled = true; }
        else if (e.KeyCode == Keys.Right) { SelectedIndex = _selected + 1; e.Handled = true; }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (_items.Length == 0 || e.Button != MouseButtons.Left) return;
        int pad = Dp(3);
        float seg = (Width - pad * 2f) / _items.Length;
        SelectedIndex = Math.Clamp((int)((e.X - pad) / seg), 0, _items.Length - 1);
    }

    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Smooth(g);
        g.Clear(BackColor);
        if (_items.Length == 0) return;

        var track = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        float radius = Dp(10);
        Gfx.FillRound(g, Theme.Bg, track, radius);
        Gfx.StrokeRound(g, Focused && ShowFocusCues ? Theme.AccentHover : Theme.Border, track, radius);

        int pad = Dp(3);
        float seg = (Width - pad * 2f) / _items.Length;
        var thumb = new RectangleF(pad + _slide.Value * seg, pad, seg, Height - pad * 2f);
        Gfx.FillRound(g, Theme.Accent, thumb, radius - Dp(3));

        for (int i = 0; i < _items.Length; i++)
        {
            float closeness = 1f - Math.Min(1f, Math.Abs(i - _slide.Value));
            var color = Gfx.Blend(Theme.TextSecondary, Color.White, closeness);
            var cell = new Rectangle((int)(pad + i * seg), 0, (int)seg, Height);
            Gfx.Text(g, _items[i], Theme.Semi(9f), color, cell, Gfx.Line | TextFormatFlags.HorizontalCenter);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _slide.Dispose();
        base.Dispose(disposing);
    }
}

internal enum ChipTone { Neutral, Success, Danger, Accent }

/// <summary>Pílula de estado com ponto colorido (ex.: "PCSX2 fechado").</summary>
internal sealed class StatusChip : ModernControl
{
    private string _text = string.Empty;
    private ChipTone _tone = ChipTone.Neutral;

    public string Label { get => _text; set { _text = value; Invalidate(); } }
    public ChipTone Tone { get => _tone; set { _tone = value; Invalidate(); } }

    public override Size GetPreferredSize(Size proposedSize)
    {
        int tw = TextRenderer.MeasureText(_text, Theme.Semi(8.5f), new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding).Width;
        return new Size(tw + Dp(36), Dp(26));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Smooth(g);
        g.Clear(BackColor);

        (Color bg, Color fg) = _tone switch
        {
            ChipTone.Success => (Theme.SuccessSoft, Theme.Success),
            ChipTone.Danger => (Theme.DangerSoft, Theme.Danger),
            ChipTone.Accent => (Theme.AccentSoft, Theme.AccentHover),
            _ => (Theme.Button, Theme.TextSecondary),
        };

        var rect = new RectangleF(0, 0, Width, Height);
        Gfx.FillRound(g, bg, rect, Height / 2f);

        float dot = Dp(7);
        Gfx.Circle(g, fg, new RectangleF(Dp(11), (Height - dot) / 2f, dot, dot));
        Gfx.Text(g, _text, Theme.Semi(8.5f), fg, new Rectangle(Dp(24), 0, Width - Dp(30), Height));
    }
}

/// <summary>Dica de ferramenta no tema escuro.</summary>
internal sealed class DarkToolTip : ToolTip
{
    private const int MaxWidth = 320;

    public DarkToolTip()
    {
        OwnerDraw = true;
        InitialDelay = 350;
        AutoPopDelay = 9000;
        Popup += (_, e) =>
        {
            string text = (e.AssociatedControl is null ? null : GetToolTip(e.AssociatedControl)) ?? string.Empty;
            var size = TextRenderer.MeasureText(text, Theme.Regular(8.75f), new Size(MaxWidth, 0), Gfx.Wrap);
            e.ToolTipSize = new Size(size.Width + 20, size.Height + 14);
        };
        Draw += (_, e) =>
        {
            var g = e.Graphics;
            var rect = new Rectangle(0, 0, e.Bounds.Width - 1, e.Bounds.Height - 1);
            using (var b = new SolidBrush(Theme.Button)) g.FillRectangle(b, e.Bounds);
            using (var p = new Pen(Theme.BorderStrong)) g.DrawRectangle(p, rect);
            TextRenderer.DrawText(g, e.ToolTipText, Theme.Regular(8.75f),
                new Rectangle(10, 7, e.Bounds.Width - 20, e.Bounds.Height - 14), Theme.Text, Gfx.Wrap);
        };
    }
}
