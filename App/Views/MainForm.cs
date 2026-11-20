using Ps2SaveImporter.Core;
using Ps2SaveImporter.UI;

namespace Ps2SaveImporter.Views;

/// <summary>Janela principal do PS2 Save Importer.</summary>
internal sealed class MainForm : Form
{
    private readonly AppSettings _settings;
    private readonly CardCatalog _catalog;
    private readonly MyMcRunner _mymc;
    private readonly Func<bool> _isPcsx2Running;
    private readonly ImportService _service;

    private readonly PagePanel _page = new();
    private readonly HeaderBar _header = new();
    private readonly SaveCard _save = new();
    private readonly CardPickerCard _picker;
    private readonly ActionCard _action = new();
    private readonly FooterBar _footer = new();
    private readonly System.Windows.Forms.Timer _pcsxTimer = new() { Interval = 2500 };

    private string? _savePath;
    private bool _busy;
    private bool _pcsx2Running;

    public MainForm() : this(null, null, null, null) { }

    /// <summary>Construtor com dependÃªncias opcionais (usado pelos testes automatizados).</summary>
    public MainForm(CardCatalog? catalog, AppSettings? settings, MyMcRunner? mymc, Func<bool>? pcsx2Running)
    {
        _settings = settings ?? AppSettings.Load();
        _catalog = catalog ?? new CardCatalog();
        _mymc = mymc ?? new MyMcRunner();
        _isPcsx2Running = pcsx2Running ?? Pcsx2Guard.IsRunning;
        _service = new ImportService(_mymc, _isPcsx2Running);
        _picker = new CardPickerCard(_catalog, _settings);

        AutoScaleMode = AutoScaleMode.None;
        Text = AppInfo.Name;
        Icon = AppIcon.Load();
        BackColor = Theme.Bg;
        DoubleBuffered = true;
        StartPosition = FormStartPosition.CenterScreen;
        int maxH = Screen.FromPoint(Cursor.Position).WorkingArea.Height - this.Dp(90);
        ClientSize = new Size(this.Dp(1040), Math.Min(this.Dp(780), Math.Max(this.Dp(560), maxH)));
        MinimumSize = new Size(this.Dp(640), this.Dp(540));
        AllowDrop = true;
        KeyPreview = true;

        _page.Dock = DockStyle.Fill;
        _page.Header = _header;
        _page.Save = _save;
        _page.Picker = _picker;
        _page.Action = _action;
        _page.Footer = _footer;
        _page.Controls.AddRange(new Control[] { _header, _save, _picker, _action, _footer });
        Controls.Add(_page);

        _save.BrowseRequested += (_, _) => BrowseSave();
        _picker.SelectedCardChanged += (_, _) => UpdateReady();
        _picker.SettingsChanged += (_, _) => _settings.Save();
        _action.ImportClicked += async (_, _) => await ImportAsync();
        _action.PreferredHeightChanged += (_, _) => _page.Relayout();
        _footer.AboutRequested += (_, _) => ShowAbout();

        _pcsx2Running = _isPcsx2Running();
        _action.SetPcsx2(_pcsx2Running);
        _pcsxTimer.Tick += async (_, _) => await PollPcsx2Async();
        _pcsxTimer.Start();

        UpdateReady();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Native.ApplyDarkTitleBar(Handle, Theme.Bg, Theme.Text, Theme.Border);
    }

    // ---------- Estado ----------

    private async Task PollPcsx2Async()
    {
        bool running = await Task.Run(_isPcsx2Running);
        if (running == _pcsx2Running) return;
        _pcsx2Running = running;
        _action.SetPcsx2(running);
        UpdateReady();
    }

    private void UpdateReady()
    {
        bool ready = false;
        string hint;
        if (_busy) hint = "Importação em andamento. Não feche o programa.";
        else if (!_mymc.Exists) hint = "Componente mymc.exe não encontrado junto do programa.";
        else if (_savePath is null) hint = "Escolha um arquivo de save para continuar.";
        else if (_picker.SelectedCard is null) hint = "Selecione um Memory Card de destino.";
        else if (_pcsx2Running) hint = "Feche o PCSX2 para liberar a importação.";
        else
        {
            ready = true;
            hint = "Um backup do cartão será criado antes de qualquer alteração.";
        }
        _action.SetReady(ready, hint);
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        _save.BrowseEnabled = !busy;
        _picker.SetBusy(busy);
        _action.SetBusy(busy);
        UpdateReady();
    }

    // ---------- Arquivo de save ----------

    private void BrowseSave()
    {
        using (var folder = new FolderBrowserDialog
        {
            Description = "Selecione a pasta do save do PCSX2 (ex.: BASLUS-21087). Cancele para escolher um arquivo MAX/PSU/CBS/SPS/PSV.",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false,
            InitialDirectory = Directory.Exists(_settings.LastSaveFolder) ? _settings.LastSaveFolder : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        })
        {
            if (folder.ShowDialog(this) == DialogResult.OK)
            {
                SetSave(folder.SelectedPath);
                if (_savePath is not null) return;
            }
        }

        using var dialog = new OpenFileDialog
        {
            Title = "Selecionar arquivo de save do PS2",
            Filter = SaveFormats.DialogFilter,
            CheckFileExists = true,
            RestoreDirectory = true,
        };
        if (!string.IsNullOrEmpty(_settings.LastSaveFolder) && Directory.Exists(_settings.LastSaveFolder))
            dialog.InitialDirectory = _settings.LastSaveFolder;
        if (dialog.ShowDialog(this) == DialogResult.OK) SetSave(dialog.FileName);
    }

    private void SetSave(string path)
    {
        _action.ResetProgress();
        if (!SaveFormats.IsSupportedInput(path))
        {
            _savePath = null;
            _save.FilePath = null;
            _save.ErrorText = $"“{Path.GetFileName(path)}”: pasta inválida ou formato não suportado. Use MAX, PSU, CBS, SPS, PSV ou uma pasta válida do PCSX2.";
            UpdateReady();
            return;
        }

        _savePath = path;
        _save.FilePath = path;
        _settings.LastSaveFolder = Path.GetDirectoryName(path);
        _settings.Save();
        UpdateReady();
    }

    // ---------- Importação ----------

    private async Task ImportAsync()
    {
        if (_busy || _savePath is null || _picker.SelectedCard is not { } card) return;
        string savePath = _savePath;

        var issue = _service.Validate(savePath, card.FullPath);
        if (issue is not null)
        {
            ModernDialog.Show(this, new DialogSpec(issue.Title, issue.Message, DialogTone.Danger, IconKind.Alert, "Entendi"));
            return;
        }

        var facts = new List<ResultFact>
        {
            new("Save", Path.GetFileName(savePath)),
            new("Formato", SaveFormats.Describe(savePath)),
            new("Cartão", card.FullPath),
        };
        var confirm = ModernDialog.Show(this, new DialogSpec(
            "Importar este save?",
            "O Memory Card original só será alterado depois que o save for importado e validado em uma cópia temporária.",
            DialogTone.Accent, IconKind.Import, "Importar", "Cancelar", facts,
            "Um backup do cartão é criado automaticamente. Se o save já existir no cartão, você escolhe se deseja substituí-lo."));
        if (confirm != DialogResult.OK) return;

        SetBusy(true);
        _action.ResetProgress();
        var progress = new Progress<StepUpdate>(_action.ApplyStep);

        ImportResult result;
        try
        {
            result = await _service.RunAsync(savePath, card.FullPath, progress, ConfirmReplaceAsync);
        }
        finally
        {
            SetBusy(false);
        }

        ShowResult(result, savePath, card);
        RevealResultSoon();
    }

    /// <summary>Se a janela for pequena, rola ate o resultado depois que ele termina de aparecer.</summary>
    private void RevealResultSoon()
    {
        var timer = new System.Windows.Forms.Timer { Interval = 380 };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            timer.Dispose();
            _page.ScrollToEnd();
        };
        timer.Start();
    }

    private Task<bool> ConfirmReplaceAsync(string folder)
    {
        var tcs = new TaskCompletionSource<bool>();
        BeginInvoke((Action)(() =>
        {
            var answer = ModernDialog.Show(this, new DialogSpec(
                "Substituir o save existente?",
                $"Já existe um save com a pasta “{folder}” neste Memory Card. Deseja substituir somente esse save pelo arquivo selecionado?",
                DialogTone.Danger, IconKind.Alert, "Substituir save", "Manter o existente",
                Note: "O restante dos jogos e saves será preservado. O save atual continua disponível no backup criado antes da importação.",
                PrimaryDanger: true));
            tcs.SetResult(answer == DialogResult.OK);
        }));
        return tcs.Task;
    }

    private void ShowResult(ImportResult result, string savePath, CardEntry card)
    {
        var facts = new List<ResultFact>
        {
            new("Save", Path.GetFileName(savePath)),
            new("Cartão", card.FileName),
        };
        if (result.BackupPath is not null) facts.Add(new ResultFact("Backup", result.BackupPath));

        switch (result.Outcome)
        {
            case ImportOutcome.Success:
                _action.SetSummary("Concluído");
                _action.ShowResult(ResultKind.Success, "Save importado com sucesso",
                    result.ReplacedFolder is null
                        ? "O save foi adicionado ao Memory Card. Abra o jogo no PCSX2 para continuar de onde parou."
                        : $"O save “{result.ReplacedFolder}” foi substituído no Memory Card. O anterior continua no backup.",
                    facts, result.BackupPath, null);
                break;

            case ImportOutcome.Cancelled:
                _action.SetSummary("Cancelado");
                _action.ShowResult(ResultKind.Info, "Importação cancelada",
                    "Nenhuma alteração foi feita no Memory Card original. O backup criado antes da tentativa foi mantido.",
                    facts, result.BackupPath, null);
                break;

            default:
                string[] lines = result.Message.Split(new[] { "\r\n" }, 2, StringSplitOptions.None);
                string headline = lines[0].TrimEnd(':', '.', ' ');
                string? details = lines.Length > 1 ? lines[1] : null;
                string tail = result.OriginalMayBeAffected
                    ? ". Se o cartão não abrir corretamente no PCSX2, restaure o backup indicado abaixo."
                    : ". O Memory Card original não foi alterado.";
                _action.ShowResult(ResultKind.Error, "A importação falhou", headline + tail, facts, result.BackupPath, details);
                break;
        }
    }

    // ---------- Sobre ----------

    private void ShowAbout()
    {
        ModernDialog.Show(this, new DialogSpec(
            $"{AppInfo.Name} {AppInfo.Version}",
            "Importe saves de PlayStation 2 (MAX, PSU, CBS, SPS, PSV e pastas de save do PCSX2) para Memory Cards, com backup automático.",
            DialogTone.Accent, IconKind.MemoryCard, "Fechar", "Abrir GitHub",
            new List<ResultFact>
            {
                new("Autor", AppInfo.Author),
                new("GitHub", AppInfo.AuthorHandle),
                new("Motor", "mymc (importação de saves)"),
            },
            AppInfo.Disclaimer,
            SecondaryAction: FooterBar.OpenGitHub));
    }

    // ---------- Arrastar e soltar ----------

    private static string[] DroppedFiles(DragEventArgs e) =>
        e.Data?.GetData(DataFormats.FileDrop) as string[] ?? Array.Empty<string>();

    private static bool IsCard(string path) => string.Equals(Path.GetExtension(path), ".ps2", StringComparison.OrdinalIgnoreCase);

    protected override void OnDragEnter(DragEventArgs e)
    {
        base.OnDragEnter(e);
        UpdateDrag(e);
    }

    protected override void OnDragOver(DragEventArgs e)
    {
        base.OnDragOver(e);
        UpdateDrag(e);
    }

    private void UpdateDrag(DragEventArgs e)
    {
        var files = DroppedFiles(e);
        bool hasCard = files.Any(IsCard);
        bool hasSave = files.Any(SaveFormats.IsSupportedInput);
        e.Effect = !_busy && files.Length > 0 ? DragDropEffects.Copy : DragDropEffects.None;
        _save.DragHighlight = !_busy && hasSave;
        _picker.DragHighlight = !_busy && hasCard;
    }

    protected override void OnDragLeave(EventArgs e)
    {
        base.OnDragLeave(e);
        _save.DragHighlight = false;
        _picker.DragHighlight = false;
    }

    protected override void OnDragDrop(DragEventArgs e)
    {
        base.OnDragDrop(e);
        _save.DragHighlight = false;
        _picker.DragHighlight = false;
        if (_busy) return;

        var files = DroppedFiles(e);
        var cards = files.Where(IsCard).ToList();
        var saves = files.Where(SaveFormats.IsSupportedInput).ToList();

        if (cards.Count > 0) _picker.AddManualCards(cards);
        if (saves.Count > 0) SetSave(saves[0]);
        else if (cards.Count == 0 && files.Length > 0) SetSave(files[0]); // mostra o aviso de formato não suportado
    }

    // ---------- Teclado e fechamento ----------

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (!_busy)
        {
            if (keyData == (Keys.Control | Keys.O)) { BrowseSave(); return true; }
            if (keyData == Keys.F5) { _picker.RefreshAutomatic(announce: true); return true; }
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_busy)
        {
            e.Cancel = true;
            ModernDialog.Show(this, new DialogSpec(
                "Importação em andamento",
                "Aguarde a conclusão para fechar o programa. Isso protege o seu Memory Card contra alterações incompletas.",
                DialogTone.Accent, IconKind.Shield, "Entendi"));
            return;
        }

        _picker.SyncSettings();
        _settings.Save();
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _pcsxTimer.Dispose();
        base.Dispose(disposing);
    }
}
