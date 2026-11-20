using System.Diagnostics;

namespace Ps2SaveImporter.UI;

internal enum ResultKind { None, Success, Error, Info }

internal sealed record ResultFact(string Label, string Value);

/// <summary>Resultado da operação: sucesso, erro ou aviso, com arquivo, cartão, backup e detalhes técnicos.</summary>
internal sealed class ResultPanel : ModernControl
{
    private readonly ModernButton _showBackup = new() { Text = "Mostrar backup", Icon = IconKind.Folder, Kind = ButtonKind.Secondary };
    private readonly ModernButton _copy = new() { Text = "Copiar caminho", Icon = IconKind.Copy, Kind = ButtonKind.Ghost };
    private readonly ModernButton _toggle = new() { Text = "Ver detalhes", Kind = ButtonKind.Ghost };
    private readonly TextBox _detailsBox = new()
    {
        Multiline = true,
        ReadOnly = true,
        BorderStyle = BorderStyle.None,
        ScrollBars = ScrollBars.Vertical,
        BackColor = Theme.Inset,
        ForeColor = Theme.TextSecondary,
        Font = Theme.Mono(8.75f),
        Visible = false,
        TabStop = false,
    };

    private ResultKind _kind = ResultKind.None;
    private string _title = string.Empty;
    private string _message = string.Empty;
    private List<ResultFact> _facts = new();
    private string? _backupPath;
    private string? _details;
    private bool _showDetails;

    public event EventHandler? PreferredHeightChanged;

    public ResultPanel()
    {
        _showBackup.Click += (_, _) => RevealBackup();
        _copy.Click += (_, _) => CopyBackupPath();
        _toggle.Click += (_, _) =>
        {
            _showDetails = !_showDetails;
            _toggle.Text = _showDetails ? "Ocultar detalhes" : "Ver detalhes";
            PerformLayout();
            Invalidate();
            PreferredHeightChanged?.Invoke(this, EventArgs.Empty);
        };
        Controls.AddRange(new Control[] { _showBackup, _copy, _toggle, _detailsBox });
        _detailsBox.HandleCreated += (_, _) => Native.ApplyDarkScrollbars(_detailsBox.Handle);
        Visible = false;
    }

    public ResultKind Kind => _kind;

    public void Present(ResultKind kind, string title, string message, IEnumerable<ResultFact> facts, string? backupPath, string? details)
    {
        _kind = kind;
        _title = title;
        _message = message;
        _facts = facts.ToList();
        _backupPath = backupPath;
        _details = string.IsNullOrWhiteSpace(details) ? null : details.Trim();
        _showDetails = false;
        _toggle.Text = "Ver detalhes";
        _detailsBox.Text = _details ?? string.Empty;
        _showBackup.Visible = _copy.Visible = backupPath is not null;
        _toggle.Visible = _details is not null;
        PerformLayout();
        Invalidate();
        PreferredHeightChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        _kind = ResultKind.None;
        Visible = false;
    }

    private static void RevealBackup_Internal(string path)
    {
        try
        {
            if (File.Exists(path))
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
            else if (Path.GetDirectoryName(path) is { } dir && Directory.Exists(dir))
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
        }
        catch
        {
            // Abrir o Explorer é apenas uma conveniência.
        }
    }

    private void RevealBackup()
    {
        if (_backupPath is not null) RevealBackup_Internal(_backupPath);
    }

    private void CopyBackupPath()
    {
        if (_backupPath is null) return;
        try
        {
            Clipboard.SetText(_backupPath);
            _copy.Text = "Copiado";
            var t = new System.Windows.Forms.Timer { Interval = 1600 };
            t.Tick += (_, _) => { _copy.Text = "Copiar caminho"; t.Stop(); t.Dispose(); };
            t.Start();
        }
        catch
        {
            // A área de transferência pode estar em uso por outro programa.
        }
    }

    private readonly record struct Metrics(
        int Height, Rectangle Title, Rectangle Message, Rectangle[] Facts, Rectangle[] Buttons, Rectangle Details);

    private List<ModernButton> ActiveButtons()
    {
        var list = new List<ModernButton>();
        if (_backupPath is not null) { list.Add(_showBackup); list.Add(_copy); }
        if (_details is not null) list.Add(_toggle);
        return list;
    }

    private Metrics Measure(int width)
    {
        int pad = Dp(16), icon = Dp(30);
        int textX = pad + icon + Dp(12);
        int textW = Math.Max(Dp(80), width - textX - pad);

        int y = pad;
        var title = new Rectangle(textX, y, textW, Dp(24));
        y += Dp(24);

        int msgH = string.IsNullOrEmpty(_message)
            ? 0
            : TextRenderer.MeasureText(_message, Theme.Regular(9f), new Size(textW, int.MaxValue), Gfx.Wrap).Height;
        var message = new Rectangle(textX, y + Dp(2), textW, msgH);
        y += msgH + Dp(10);

        var facts = new Rectangle[_facts.Count];
        for (int i = 0; i < facts.Length; i++)
        {
            facts[i] = new Rectangle(textX, y, textW, Dp(20));
            y += Dp(20);
        }
        if (facts.Length > 0) y += Dp(6);

        // Botoes: quebram de linha quando nao cabem na largura do painel.
        var actives = ActiveButtons();
        var buttons = new Rectangle[actives.Count];
        int startX = textX - Dp(10), bx = startX, by = y, bh = Dp(34);
        for (int i = 0; i < actives.Count; i++)
        {
            int bw = actives[i].GetPreferredSize(Size.Empty).Width;
            if (bx > startX && bx + bw > width - pad + Dp(10)) { bx = startX; by += bh + Dp(4); }
            buttons[i] = new Rectangle(bx, by, bw, bh);
            bx += bw + Dp(6);
        }
        if (actives.Count > 0) y = by + bh + Dp(2);

        var details = Rectangle.Empty;
        if (_showDetails && _details is not null)
        {
            y += Dp(8);
            details = new Rectangle(pad, y, width - pad * 2, Dp(120));
            y += details.Height;
        }

        return new Metrics(y + pad, title, message, facts, buttons, details);
    }

    public int GetPreferredHeight(int width) => _kind == ResultKind.None ? 0 : Measure(width).Height;

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        if (_kind == ResultKind.None || Width < 50) return;

        var m = Measure(Width);
        var actives = ActiveButtons();
        for (int i = 0; i < actives.Count; i++)
        {
            actives[i].BackColor = Tint;
            actives[i].SetBounds(m.Buttons[i].X, m.Buttons[i].Y, m.Buttons[i].Width, m.Buttons[i].Height);
        }

        _detailsBox.Visible = _showDetails && _details is not null;
        if (_detailsBox.Visible)
            _detailsBox.SetBounds(m.Details.X + Dp(10), m.Details.Y + Dp(8), m.Details.Width - Dp(20), m.Details.Height - Dp(16));
    }

    private Color Tint => _kind switch
    {
        ResultKind.Success => Theme.SuccessSoft,
        ResultKind.Error => Theme.DangerSoft,
        _ => Theme.Inset,
    };

    private Color Tone => _kind switch
    {
        ResultKind.Success => Theme.Success,
        ResultKind.Error => Theme.Danger,
        _ => Theme.AccentHover,
    };


    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Smooth(g);
        g.Clear(Theme.Surface);
        if (_kind == ResultKind.None) return;

        var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        Gfx.FillRound(g, Tint, rect, Dp(12));
        Gfx.StrokeRound(g, Gfx.Blend(Tint, Tone, 0.45f), rect, Dp(12));

        var m = Measure(Width);
        int pad = Dp(16), icon = Dp(30);
        var badge = new RectangleF(pad, pad - Dp(1), icon, icon);
        Gfx.Circle(g, Tone, badge);
        var glyph = _kind switch
        {
            ResultKind.Success => IconKind.Check,
            ResultKind.Error => IconKind.Cross,
            _ => IconKind.Info,
        };
        Icons.Draw(g, glyph, RectangleF.Inflate(badge, -Dp(8), -Dp(8)), _kind == ResultKind.Info ? Theme.Bg : Color.White, 2.5f);

        Gfx.Text(g, _title, Theme.Semi(11f), _kind == ResultKind.Info ? Theme.Text : Tone, m.Title);
        if (m.Message.Height > 0)
            Gfx.Text(g, _message, Theme.Regular(9f), Theme.TextSecondary, m.Message, Gfx.Wrap);

        for (int i = 0; i < m.Facts.Length; i++)
        {
            var r = m.Facts[i];
            int labelW = Dp(58);
            Gfx.Text(g, _facts[i].Label, Theme.Regular(8.75f), Theme.TextMuted, new Rectangle(r.X, r.Y, labelW, r.Height));
            Gfx.Text(g, _facts[i].Value, Theme.Regular(8.75f), Theme.Text,
                new Rectangle(r.X + labelW, r.Y, r.Width - labelW, r.Height), Gfx.PathLine);
        }

        if (!m.Details.IsEmpty)
        {
            Gfx.FillRound(g, Theme.Inset, m.Details, Dp(8));
            Gfx.StrokeRound(g, Theme.Border, new RectangleF(m.Details.X + 0.5f, m.Details.Y + 0.5f, m.Details.Width - 1f, m.Details.Height - 1f), Dp(8));
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        PerformLayout();
    }
}
