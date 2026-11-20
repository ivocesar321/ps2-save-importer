using System.Drawing.Drawing2D;

namespace Ps2SaveImporter.UI;

internal enum ButtonKind { Primary, Secondary, Ghost, Danger }

/// <summary>Botão arredondado com animação de hover/pressionado, ícone vetorial e foco por teclado.</summary>
internal sealed class ModernButton : ModernControl
{
    private readonly Animator _hover;
    private readonly Animator _press;
    private ButtonKind _kind = ButtonKind.Secondary;
    private IconKind? _icon;
    private bool _large;

    public ModernButton()
    {
        TabStop = true;
        SetStyle(ControlStyles.Selectable, true);
        Cursor = Cursors.Hand;
        AccessibleRole = AccessibleRole.PushButton;
        _hover = new Animator(Invalidate);
        _press = new Animator(Invalidate) { Speed = 0.4f };
    }

    public ButtonKind Kind { get => _kind; set { _kind = value; Invalidate(); } }
    public IconKind? Icon { get => _icon; set { _icon = value; Invalidate(); } }
    public bool Large { get => _large; set { _large = value; Invalidate(); } }

    [System.Diagnostics.CodeAnalysis.AllowNull]
    public override string Text
    {
        get => base.Text;
        set { base.Text = value; Invalidate(); }
    }

    private Font TextFont => _large ? Theme.Semi(11f) : Theme.Semi(9.25f);

    public override Size GetPreferredSize(Size proposedSize)
    {
        int iconW = _icon is null ? 0 : Dp(_large ? 20 : 18);
        int gap = _icon is not null && !string.IsNullOrEmpty(Text) ? Dp(8) : 0;
        int tw = string.IsNullOrEmpty(Text)
            ? 0
            : TextRenderer.MeasureText(Text, TextFont, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width;
        return new Size(iconW + gap + tw + Dp(_large ? 44 : 30), Dp(_large ? 48 : 36));
    }

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); if (Enabled) _hover.To(1); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover.To(0); _press.To(0); }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left && Enabled) _press.To(1);
    }

    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); _press.To(0); }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        if (!Enabled) { _hover.Jump(0); _press.Jump(0); }
        Cursor = Enabled ? Cursors.Hand : Cursors.Default;
        Invalidate();
    }

    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode is Keys.Space or Keys.Enter)
        {
            e.Handled = true;
            if (Enabled) OnClick(EventArgs.Empty);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Smooth(g);
        g.Clear(BackColor);

        float h = _hover.Value, p = _press.Value;
        var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        float radius = Dp(_large ? 12 : 9);

        Color fill, fore, border = Color.Transparent;
        switch (_kind)
        {
            case ButtonKind.Primary:
                fill = Enabled
                    ? Gfx.Blend(Gfx.Blend(Theme.Accent, Theme.AccentHover, h), Theme.AccentPressed, p)
                    : Theme.Button;
                fore = Enabled ? Color.White : Theme.TextMuted;
                break;
            case ButtonKind.Danger:
                fill = Enabled
                    ? Gfx.Blend(Gfx.Blend(Theme.Danger, Color.FromArgb(241, 108, 100), h), Color.FromArgb(196, 62, 55), p)
                    : Theme.Button;
                fore = Enabled ? Color.White : Theme.TextMuted;
                break;
            case ButtonKind.Ghost:
                fill = Gfx.Blend(BackColor, Theme.Hover, Enabled ? Math.Max(h, p) : 0);
                fore = Enabled ? Gfx.Blend(Theme.TextSecondary, Theme.Text, h) : Theme.TextMuted;
                break;
            default:
                fill = Enabled ? Gfx.Blend(Gfx.Blend(Theme.Button, Theme.ButtonHover, h), Theme.Surface, p * 0.5f) : Theme.Inset;
                fore = Enabled ? Theme.Text : Theme.TextMuted;
                border = Enabled ? Gfx.Blend(Theme.Border, Theme.BorderStrong, h) : Theme.Border;
                break;
        }

        Gfx.FillRound(g, fill, rect, radius);
        if (border.A > 0) Gfx.StrokeRound(g, border, rect, radius);

        if (Focused && ShowFocusCues)
            Gfx.StrokeRound(g, Theme.AccentHover, RectangleF.Inflate(rect, -1.5f, -1.5f), radius - 1f, 2f);

        // Conteúdo: ícone + texto centralizados.
        var font = TextFont;
        int iconW = _icon is null ? 0 : Dp(_large ? 20 : 18);
        int gap = _icon is not null && !string.IsNullOrEmpty(Text) ? Dp(8) : 0;
        int textW = string.IsNullOrEmpty(Text) ? 0 : Gfx.TextWidth(g, Text, font);
        int total = iconW + gap + textW;
        int x = Math.Max(Dp(8), (Width - total) / 2);

        if (_icon is not null)
            Icons.Draw(g, _icon.Value, new RectangleF(x, (Height - iconW) / 2f, iconW, iconW), fore);

        if (textW > 0)
        {
            int tx = x + iconW + gap;
            Gfx.Text(g, Text, font, fore, new Rectangle(tx, 0, Math.Max(0, Width - tx - Dp(8)), Height));
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _hover.Dispose(); _press.Dispose(); }
        base.Dispose(disposing);
    }
}
