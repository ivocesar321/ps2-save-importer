using Ps2SaveImporter.Core;

namespace Ps2SaveImporter.UI;

/// <summary>Lista de Memory Cards de destino, desenhada como linhas selecionáveis (rolagem, teclado, dica de erro).</summary>
internal sealed class CardListControl : ModernControl
{
    private readonly List<CardEntry> _items = new();
    private readonly DarkToolTip _tip = new();
    private int _selected = -1;
    private int _hover = -1;
    private int _hoverRemove = -1;
    private int _scroll;
    private string _tipText = string.Empty;

    public bool ShowRemove { get; set; }
    public string EmptyTitle { get; set; } = "Nenhum cartão";
    public string EmptyHint { get; set; } = string.Empty;

    public event EventHandler? SelectionChanged;
    public event Action<CardEntry>? RemoveRequested;

    public CardListControl()
    {
        TabStop = true;
        SetStyle(ControlStyles.Selectable, true);
        BackColor = Theme.Surface;
        AccessibleRole = AccessibleRole.List;
    }

    public CardEntry? SelectedEntry => _selected >= 0 && _selected < _items.Count ? _items[_selected] : null;
    public int Count => _items.Count;

    private int RowH => Dp(58);
    private int Gap => Dp(6);
    private int ContentHeight => _items.Count == 0 ? 0 : _items.Count * (RowH + Gap) - Gap;
    private bool NeedScroll => ContentHeight > Height;
    private int MaxScroll => Math.Max(0, ContentHeight - Height);

    /// <summary>Substitui os itens. Mantém a seleção em <paramref name="preferredPath"/> ou escolhe o primeiro cartão válido.</summary>
    public void SetItems(IEnumerable<CardEntry> items, string? preferredPath)
    {
        _items.Clear();
        _items.AddRange(items);
        _selected = -1;
        if (preferredPath is not null)
            _selected = _items.FindIndex(c => c.IsValid && string.Equals(c.FullPath, preferredPath, StringComparison.OrdinalIgnoreCase));
        if (_selected < 0)
            _selected = _items.FindIndex(c => c.IsValid);
        _scroll = Math.Clamp(_scroll, 0, MaxScroll);
        _hover = _hoverRemove = -1;
        if (_selected >= 0) EnsureVisible(_selected);
        Invalidate();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Select(CardEntry entry)
    {
        int i = _items.IndexOf(entry);
        if (i >= 0 && _items[i].IsValid) SetSelected(i);
    }

    private void SetSelected(int index)
    {
        if (index == _selected) return;
        _selected = index;
        EnsureVisible(index);
        Invalidate();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void EnsureVisible(int index)
    {
        int top = index * (RowH + Gap);
        int bottom = top + RowH;
        if (top < _scroll) _scroll = top;
        else if (bottom > _scroll + Height) _scroll = bottom - Height;
        _scroll = Math.Clamp(_scroll, 0, MaxScroll);
    }

    private Rectangle RowRect(int i)
    {
        int w = Width - (NeedScroll ? Dp(10) : 0);
        return new Rectangle(0, i * (RowH + Gap) - _scroll, w, RowH);
    }

    private Rectangle RemoveRect(int i)
    {
        var r = RowRect(i);
        int s = Dp(26);
        return new Rectangle(r.Right - Dp(12) - s, r.Y + (r.Height - s) / 2, s, s);
    }

    private int IndexAt(Point p)
    {
        int idx = (p.Y + _scroll) / (RowH + Gap);
        if (p.Y + _scroll < 0 || idx < 0 || idx >= _items.Count) return -1;
        return RowRect(idx).Contains(p) ? idx : -1;
    }

    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Up or Keys.Down or Keys.Home or Keys.End || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_items.Count == 0) return;
        int dir = e.KeyCode switch { Keys.Down => 1, Keys.Up => -1, _ => 0 };
        if (e.KeyCode == Keys.Home) { MoveTo(0, 1); e.Handled = true; }
        else if (e.KeyCode == Keys.End) { MoveTo(_items.Count - 1, -1); e.Handled = true; }
        else if (dir != 0) { MoveTo(_selected + dir, dir); e.Handled = true; }
    }

    private void MoveTo(int start, int step)
    {
        for (int i = start; i >= 0 && i < _items.Count; i += step)
        {
            if (_items[i].IsValid) { SetSelected(i); return; }
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int idx = IndexAt(e.Location);
        int rem = idx >= 0 && ShowRemove && RemoveRect(idx).Contains(e.Location) ? idx : -1;
        string tip = idx >= 0 && !_items[idx].IsValid ? _items[idx].Validation.Message : string.Empty;

        if (idx != _hover || rem != _hoverRemove)
        {
            _hover = idx;
            _hoverRemove = rem;
            Cursor = idx >= 0 && (_items[idx].IsValid || rem >= 0) ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }
        if (tip != _tipText)
        {
            _tipText = tip;
            _tip.SetToolTip(this, tip);
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = _hoverRemove = -1;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        Focus();
        int idx = IndexAt(e.Location);
        if (idx < 0) return;
        if (ShowRemove && RemoveRect(idx).Contains(e.Location))
        {
            RemoveRequested?.Invoke(_items[idx]);
            return;
        }
        if (_items[idx].IsValid) SetSelected(idx);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (!NeedScroll) return;
        _scroll = Math.Clamp(_scroll - e.Delta * Dp(48) / 120, 0, MaxScroll);
        Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        _scroll = Math.Clamp(_scroll, 0, MaxScroll);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Smooth(g);
        g.Clear(BackColor);

        if (_items.Count == 0)
        {
            DrawEmpty(g);
            return;
        }

        for (int i = 0; i < _items.Count; i++)
        {
            var r = RowRect(i);
            if (r.Bottom < 0 || r.Top > Height) continue;
            DrawRow(g, i, r);
        }

        if (!Enabled)
        {
            // Lista bloqueada durante a importação: escurece levemente.
            using var veil = new SolidBrush(Color.FromArgb(110, Theme.Surface));
            g.FillRectangle(veil, ClientRectangle);
        }

        if (NeedScroll)
        {
            float ratio = (float)Height / ContentHeight;
            int th = Math.Max(Dp(28), (int)(Height * ratio));
            int ty = (int)((Height - th) * (_scroll / (float)MaxScroll));
            Gfx.FillRound(g, Theme.BorderStrong, new RectangleF(Width - Dp(4), ty, Dp(3), th), Dp(2));
        }
    }

    private void DrawEmpty(Graphics g)
    {
        var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        Gfx.FillRound(g, Theme.Inset, rect, Dp(12));
        Gfx.StrokeRound(g, Theme.Border, rect, Dp(12));

        int icon = Dp(30);
        int cy = Height / 2 - Dp(26);
        Icons.Draw(g, IconKind.MemoryCard, new RectangleF((Width - icon) / 2f, cy, icon, icon), Theme.TextMuted);
        Gfx.Text(g, EmptyTitle, Theme.Semi(10f), Theme.TextSecondary,
            new Rectangle(Dp(16), cy + icon + Dp(6), Width - Dp(32), Dp(20)), Gfx.Line | TextFormatFlags.HorizontalCenter);
        if (EmptyHint.Length > 0)
            Gfx.Text(g, EmptyHint, Theme.Regular(8.75f), Theme.TextMuted,
                new Rectangle(Dp(24), cy + icon + Dp(28), Width - Dp(48), Dp(36)),
                Gfx.Wrap | TextFormatFlags.HorizontalCenter);
    }

    private void DrawRow(Graphics g, int i, Rectangle r)
    {
        var it = _items[i];
        bool sel = i == _selected, hov = i == _hover;
        bool valid = it.IsValid;
        float rad = Dp(10);

        var rf = new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1f, r.Height - 1f);
        Color bg = sel ? Theme.AccentSoft : (hov && valid ? Theme.Hover : Theme.Inset);
        Color bd = sel ? Theme.Accent
            : valid ? (hov ? Theme.BorderStrong : Theme.Border)
            : Gfx.Blend(Theme.Border, Theme.Danger, 0.4f);
        Gfx.FillRound(g, bg, rf, rad);
        Gfx.StrokeRound(g, bd, rf, rad, sel ? 1.5f : 1f);

        // Ícone do cartão.
        int ts = Dp(36);
        var tile = new RectangleF(r.X + Dp(12), r.Y + (r.Height - ts) / 2f, ts, ts);
        Gfx.FillRound(g, sel ? Gfx.Blend(Theme.AccentSoft, Theme.Accent, 0.28f) : Theme.Button, tile, Dp(9));
        Icons.Draw(g, IconKind.MemoryCard, RectangleF.Inflate(tile, -Dp(8), -Dp(8)),
            valid ? (sel ? Theme.AccentHover : Theme.TextSecondary) : Theme.TextMuted);

        // Lado direito (da direita para a esquerda): remover, indicador, selo, tamanho.
        int right = r.Right - Dp(14);
        if (ShowRemove)
        {
            var rr = RemoveRect(i);
            if (i == _hoverRemove) Gfx.FillRound(g, Theme.ButtonHover, rr, Dp(7));
            Icons.Draw(g, IconKind.Cross, RectangleF.Inflate(rr, -Dp(7), -Dp(7)), i == _hoverRemove ? Theme.Text : Theme.TextMuted, 2f);
            right = rr.Left - Dp(8);
        }

        if (valid)
        {
            int d = Dp(20);
            var ind = new RectangleF(right - d, r.Y + (r.Height - d) / 2f, d, d);
            if (sel)
            {
                Gfx.Circle(g, Theme.Accent, ind);
                Icons.Draw(g, IconKind.Check, RectangleF.Inflate(ind, -Dp(4), -Dp(4)), Color.White, 2.4f);
            }
            else
            {
                using var pen = new Pen(Theme.BorderStrong, 1.5f);
                g.DrawEllipse(pen, RectangleF.Inflate(ind, -0.75f, -0.75f));
            }
            right -= d + Dp(12);
        }

        string badge = valid ? "Válido" : it.Validation.Badge;
        var bf = Theme.Semi(8f);
        int bw = Gfx.TextWidth(g, badge, bf) + Dp(18);
        var br = new RectangleF(right - bw, r.Y + (r.Height - Dp(20)) / 2f, bw, Dp(20));
        Gfx.FillRound(g, valid ? Theme.SuccessSoft : Theme.DangerSoft, br, Dp(10));
        Gfx.Text(g, badge, bf, valid ? Theme.Success : Theme.Danger, Rectangle.Round(br), Gfx.Line | TextFormatFlags.HorizontalCenter);
        right = (int)br.Left - Dp(12);

        string size = valid && it.Validation.CapacityMb > 0 ? it.Validation.CapacityMb + " MB" : string.Empty;
        if (size.Length > 0)
        {
            int sw = Gfx.TextWidth(g, size, Theme.Regular(8.75f));
            Gfx.Text(g, size, Theme.Regular(8.75f), Theme.TextMuted,
                new Rectangle(right - sw, r.Y, sw + 2, r.Height), Gfx.Line | TextFormatFlags.Right);
            right -= sw + Dp(12);
        }

        // Nome e pasta.
        int tx = (int)tile.Right + Dp(12);
        int tw = Math.Max(Dp(40), right - tx);
        Gfx.Text(g, it.FileName, Theme.Semi(10f), valid ? Theme.Text : Theme.TextMuted,
            new Rectangle(tx, r.Y + Dp(9), tw, Dp(20)));
        Gfx.Text(g, it.Folder, Theme.Regular(8.5f), Theme.TextMuted,
            new Rectangle(tx, r.Y + Dp(29), tw, Dp(18)), Gfx.PathLine);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _tip.Dispose();
        base.Dispose(disposing);
    }
}
