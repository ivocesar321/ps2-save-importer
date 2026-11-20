using System.Diagnostics;
using Ps2SaveImporter.Core;
using Ps2SaveImporter.UI;

namespace Ps2SaveImporter.Views;

/// <summary>Cabeçalho: logo, nome e breve descrição do programa.</summary>
internal sealed class HeaderBar : ModernControl
{
    public HeaderBar() => BackColor = Theme.Bg;

    private int Logo => Dp(56);

    public int GetPreferredHeight(int width)
    {
        int textW = Math.Max(Dp(120), width - Logo - Dp(18));
        int descH = TextRenderer.MeasureText(AppInfo.Tagline, Theme.Regular(9.5f), new Size(textW, int.MaxValue), Gfx.Wrap).Height;
        return Math.Max(Logo, Dp(32) + descH + Dp(4));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Smooth(g);
        g.Clear(BackColor);

        BrandIcon.Draw(g, new RectangleF(0, 0, Logo, Logo), true);

        int x = Logo + Dp(18);
        int w = Math.Max(Dp(120), Width - x);
        Gfx.Text(g, AppInfo.Name, Theme.Semi(19f), Theme.Text, new Rectangle(x, Dp(-2), w, Dp(34)));
        int descH = TextRenderer.MeasureText(AppInfo.Tagline, Theme.Regular(9.5f), new Size(w, int.MaxValue), Gfx.Wrap).Height;
        Gfx.Text(g, AppInfo.Tagline, Theme.Regular(9.5f), Theme.TextSecondary, new Rectangle(x, Dp(32), w, descH), Gfx.Wrap);
    }
}

/// <summary>Rodapé discreto: créditos da autoria, link do GitHub, "Sobre" e aviso de projeto independente.</summary>
internal sealed class FooterBar : ModernControl
{
    private static Font? _underline;
    private readonly List<(Rectangle Bounds, Action Click, string Id)> _links = new();
    private string? _hover;

    public event EventHandler? AboutRequested;

    public FooterBar()
    {
        BackColor = Theme.Bg;
        AccessibleRole = AccessibleRole.StaticText;
    }

    private static Font Underline => _underline ??= new Font(Theme.Regular(8.75f), FontStyle.Underline);

    private bool TwoLines(int width)
    {
        int left = TextRenderer.MeasureText(LeftText + "GitHub  ·  Sobre", Theme.Regular(8.75f), new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding).Width;
        int right = TextRenderer.MeasureText(AppInfo.Disclaimer, Theme.Regular(8.75f), new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding).Width;
        return left + right + Dp(40) > width;
    }

    private static string LeftText => $"{AppInfo.Name} v{AppInfo.Version}  ·  Projeto independente criado por {AppInfo.Author}  ·  ";

    public int GetPreferredHeight(int width) => TwoLines(width) ? Dp(44) : Dp(24);

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Smooth(g);
        g.Clear(BackColor);
        _links.Clear();

        var font = Theme.Regular(8.75f);
        bool two = TwoLines(Width);
        int lineH = Dp(20);
        int x = 0;

        void Plain(string text)
        {
            int w = Gfx.TextWidth(g, text, font);
            Gfx.Text(g, text, font, Theme.TextMuted, new Rectangle(x, 0, w + 2, lineH));
            x += w;
        }

        void Link(string text, string id, Action click)
        {
            bool hot = _hover == id;
            var f = hot ? Underline : font;
            int w = Gfx.TextWidth(g, text, f);
            var r = new Rectangle(x, 0, w + 2, lineH);
            Gfx.Text(g, text, f, hot ? Theme.AccentHover : Theme.TextSecondary, r);
            _links.Add((r, click, id));
            x += w;
        }

        Plain(LeftText);
        Link(AppInfo.AuthorHandle, "github", OpenGitHub);
        Plain("  ·  ");
        Link("Sobre", "about", () => AboutRequested?.Invoke(this, EventArgs.Empty));

        if (two)
            Gfx.Text(g, AppInfo.Disclaimer, font, Theme.TextMuted, new Rectangle(0, lineH, Width, lineH));
        else
            Gfx.Text(g, AppInfo.Disclaimer, font, Theme.TextMuted, new Rectangle(0, 0, Width, lineH), Gfx.Line | TextFormatFlags.Right);
    }

    public static void OpenGitHub()
    {
        try { Process.Start(new ProcessStartInfo(AppInfo.AuthorUrl) { UseShellExecute = true }); }
        catch { /* sem navegador padrão configurado */ }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        string? id = _links.FirstOrDefault(l => l.Bounds.Contains(e.Location)).Id;
        if (id != _hover)
        {
            _hover = id;
            Cursor = id is null ? Cursors.Default : Cursors.Hand;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hover is not null) { _hover = null; Cursor = Cursors.Default; Invalidate(); }
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        foreach (var l in _links)
            if (l.Bounds.Contains(e.Location)) { l.Click(); return; }
    }
}
