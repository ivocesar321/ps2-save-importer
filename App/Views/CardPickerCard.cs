using Ps2SaveImporter.Core;
using Ps2SaveImporter.UI;

namespace Ps2SaveImporter.Views;

internal enum NoticeKind { Info, Success, Error }

/// <summary>Passo 2: Memory Card de destino — detecção automática ou seleção manual.</summary>
internal sealed class CardPickerCard : SectionCard
{
    private readonly CardCatalog _catalog;
    private readonly AppSettings _settings;
    private readonly SegmentedControl _mode = new() { Items = new[] { "Automático", "Manual" } };
    private readonly CardListControl _list = new();
    private readonly ModernButton _browse = new() { Text = "Procurar cartão…", Icon = IconKind.Search, Kind = ButtonKind.Secondary };
    private readonly ModernButton _refresh = new() { Text = "Atualizar", Icon = IconKind.Refresh, Kind = ButtonKind.Ghost };
    private readonly System.Windows.Forms.Timer _noticeTimer = new() { Interval = 6500 };

    private string? _autoError;
    private string? _selectedAuto;
    private string? _selectedManual;
    private string? _noticeText;
    private NoticeKind _noticeKind;
    private bool _dragHighlight;
    private bool _applying;

    public event EventHandler? SelectedCardChanged;
    public event EventHandler? SettingsChanged;

    public CardPickerCard(CardCatalog catalog, AppSettings settings) : base(2, "Memory Card de destino")
    {
        _catalog = catalog;
        _settings = settings;

        _autoError = _catalog.RefreshAutomatic();
        foreach (var path in settings.ManualCards.ToList())
            _catalog.AddManual(path); // cartões que sumiram ou ficaram inválidos são ignorados

        _mode.SetSelectedSilently(settings.ManualMode ? 1 : 0);
        _mode.SelectedIndexChanged += (_, _) => { ApplyMode(); NotifySettings(); };
        _list.SelectionChanged += (_, _) => OnListSelectionChanged();
        _list.RemoveRequested += RemoveManual;
        _browse.Click += (_, _) => BrowseForCards();
        _refresh.Click += (_, _) => RefreshAutomatic(announce: true);
        _noticeTimer.Tick += (_, _) => { _noticeTimer.Stop(); _noticeText = null; Invalidate(); };

        Controls.AddRange(new Control[] { _mode, _list, _browse, _refresh });
        ApplyMode();
    }

    public bool ManualMode => _mode.SelectedIndex == 1;
    public CardEntry? SelectedCard => _list.SelectedEntry;

    public int PreferredHeight =>
        Pad + HeaderHeight + Dp(6) + Dp(18) + Dp(10) + Dp(3 * 58 + 2 * 6) + Dp(10) + Dp(36) + Dp(8) + Dp(16) + Pad;

    public bool DragHighlight
    {
        get => _dragHighlight;
        set { if (_dragHighlight != value) { _dragHighlight = value; Invalidate(); } }
    }

    public void SetBusy(bool busy)
    {
        _mode.Enabled = !busy;
        _list.Enabled = !busy;
        _browse.Enabled = !busy;
        _refresh.Enabled = !busy;
    }

    public void SyncSettings()
    {
        _settings.ManualMode = ManualMode;
        _settings.ManualCards = _catalog.Manual.Select(c => c.FullPath).ToList();
    }

    private void NotifySettings()
    {
        SyncSettings();
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    // ---------- Modo e lista ----------

    private void ApplyMode()
    {
        _applying = true;
        try
        {
            bool manual = ManualMode;
            _list.ShowRemove = manual;
            _list.EmptyTitle = manual ? "Nenhum cartão adicionado" : "Nenhum Memory Card encontrado";
            _list.EmptyHint = manual
                ? "Use “Procurar cartão…” para escolher um arquivo .ps2 de qualquer pasta."
                : $"Coloque seus cartões .ps2 em {_catalog.AutomaticDirectory} ou use “Procurar cartão…”.";
            _list.SetItems(manual ? _catalog.Manual : _catalog.Automatic, manual ? _selectedManual : _selectedAuto);
            string? chosen = _list.SelectedEntry?.FullPath;
            if (manual) _selectedManual = chosen; else _selectedAuto = chosen;
            _refresh.Visible = !manual;
        }
        finally
        {
            _applying = false;
        }
        PerformLayout();
        Invalidate();
        SelectedCardChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnListSelectionChanged()
    {
        if (_applying) return;
        var path = _list.SelectedEntry?.FullPath;
        if (ManualMode) _selectedManual = path; else _selectedAuto = path;
        SelectedCardChanged?.Invoke(this, EventArgs.Empty);
    }

    // ---------- Detecção automática ----------

    public void RefreshAutomatic(bool announce = false)
    {
        _autoError = _catalog.RefreshAutomatic();
        if (!ManualMode) ApplyMode();
        else Invalidate();

        if (!announce) return;
        if (_autoError is not null) ShowNotice(NoticeKind.Error, "Não foi possível ler a pasta de cartões.");
        else
        {
            int n = _catalog.Automatic.Count;
            ShowNotice(NoticeKind.Info, n == 0 ? "Nenhum cartão encontrado na pasta do PCSX2." : $"{n} {(n == 1 ? "cartão detectado" : "cartões detectados")}.");
        }
    }

    // ---------- Seleção manual ----------

    public void BrowseForCards()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Selecionar Memory Card do PS2",
            Filter = "Memory Card do PS2 (*.ps2)|*.ps2|Todos os arquivos (*.*)|*.*",
            Multiselect = true,
            CheckFileExists = true,
            RestoreDirectory = true,
        };

        string? start = _settings.LastCardFolder;
        if (string.IsNullOrEmpty(start) || !Directory.Exists(start)) start = _catalog.AutomaticDirectory;
        if (Directory.Exists(start)) dialog.InitialDirectory = start;

        if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;
        _settings.LastCardFolder = Path.GetDirectoryName(dialog.FileNames[0]);
        AddManualCards(dialog.FileNames);
    }

    /// <summary>Adiciona cartões escolhidos (diálogo ou arrastar e soltar), sem duplicar e validando cada arquivo.</summary>
    public void AddManualCards(IEnumerable<string> paths)
    {
        int added = 0, invalid = 0;
        CardEntry? lastAdded = null, existing = null;
        string? lastMessage = null;
        bool existingIsAuto = false;

        foreach (var p in paths)
        {
            var r = _catalog.AddManual(p);
            lastMessage = r.Message;
            switch (r.Outcome)
            {
                case AddOutcome.Added: added++; lastAdded = r.Entry; break;
                case AddOutcome.AlreadyAutomatic: existing = r.Entry; existingIsAuto = true; break;
                case AddOutcome.AlreadyManual: existing = r.Entry; existingIsAuto = false; break;
                default: invalid++; lastMessage = $"{Path.GetFileName(p)}: {r.Message}"; break;
            }
        }

        if (added > 0 && lastAdded is not null)
        {
            _selectedManual = lastAdded.FullPath;
            if (!ManualMode) _mode.SelectedIndex = 1; else ApplyMode();
            ShowNotice(NoticeKind.Success, added == 1 ? "Cartão adicionado e selecionado." : $"{added} cartões adicionados.");
            NotifySettings();
        }
        else if (existing is not null)
        {
            if (existingIsAuto)
            {
                _selectedAuto = existing.FullPath;
                if (ManualMode) _mode.SelectedIndex = 0; else ApplyMode();
            }
            else
            {
                _selectedManual = existing.FullPath;
                if (!ManualMode) _mode.SelectedIndex = 1; else ApplyMode();
            }
            ShowNotice(NoticeKind.Info, lastMessage ?? "Este cartão já está na lista.");
        }
        else if (invalid > 0)
        {
            ShowNotice(NoticeKind.Error, invalid == 1 ? lastMessage! : $"{invalid} arquivos não são Memory Cards válidos.");
        }
    }

    private void RemoveManual(CardEntry entry)
    {
        if (!_catalog.RemoveManual(entry)) return;
        if (string.Equals(_selectedManual, entry.FullPath, StringComparison.OrdinalIgnoreCase)) _selectedManual = null;
        ApplyMode();
        ShowNotice(NoticeKind.Info, "Cartão removido da lista (o arquivo não foi apagado).");
        NotifySettings();
    }

    // ---------- Aviso e desenho ----------

    public void ShowNotice(NoticeKind kind, string text)
    {
        _noticeKind = kind;
        _noticeText = text;
        _noticeTimer.Stop();
        _noticeTimer.Start();
        Invalidate();
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        int w = Math.Max(0, Width - Pad * 2);

        var segSize = _mode.GetPreferredSize(Size.Empty);
        _mode.SetBounds(Width - Pad - segSize.Width, Pad - Dp(3), segSize.Width, segSize.Height);

        int subtitleBottom = Pad + HeaderHeight + Dp(6) + Dp(18);
        int noticeTop = Height - Pad - Dp(16);
        int buttonsTop = noticeTop - Dp(8) - Dp(36);

        int listTop = subtitleBottom + Dp(10);
        _list.SetBounds(Pad, listTop, w, Math.Max(Dp(80), buttonsTop - Dp(10) - listTop));

        int bx = Pad;
        var bs = _browse.GetPreferredSize(Size.Empty);
        _browse.SetBounds(bx, buttonsTop, bs.Width, Dp(36));
        bx += bs.Width + Dp(8);
        if (_refresh.Visible)
        {
            var rs = _refresh.GetPreferredSize(Size.Empty);
            _refresh.SetBounds(bx, buttonsTop, rs.Width, Dp(36));
        }
    }

    protected override void PaintContent(Graphics g)
    {
        // Subtítulo.
        string subtitle;
        Color subColor = Theme.TextMuted;
        if (ManualMode) subtitle = "Cartões que você adicionou (arquivos .ps2 válidos).";
        else if (_autoError is not null) { subtitle = "Não foi possível ler a pasta: " + _autoError; subColor = Theme.Danger; }
        else subtitle = "Pasta padrão do PCSX2: " + _catalog.AutomaticDirectory;
        Gfx.Text(g, subtitle, Theme.Regular(8.75f), subColor,
            new Rectangle(Pad, Pad + HeaderHeight + Dp(6), Width - Pad * 2, Dp(18)), Gfx.PathLine);

        if (_dragHighlight)
        {
            using var pen = new Pen(Theme.Accent, 1.8f) { DashPattern = new[] { 4f, 4f } };
            var r = new RectangleF(_list.Left - 3, _list.Top - 3, _list.Width + 6, _list.Height + 6);
            using var path = Gfx.RoundRect(r, Dp(12));
            g.DrawPath(pen, path);
        }

        // Aviso temporário.
        int noticeTop = Height - Pad - Dp(16);
        if (_noticeText is not null)
        {
            (IconKind icon, Color color) = _noticeKind switch
            {
                NoticeKind.Success => (IconKind.CheckCircle, Theme.Success),
                NoticeKind.Error => (IconKind.XCircle, Theme.Danger),
                _ => (IconKind.Info, Theme.TextSecondary),
            };
            Icons.Draw(g, icon, new RectangleF(Pad, noticeTop, Dp(16), Dp(16)), color, 1.9f);
            Gfx.Text(g, _noticeText, Theme.Regular(8.75f), color,
                new Rectangle(Pad + Dp(24), noticeTop - Dp(1), Width - Pad * 2 - Dp(24), Dp(18)));
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _noticeTimer.Dispose();
        base.Dispose(disposing);
    }
}
