using Ps2SaveImporter.Core;
using Ps2SaveImporter.UI;

namespace Ps2SaveImporter.Views;

/// <summary>Passo 3: importar, acompanhar as etapas e ver o resultado (com o backup criado).</summary>
internal sealed class ActionCard : SectionCard
{
    private readonly StatusChip _chip = new() { Label = "PCSX2 fechado", Tone = ChipTone.Success };
    private readonly ModernButton _import = new() { Text = "Importar save", Icon = IconKind.Import, Kind = ButtonKind.Primary, Large = true };
    private readonly StepProgress _progress = new(ImportService.StepTitles);
    private readonly ResultPanel _result = new();
    private readonly Animator _reveal;
    private string _hint = string.Empty;
    private bool _busy;
    private bool _ready;

    public event EventHandler? ImportClicked;
    public event EventHandler? PreferredHeightChanged;

    public ActionCard() : base(3, "Importar")
    {
        _import.Click += (_, _) => ImportClicked?.Invoke(this, EventArgs.Empty);
        _reveal = new Animator(OnRevealTick) { Speed = 0.3f };
        _result.PreferredHeightChanged += (_, _) => { PerformLayout(); PreferredHeightChanged?.Invoke(this, EventArgs.Empty); };
        Controls.AddRange(new Control[] { _chip, _import, _progress, _result });
        UpdateButton();
    }

    private int ResultHeight(int width) =>
        _result.Kind == ResultKind.None ? 0 : (int)(_result.GetPreferredHeight(width) * Ease(_reveal.Value));

    private static float Ease(float t) => 1f - (1f - t) * (1f - t);

    public int PreferredHeight(int width)
    {
        int inner = Math.Max(0, width - Pad * 2);
        int h = Pad + HeaderHeight + Dp(14) + Dp(48) + Dp(8) + Dp(18) + Dp(16) + _progress.PreferredHeight + Pad;
        int r = ResultHeight(inner);
        return r > 0 ? h + Dp(12) + r : h;
    }

    // ---------- Estado ----------

    public void SetPcsx2(bool running)
    {
        _chip.Label = running ? "PCSX2 em execução" : "PCSX2 fechado";
        _chip.Tone = running ? ChipTone.Danger : ChipTone.Success;
        PerformLayout();
    }

    public void SetReady(bool ready, string hint)
    {
        _ready = ready;
        _hint = hint;
        UpdateButton();
        Invalidate();
    }

    public void SetBusy(bool busy)
    {
        _busy = busy;
        UpdateButton();
        Invalidate();
    }

    private void UpdateButton()
    {
        _import.Enabled = _ready && !_busy;
        _import.Text = _busy ? "Importando…" : "Importar save";
    }

    // ---------- Progresso ----------

    public void ResetProgress()
    {
        _progress.Reset();
        _result.Clear();
        _reveal.Jump(0);
        PerformLayout();
        PreferredHeightChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ApplyStep(StepUpdate u)
    {
        _progress.Apply(u);
        int n = _progress.StepCount;
        _progress.Summary = u.State switch
        {
            StepState.Running => $"Etapa {(int)u.Step + 1} de {n}",
            StepState.Failed => $"Falha na etapa {(int)u.Step + 1}",
            _ => _progress.Summary,
        };
    }

    public void SetSummary(string text) => _progress.Summary = text;

    // ---------- Resultado ----------

    public void ShowResult(ResultKind kind, string title, string message, IEnumerable<ResultFact> facts, string? backup, string? details)
    {
        _result.Present(kind, title, message, facts, backup, details);
        _result.Visible = true;
        _reveal.Jump(0);
        _reveal.To(1);
        PerformLayout();
        PreferredHeightChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnRevealTick()
    {
        PerformLayout();
        PreferredHeightChanged?.Invoke(this, EventArgs.Empty);
    }

    // ---------- Layout e desenho ----------

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        int w = Math.Max(0, Width - Pad * 2);

        var cs = _chip.GetPreferredSize(Size.Empty);
        _chip.SetBounds(Width - Pad - cs.Width, Pad + (HeaderHeight - cs.Height) / 2, cs.Width, cs.Height);

        int y = Pad + HeaderHeight + Dp(14);
        _import.SetBounds(Pad, y, w, Dp(48));
        y += Dp(48) + Dp(8) + Dp(18) + Dp(16);

        _progress.SetBounds(Pad, y, w, _progress.PreferredHeight);
        y += _progress.PreferredHeight + Dp(12);

        int rh = ResultHeight(w);
        _result.Visible = rh > 0;
        _result.SetBounds(Pad, y, w, rh);
    }

    protected override void PaintContent(Graphics g)
    {
        int y = Pad + HeaderHeight + Dp(14) + Dp(48) + Dp(8);
        Gfx.Text(g, _hint, Theme.Regular(8.75f), _ready || _busy ? Theme.TextMuted : Theme.TextSecondary,
            new Rectangle(Pad, y, Width - Pad * 2, Dp(18)), Gfx.Line | TextFormatFlags.HorizontalCenter);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _reveal.Dispose();
        base.Dispose(disposing);
    }
}
