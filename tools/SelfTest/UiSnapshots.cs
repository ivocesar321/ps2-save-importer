using System.Drawing.Imaging;
using System.Reflection;
using Ps2SaveImporter.Core;
using Ps2SaveImporter.UI;
using Ps2SaveImporter.Views;

namespace SelfTest;

/// <summary>Renderiza a interface em vários estados e tamanhos e grava PNGs para inspeção visual.</summary>
internal static class UiSnapshots
{
    private static T Get<T>(object o, string field) =>
        (T)o.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(o)!;

    private static void Pump(int ms)
    {
        long end = Environment.TickCount64 + ms;
        while (Environment.TickCount64 < end)
        {
            Application.DoEvents();
            Thread.Sleep(8);
        }
    }

    private static void SavePng(Control c, string path, Rectangle area)
    {
        using var bmp = new Bitmap(area.Width, area.Height);
        c.DrawToBitmap(bmp, new Rectangle(0, 0, c.Width, c.Height));
        bmp.Save(path, ImageFormat.Png);
        Console.WriteLine("Imagem: " + path);
    }

    private static void SnapPage(MainForm f, string path)
    {
        var page = Get<PagePanel>(f, "_page");
        using var bmp = new Bitmap(page.Width, page.Height);
        page.DrawToBitmap(bmp, new Rectangle(0, 0, page.Width, page.Height));
        bmp.Save(path, ImageFormat.Png);
        Console.WriteLine($"Imagem: {path} ({page.Width}x{page.Height}, rolagem={page.ScrollVisible})");
    }

    private static MainForm Create(CardCatalog cat, AppSettings set, MyMcRunner mymc, Func<bool> pcsx, Size client)
    {
        var f = new MainForm(cat, set, mymc, pcsx)
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-12000, 50),
            ShowInTaskbar = false,
        };
        f.ClientSize = client;
        f.Show();
        Pump(350);
        return f;
    }

    private static void Invoke(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)!.Invoke(target, args);

    private static void SnapDialog(DialogSpec spec, string path)
    {
        var d = (Form)Activator.CreateInstance(typeof(ModernDialog), BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] { spec }, null)!;
        d.StartPosition = FormStartPosition.Manual;
        d.Location = new Point(-12000, 50);
        d.ShowInTaskbar = false;
        d.Show();
        Pump(500);
        using var bmp = new Bitmap(d.Width, d.Height);
        d.DrawToBitmap(bmp, new Rectangle(0, 0, d.Width, d.Height));
        // DrawToBitmap inclui a moldura; recorta a área do cliente.
        var frame = d.PointToScreen(Point.Empty);
        bmp.Save(path, ImageFormat.Png);
        Console.WriteLine($"Imagem: {path} (cliente {d.ClientSize.Width}x{d.ClientSize.Height}, janela {d.Width}x{d.Height}, origem {frame})");
        d.Close();
    }

    public static int Run(string mymcPath, string work, string outDir)
    {
        Directory.CreateDirectory(outDir);
        string uiWork = Path.Combine(work, "ui");
        if (Directory.Exists(uiWork)) Directory.Delete(uiWork, true);
        string autoDir = Path.Combine(uiWork, "memcards");
        Directory.CreateDirectory(autoDir);

        // Cartões de demonstração (cópias): nunca os originais.
        string realDir = CardCatalog.DefaultDirectory;
        var real = Directory.Exists(realDir) ? Directory.GetFiles(realDir, "*.ps2").OrderBy(p => p).ToList() : new List<string>();
        string blank = Path.Combine(work, "blank.ps2");
        File.Copy(real.Count > 0 ? real[0] : blank, Path.Combine(autoDir, "Mcd001.ps2"));
        File.Copy(real.Count > 1 ? real[1] : blank, Path.Combine(autoDir, "Mcd002.ps2"));
        var rnd = new byte[30000];
        new Random(5).NextBytes(rnd);
        File.WriteAllBytes(Path.Combine(autoDir, "Quebrado.ps2"), rnd);
        string otherDir = Path.Combine(uiWork, "Jogos", "Wrestling");
        Directory.CreateDirectory(otherDir);
        string manual = Path.Combine(otherDir, "WWE Cartao Principal.ps2");
        File.Copy(blank, manual);

        string psu = Path.Combine(work, "BESLES-TESTSAVE.psu");
        var mymc = new MyMcRunner(mymcPath);
        var settings = AppSettings.Load(Path.Combine(uiWork, "settings.json"));
        var catalog = new CardCatalog(autoDir);

        // ---------- Janela principal ----------
        var f = Create(catalog, settings, mymc, () => false, new Size(1040, 700));
        SnapPage(f, Path.Combine(outDir, "01-inicial.png"));

        Invoke(f, "SetSave", psu);
        Pump(200);
        SnapPage(f, Path.Combine(outDir, "02-pronto.png"));

        var picker = Get<CardPickerCard>(f, "_picker");
        picker.AddManualCards(new[] { manual });
        Pump(500);
        SnapPage(f, Path.Combine(outDir, "03-manual.png"));

        var action = Get<ActionCard>(f, "_action");
        action.SetBusy(true);
        action.ResetProgress();
        action.ApplyStep(new StepUpdate(ImportStep.Preflight, StepState.Done));
        action.ApplyStep(new StepUpdate(ImportStep.Backup, StepState.Done, "Mcd001.ps2.backup-20261119-091500"));
        action.ApplyStep(new StepUpdate(ImportStep.TempCopy, StepState.Done));
        action.ApplyStep(new StepUpdate(ImportStep.Import, StepState.Running, "Importando na cópia…"));
        Pump(700);
        SnapPage(f, Path.Combine(outDir, "04-importando.png"));

        action.SetBusy(false);
        action.ApplyStep(new StepUpdate(ImportStep.Import, StepState.Done));
        action.ApplyStep(new StepUpdate(ImportStep.Validate, StepState.Done));
        action.ApplyStep(new StepUpdate(ImportStep.Apply, StepState.Done));
        action.SetSummary("Concluído");
        string backup = Path.Combine(autoDir, "Mcd001.ps2.backup-20261119-091500");
        action.ShowResult(ResultKind.Success, "Save importado com sucesso",
            "O save foi adicionado ao Memory Card. Abra o jogo no PCSX2 para continuar de onde parou.",
            new[] { new ResultFact("Save", "BESLES-TESTSAVE.psu"), new ResultFact("Cartão", "Mcd001.ps2"), new ResultFact("Backup", backup) },
            backup, null);
        Pump(900);
        SnapPage(f, Path.Combine(outDir, "05-sucesso.png"));

        action.ResetProgress();
        action.ApplyStep(new StepUpdate(ImportStep.Preflight, StepState.Done));
        action.ApplyStep(new StepUpdate(ImportStep.Backup, StepState.Done, "Mcd001.ps2.backup-20261119-091500"));
        action.ApplyStep(new StepUpdate(ImportStep.TempCopy, StepState.Done));
        action.ApplyStep(new StepUpdate(ImportStep.Import, StepState.Failed));
        action.ShowResult(ResultKind.Error, "A importação falhou",
            "Não foi possível importar este arquivo. O Memory Card original não foi alterado.",
            new[] { new ResultFact("Save", "corrompido.psu"), new ResultFact("Cartão", "Mcd001.ps2"), new ResultFact("Backup", backup) },
            backup, "Traceback (most recent call last):\r\n  File \"mymc.py\", line 640, in main\r\nps2save.corrupt: Not a EMS (.psu) save file.");
        var result = Get<ResultPanel>(action, "_result");
        var toggle = Get<ModernButton>(result, "_toggle");
        typeof(Control).GetMethod("OnClick", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(toggle, new object[] { EventArgs.Empty });
        Pump(900);
        SnapPage(f, Path.Combine(outDir, "06-erro.png"));

        Get<PagePanel>(f, "_page").ScrollToEnd();
        Pump(300);
        SnapPage(f, Path.Combine(outDir, "06b-erro-fim.png"));

        f.ClientSize = new Size(740, 1330);
        Pump(500);
        SnapPage(f, Path.Combine(outDir, "07-estreita.png"));

        f.ClientSize = new Size(1560, 820);
        Pump(500);
        SnapPage(f, Path.Combine(outDir, "08-larga.png"));

        f.ClientSize = new Size(640, 560);
        Pump(500);
        SnapPage(f, Path.Combine(outDir, "09-minima.png"));
        f.Close();

        // ---------- PCSX2 aberto ----------
        var f2 = Create(new CardCatalog(autoDir), AppSettings.Load(Path.Combine(uiWork, "s2.json")), mymc, () => true, new Size(1040, 700));
        Invoke(f2, "SetSave", psu);
        Pump(300);
        SnapPage(f2, Path.Combine(outDir, "10-pcsx2-aberto.png"));
        f2.Close();

        // ---------- Diálogos ----------
        SnapDialog(new DialogSpec("Importar este save?",
            "O Memory Card original só será alterado depois que o save for importado e validado em uma cópia temporária.",
            DialogTone.Accent, IconKind.Import, "Importar", "Cancelar",
            new[] { new ResultFact("Save", "BESLES-TESTSAVE.psu"), new ResultFact("Formato", "PSU (EMS)"), new ResultFact("Cartão", Path.Combine(autoDir, "Mcd001.ps2")) },
            "Um backup do cartão é criado automaticamente. Se o save já existir no cartão, você escolhe se deseja substituí-lo."),
            Path.Combine(outDir, "11-dialogo-confirmar.png"));

        SnapDialog(new DialogSpec("Substituir o save existente?",
            "Já existe um save chamado “BESLES-TESTSAVE” neste Memory Card. Deseja substituí-lo pelo save que você está importando?",
            DialogTone.Danger, IconKind.Alert, "Substituir save", "Manter o existente",
            Note: "O save atual continua disponível no backup criado antes da importação.", PrimaryDanger: true),
            Path.Combine(outDir, "12-dialogo-substituir.png"));

        SnapDialog(new DialogSpec($"{AppInfo.Name} {AppInfo.Version}",
            "Importe saves de PlayStation 2 (MAX, PSU, CBS, SPS e PSV) para os Memory Cards do PCSX2, com backup automático.",
            DialogTone.Accent, IconKind.MemoryCard, "Fechar", "Abrir GitHub",
            new[] { new ResultFact("Autor", AppInfo.Author), new ResultFact("GitHub", AppInfo.AuthorHandle), new ResultFact("Motor", "mymc (importação de saves)") },
            AppInfo.Disclaimer, SecondaryAction: () => { }),
            Path.Combine(outDir, "13-dialogo-sobre.png"));

        return 0;
    }
}
