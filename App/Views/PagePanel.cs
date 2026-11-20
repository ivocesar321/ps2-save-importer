using Ps2SaveImporter.UI;

namespace Ps2SaveImporter.Views;

/// <summary>
/// Área rolável da janela. Layout responsivo: em janelas largas usa duas colunas
/// (entradas à esquerda, importação e resultado à direita); em janelas estreitas empilha tudo.
/// A rolagem vertical é própria (barra escura) e só aparece quando o conteúdo não cabe.
/// </summary>
internal sealed class PagePanel : Panel
{
    private readonly VScrollBar _bar = new() { Dock = DockStyle.Right, Visible = false, TabStop = false };
    private int _scrollY;
    private bool _busyLayout;

    public HeaderBar? Header { get; set; }
    public SaveCard? Save { get; set; }
    public CardPickerCard? Picker { get; set; }
    public ActionCard? Action { get; set; }
    public FooterBar? Footer { get; set; }

    public PagePanel()
    {
        DoubleBuffered = true;
        BackColor = Theme.Bg;
        SetStyle(ControlStyles.ResizeRedraw, true);
        Controls.Add(_bar);
        _bar.ValueChanged += (_, _) =>
        {
            if (_bar.Value == _scrollY) return;
            _scrollY = _bar.Value;
            Relayout();
        };
        _bar.HandleCreated += (_, _) => Native.ApplyDarkScrollbars(_bar.Handle);
    }

    private int Dp(float v) => (int)Math.Round(v * DeviceDpi / 96f);

    public bool ScrollVisible => _bar.Visible;

    /// <summary>Rola até o fim do conteúdo (usado em testes e ao mostrar o resultado).</summary>
    public void ScrollToEnd()
    {
        if (_bar.Visible) _bar.Value = Math.Max(_bar.Minimum, _bar.Maximum - _bar.LargeChange + 1);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (!_bar.Visible) return;
        int max = _bar.Maximum - _bar.LargeChange + 1;
        _bar.Value = Math.Clamp(_bar.Value - e.Delta * Dp(64) / 120, _bar.Minimum, Math.Max(_bar.Minimum, max));
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        Relayout();
    }

    public void Relayout()
    {
        if (_busyLayout || Header is null || Save is null || Picker is null || Action is null || Footer is null) return;
        _busyLayout = true;
        try
        {
            int viewH = ClientSize.Height;
            int viewW = ClientSize.Width;

            // 1) mede sem a barra; 2) se não couber, mede de novo já descontando a barra.
            int contentH = LayoutPass(viewW, viewH, apply: false);
            bool needBar = contentH > viewH;
            if (needBar) contentH = LayoutPass(Math.Max(Dp(300), viewW - SystemInformation.VerticalScrollBarWidth), viewH, apply: false);
            needBar = contentH > viewH;

            if (_bar.Visible != needBar) _bar.Visible = needBar;
            if (needBar)
            {
                _bar.SmallChange = Dp(40);
                _bar.LargeChange = Math.Max(1, viewH);
                _bar.Maximum = Math.Max(0, contentH - 1);
                _scrollY = Math.Clamp(_scrollY, 0, Math.Max(0, contentH - viewH));
                if (_bar.Value != _scrollY) _bar.Value = _scrollY;
                viewW -= _bar.Width;
            }
            else
            {
                _scrollY = 0;
            }

            LayoutPass(viewW, viewH, apply: true);
        }
        finally
        {
            _busyLayout = false;
        }
    }

    /// <summary>Calcula (e opcionalmente aplica) as posições; devolve a altura total do conteúdo.</summary>
    private int LayoutPass(int viewW, int viewH, bool apply)
    {
        int margin = Dp(24), gap = Dp(16);
        int contentW = Math.Max(Dp(300), Math.Min(viewW - margin * 2, Dp(1160)));
        int x0 = Math.Max(margin, (viewW - contentW) / 2);
        int y = margin;

        void Place(Control c, int x, int top, int w, int h)
        {
            if (apply) c.SetBounds(x, top - _scrollY, w, h);
        }

        int hh = Header!.GetPreferredHeight(contentW);
        Place(Header, x0, y, contentW, hh);
        y += hh + gap + Dp(6);

        int fh = Footer!.GetPreferredHeight(contentW);
        int columnsBottom;
        if (contentW >= Dp(900))
        {
            int leftW = (int)((contentW - gap) * 0.55);
            int rightW = contentW - gap - leftW;
            int saveH = Save!.PreferredHeight;
            int leftMin = saveH + gap + Picker!.PreferredHeight;
            // As colunas ocupam a altura disponivel da janela (a lista de cartoes cresce junto).
            int avail = viewH - y - gap - fh - margin;
            int colH = Math.Max(Math.Max(leftMin, Action!.PreferredHeight(rightW)), avail);

            Place(Save, x0, y, leftW, saveH);
            Place(Picker, x0, y + saveH + gap, leftW, colH - saveH - gap);
            Place(Action, x0 + leftW + gap, y, rightW, colH);
            columnsBottom = y + colH;
        }
        else
        {
            int saveH = Save!.PreferredHeight, pickH = Picker!.PreferredHeight, actH = Action!.PreferredHeight(contentW);
            Place(Save, x0, y, contentW, saveH);
            Place(Picker, x0, y + saveH + gap, contentW, pickH);
            Place(Action, x0, y + saveH + gap + pickH + gap, contentW, actH);
            columnsBottom = y + saveH + gap + pickH + gap + actH;
        }

        y = columnsBottom + gap;
        // Rodape ancorado na base da janela quando sobra espaco.
        Place(Footer!, x0, Math.Max(y, viewH - fh - margin), contentW, fh);
        return y + fh + margin;
    }
}
