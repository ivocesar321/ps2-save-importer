using System.Drawing.Drawing2D;
using System.Reflection;
using Ps2SaveImporter.Core;

namespace Ps2SaveImporter.UI;

/// <summary>Ícone da janela, carregado do recurso embutido (funciona também em executável único).</summary>
internal static class AppIcon
{
    private static Icon? _icon;

    public static Icon? Load()
    {
        if (_icon is not null) return _icon;
        try
        {
            using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("AppIcon.ico");
            if (s is not null) _icon = new Icon(s);
        }
        catch
        {
            // Sem ícone, o Windows usa o padrão.
        }
        return _icon;
    }
}

internal enum DialogTone { Accent, Success, Danger }

internal sealed record DialogSpec(
    string Title,
    string Message,
    DialogTone Tone,
    IconKind Icon,
    string PrimaryText,
    string? SecondaryText = null,
    IReadOnlyList<ResultFact>? Facts = null,
    string? Note = null,
    Action? SecondaryAction = null,
    bool PrimaryDanger = false);

/// <summary>Caixa de diálogo moderna (substitui o MessageBox): ícone, título, mensagem, detalhes e botões.</summary>
internal sealed class ModernDialog : Form
{
    private readonly DialogSpec _spec;
    private readonly ModernButton _primary = new();
    private readonly ModernButton? _secondary;
    private readonly System.Windows.Forms.Timer _fade = new() { Interval = 15 };

    private Rectangle _title, _message, _facts, _note;
    private int _footerTop;

    public static DialogResult Show(IWin32Window? owner, DialogSpec spec)
    {
        using var dialog = new ModernDialog(spec);
        return owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
    }

    private ModernDialog(DialogSpec spec)
    {
        _spec = spec;
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Theme.Surface;
        DoubleBuffered = true;
        Text = AppInfo.Name;
        Icon = AppIcon.Load();

        _primary.Text = spec.PrimaryText;
        _primary.Kind = spec.PrimaryDanger ? ButtonKind.Danger : ButtonKind.Primary;
        _primary.BackColor = Theme.Bg;
        _primary.Click += (_, _) => { DialogResult = DialogResult.OK; Close(); };
        Controls.Add(_primary);

        if (spec.SecondaryText is not null)
        {
            _secondary = new ModernButton { Text = spec.SecondaryText, Kind = ButtonKind.Secondary, BackColor = Theme.Bg };
            _secondary.Click += (_, _) =>
            {
                if (spec.SecondaryAction is not null) { spec.SecondaryAction(); return; }
                DialogResult = DialogResult.Cancel;
                Close();
            };
            Controls.Add(_secondary);
        }

        _fade.Tick += (_, _) =>
        {
            Opacity = Math.Min(1.0, Opacity + 0.18);
            if (Opacity >= 1.0) _fade.Stop();
        };
        BuildLayout();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Native.ApplyDarkTitleBar(Handle, Theme.Surface, Theme.Text, Theme.Border);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _primary.Focus();
        if (SystemInformation.UIEffectsEnabled)
        {
            Opacity = 0.0;
            _fade.Start();
        }
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        BuildLayout();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            DialogResult = DialogResult.Cancel;
            Close();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void BuildLayout()
    {
        int width = this.Dp(500), pad = this.Dp(24), icon = this.Dp(44);
        int textX = pad + icon + this.Dp(16);
        int textW = width - textX - pad;

        int y = pad;
        _title = new Rectangle(textX, y, textW, this.Dp(30));
        y += this.Dp(32);

        int msgH = TextRenderer.MeasureText(_spec.Message, Theme.Regular(9.5f), new Size(textW, int.MaxValue), Gfx.Wrap).Height;
        _message = new Rectangle(textX, y, textW, msgH);
        y = Math.Max(y + msgH, pad + icon) + this.Dp(18);

        _facts = Rectangle.Empty;
        if (_spec.Facts is { Count: > 0 })
        {
            int rowH = this.Dp(26);
            _facts = new Rectangle(pad, y, width - pad * 2, rowH * _spec.Facts.Count + this.Dp(20));
            y += _facts.Height + this.Dp(14);
        }

        _note = Rectangle.Empty;
        if (!string.IsNullOrEmpty(_spec.Note))
        {
            int nh = TextRenderer.MeasureText(_spec.Note, Theme.Regular(8.75f), new Size(width - pad * 2 - this.Dp(24), int.MaxValue), Gfx.Wrap).Height;
            _note = new Rectangle(pad, y, width - pad * 2, nh);
            y += nh + this.Dp(14);
        }

        y += this.Dp(6);
        _footerTop = y;
        int footerH = this.Dp(72);
        ClientSize = new Size(width, y + footerH);

        int bh = this.Dp(38);
        int by = _footerTop + (footerH - bh) / 2;
        var ps = _primary.GetPreferredSize(Size.Empty);
        int pw = Math.Max(ps.Width, this.Dp(110));
        _primary.SetBounds(width - pad - pw, by, pw, bh);
        if (_secondary is not null)
        {
            var ss = _secondary.GetPreferredSize(Size.Empty);
            int sw = Math.Max(ss.Width, this.Dp(110));
            _secondary.SetBounds(_primary.Left - this.Dp(8) - sw, by, sw, bh);
        }
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        Color tone = _spec.Tone switch
        {
            DialogTone.Success => Theme.Success,
            DialogTone.Danger => Theme.Danger,
            _ => Theme.Accent,
        };
        Color soft = _spec.Tone switch
        {
            DialogTone.Success => Theme.SuccessSoft,
            DialogTone.Danger => Theme.DangerSoft,
            _ => Theme.AccentSoft,
        };

        int pad = this.Dp(24), icon = this.Dp(44);
        var badge = new RectangleF(pad, pad, icon, icon);
        Gfx.Circle(g, soft, badge);
        Icons.Draw(g, _spec.Icon, RectangleF.Inflate(badge, -this.Dp(11), -this.Dp(11)),
            _spec.Tone == DialogTone.Accent ? Theme.AccentHover : tone, 2f);

        Gfx.Text(g, _spec.Title, Theme.Semi(13f), Theme.Text, _title);
        Gfx.Text(g, _spec.Message, Theme.Regular(9.5f), Theme.TextSecondary, _message, Gfx.Wrap);

        if (!_facts.IsEmpty && _spec.Facts is not null)
        {
            Gfx.FillRound(g, Theme.Inset, _facts, this.Dp(10));
            Gfx.StrokeRound(g, Theme.Border, new RectangleF(_facts.X + 0.5f, _facts.Y + 0.5f, _facts.Width - 1f, _facts.Height - 1f), this.Dp(10));
            int rowH = this.Dp(26);
            int y = _facts.Y + this.Dp(10);
            foreach (var f in _spec.Facts)
            {
                int labelW = this.Dp(78);
                Gfx.Text(g, f.Label, Theme.Regular(9f), Theme.TextMuted, new Rectangle(_facts.X + this.Dp(14), y, labelW, rowH));
                Gfx.Text(g, f.Value, Theme.Regular(9f), Theme.Text,
                    new Rectangle(_facts.X + this.Dp(14) + labelW, y, _facts.Width - this.Dp(28) - labelW, rowH), Gfx.PathLine);
                y += rowH;
            }
        }

        if (!_note.IsEmpty)
        {
            Icons.Draw(g, IconKind.Shield, new RectangleF(_note.X, _note.Y - this.Dp(1), this.Dp(16), this.Dp(16)), Theme.TextMuted, 1.9f);
            Gfx.Text(g, _spec.Note!, Theme.Regular(8.75f), Theme.TextMuted,
                new Rectangle(_note.X + this.Dp(24), _note.Y, _note.Width - this.Dp(24), _note.Height), Gfx.Wrap);
        }

        // Rodapé.
        using (var b = new SolidBrush(Theme.Bg))
            g.FillRectangle(b, 0, _footerTop, ClientSize.Width, ClientSize.Height - _footerTop);
        using var pen = new Pen(Theme.Border);
        g.DrawLine(pen, 0, _footerTop, ClientSize.Width, _footerTop);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _fade.Dispose();
        base.Dispose(disposing);
    }
}
