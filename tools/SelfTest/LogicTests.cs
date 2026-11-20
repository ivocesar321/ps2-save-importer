using System.Security.Cryptography;
using Ps2SaveImporter.Core;
using Ps2SaveImporter.Views;

namespace SelfTest;

/// <summary>Testes de lógica: validação, lista de destinos, importação/backup e proteção do PCSX2.</summary>
internal static class LogicTests
{
    private static int _pass, _fail;

    private sealed class ListProgress : IProgress<StepUpdate>
    {
        public List<StepUpdate> Items { get; } = new();
        public void Report(StepUpdate value) => Items.Add(value);
    }

    private static void Check(string name, bool ok, string? info = null)
    {
        if (ok) _pass++; else _fail++;
        Console.WriteLine($"{(ok ? "[PASS]" : "[FAIL]")} {name}{(info is null ? "" : "  -> " + info)}");
    }

    private static void RunSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() => { try { action(); } catch (Exception e) { error = e; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error is not null) throw new InvalidOperationException("Falha no teste de interface: " + error.Message, error);
    }

    private static string Sha(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private static string[] TempLeftovers() =>
        Directory.GetFiles(Path.GetTempPath(), "PS2SaveImport-*.ps2");

    private static string[] Backups(string card) =>
        Directory.GetFiles(Path.GetDirectoryName(card)!, Path.GetFileName(card) + ".backup-*");

    private static byte[] Random(int n, int seed)
    {
        var b = new byte[n];
        new System.Random(seed).NextBytes(b);
        return b;
    }

    private static HashSet<string> Lines(string output) =>
        output.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToHashSet();

    public static async Task<int> Run(string mymcPath, string work)
    {
        if (Directory.Exists(work)) Directory.Delete(work, true);
        Directory.CreateDirectory(work);

        // Fotografia dos cartões REAIS do usuário: nada pode mudar neles durante os testes.
        string realDir = CardCatalog.DefaultDirectory;
        var realBefore = Directory.Exists(realDir)
            ? Directory.GetFiles(realDir).OrderBy(f => f).Select(f => (f, new FileInfo(f).Length, Sha(f))).ToList()
            : new List<(string, long, string)>();
        Console.WriteLine($"Cartões reais monitorados (somente leitura): {realBefore.Count} arquivo(s) em {realDir}");

        var mymc = new MyMcRunner(mymcPath);
        Check("mymc.exe encontrado", mymc.Exists, mymcPath);

        // ---------- Material de teste ----------
        string src = Path.Combine(work, "src.ps2");
        var fmt = await mymc.RunAsync(src, "format");
        Check("mymc formata um cartão novo", fmt.ExitCode == 0 && File.Exists(src), fmt.Output.Trim());
        await mymc.RunAsync(src, "mkdir", "BESLES-TESTSAVE");
        File.WriteAllBytes(Path.Combine(work, "icon.sys"), Random(200, 1));
        File.WriteAllBytes(Path.Combine(work, "data.bin"), Random(10240, 2));
        var add = await mymc.RunAsync(src, "add", "-d", "BESLES-TESTSAVE", Path.Combine(work, "icon.sys"), Path.Combine(work, "data.bin"));
        Check("cria um save de teste dentro do cartão", add.ExitCode == 0, add.Output.Trim());
        await mymc.RunAsync(src, "export", "-d", work, "BESLES-TESTSAVE");
        await mymc.RunAsync(src, "export", "-m", "-d", work, "BESLES-TESTSAVE");
        string psu = Path.Combine(work, "BESLES-TESTSAVE.psu");
        string max = Path.Combine(work, "BESLES-TESTSAVE.max");
        Check("gera save .psu", File.Exists(psu), psu);
        Check("gera save .max", File.Exists(max), max);

        string blank = Path.Combine(work, "blank.ps2");
        File.Copy(src, blank);
        // Cartão vazio (sem o save) para importar.
        await mymc.RunAsync(blank, "delete", "/BESLES-TESTSAVE");

        string? realCopy = null;
        string? realSource = Directory.Exists(realDir) ? Directory.GetFiles(realDir, "*.ps2").OrderBy(f => f).FirstOrDefault() : null;
        if (realSource is not null)
        {
            realCopy = Path.Combine(work, "real-copy.ps2");
            File.Copy(realSource, realCopy);
        }

        // ---------- Formatos ----------
        foreach (var ext in new[] { ".max", ".psu", ".cbs", ".sps", ".psv", ".PSU", ".Max" })
            Check($"formato {ext} suportado", SaveFormats.IsSupported("x" + ext));
        Check("formato .zip rejeitado", !SaveFormats.IsSupported("x.zip"));
        Check("formato .ps2 não é save", !SaveFormats.IsSupported("x.ps2"));

        // ---------- Validação de Memory Card ----------
        var vb = MemoryCardValidator.Validate(blank);
        Check("cartão formatado é válido (8 MB, com ECC)", vb.IsValid && vb.CapacityMb == 8 && vb.HasEcc, vb.Message);
        if (realCopy is not null)
        {
            var vr = MemoryCardValidator.Validate(realCopy);
            Check("cópia de cartão real do PCSX2 é válida", vr.IsValid, vr.Message);
        }

        string garbage = Path.Combine(work, "lixo.ps2");
        File.WriteAllBytes(garbage, Random(20000, 3));
        var vg = MemoryCardValidator.Validate(garbage);
        Check("arquivo aleatório .ps2 é rejeitado (assinatura)", !vg.IsValid && vg.Message.Contains("assinatura"), vg.Message);

        string tiny = Path.Combine(work, "pequeno.ps2");
        File.WriteAllBytes(tiny, new byte[10]);
        Check("arquivo minúsculo é rejeitado", !MemoryCardValidator.Validate(tiny).IsValid);

        string truncated = Path.Combine(work, "truncado.ps2");
        File.WriteAllBytes(truncated, File.ReadAllBytes(blank).Take(1_000_000).ToArray());
        var vt = MemoryCardValidator.Validate(truncated);
        Check("cartão truncado é rejeitado (tamanho)", !vt.IsValid && vt.Message.Contains("tamanho"), vt.Message);

        string wrongExt = Path.Combine(work, "cartao.bin");
        File.Copy(blank, wrongExt);
        Check("extensão diferente de .ps2 é rejeitada", !MemoryCardValidator.Validate(wrongExt).IsValid);
        Check("arquivo inexistente é rejeitado", !MemoryCardValidator.Validate(Path.Combine(work, "nao-existe.ps2")).IsValid);

        string readOnly = Path.Combine(work, "somente-leitura.ps2");
        File.Copy(blank, readOnly);
        File.SetAttributes(readOnly, FileAttributes.ReadOnly);
        Check("cartão somente leitura é rejeitado", !MemoryCardValidator.Validate(readOnly).IsValid);
        File.SetAttributes(readOnly, FileAttributes.Normal);

        string locked = Path.Combine(work, "bloqueado.ps2");
        File.Copy(blank, locked);
        using (new FileStream(locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            Check("cartão bloqueado por outro processo não derruba o programa", !MemoryCardValidator.Validate(locked).IsValid);

        string noEcc = Path.Combine(work, "sem-ecc.ps2");
        {
            var all = File.ReadAllBytes(blank);
            var plain = new byte[all.Length / 528 * 512];
            for (int p = 0; p < all.Length / 528; p++) Buffer.BlockCopy(all, p * 528, plain, p * 512, 512);
            File.WriteAllBytes(noEcc, plain);
            var vn = MemoryCardValidator.Validate(noEcc);
            Check("cartão sem ECC (8.388.608 bytes) é válido", vn.IsValid && !vn.HasEcc && vn.CapacityMb == 8, vn.Message);
        }

        // O PCSX2 cria cartões novos como arquivos de 0xFF (sem formato) até que sejam formatados.
        string unformatted = Path.Combine(work, "nao-formatado.ps2");
        File.WriteAllBytes(unformatted, Enumerable.Repeat((byte)0xFF, 8650752).ToArray());
        var vu = MemoryCardValidator.Validate(unformatted);
        Check("cartão não formatado (tudo 0xFF) é rejeitado com aviso claro", !vu.IsValid && vu.Badge == "Não formatado" && vu.Message.Contains("formatado"), vu.Message);
        var mu = await mymc.RunAsync(unformatted, "import", psu);
        Check("o mymc também não consegue importar em cartão não formatado", mu.ExitCode != 0, mu.Output.Trim().Split('\n')[0]);
        foreach (var realCard in Directory.Exists(realDir) ? Directory.GetFiles(realDir, "*.ps2") : Array.Empty<string>())
        {
            var vrc = MemoryCardValidator.Validate(realCard);
            Console.WriteLine($"[info] cartão real {Path.GetFileName(realCard)}: {(vrc.IsValid ? "válido" : vrc.Badge)} - {vrc.Message}");
        }

        // ---------- Lista de destinos (detecção automática + manual, sem duplicados) ----------
        string autoDir = Path.Combine(work, "auto");
        Directory.CreateDirectory(autoDir);
        File.Copy(blank, Path.Combine(autoDir, "Mcd001.ps2"));
        File.Copy(realCopy ?? blank, Path.Combine(autoDir, "Mcd002.ps2"));
        File.Copy(garbage, Path.Combine(autoDir, "Quebrado.ps2"));
        File.Copy(blank, Path.Combine(autoDir, "Mcd001.ps2.backup-20260101-000000"));
        File.WriteAllText(Path.Combine(autoDir, "notas.txt"), "x");

        var catalog = new CardCatalog(autoDir);
        Check("detecção automática sem erro", catalog.RefreshAutomatic() is null);
        Check("detecta só arquivos .ps2 (ignora backups e outros)", catalog.Automatic.Count == 3,
            string.Join(", ", catalog.Automatic.Select(c => c.FileName)));
        Check("lista automática em ordem de nome", catalog.Automatic.Select(c => c.FileName).SequenceEqual(new[] { "Mcd001.ps2", "Mcd002.ps2", "Quebrado.ps2" }));
        Check("cartão inválido aparece marcado como inválido", !catalog.Automatic.First(c => c.FileName == "Quebrado.ps2").IsValid);

        string others = Path.Combine(work, "outra pasta");
        Directory.CreateDirectory(others);
        string manualCard = Path.Combine(others, "Meu Cartão.ps2");
        File.Copy(blank, manualCard);
        string manualFull = Path.GetFullPath(manualCard);

        var a1 = catalog.AddManual(manualCard);
        Check("adiciona cartão manual válido", a1.Outcome == AddOutcome.Added && catalog.Manual.Count == 1, a1.Message);
        Check("não duplica o mesmo caminho", catalog.AddManual(manualCard).Outcome == AddOutcome.AlreadyManual && catalog.Manual.Count == 1);
        Check("não duplica caminho com maiúsculas/minúsculas diferentes", catalog.AddManual(manualCard.ToUpperInvariant()).Outcome == AddOutcome.AlreadyManual && catalog.Manual.Count == 1);
        string roundabout = Path.Combine(others, "..", "outra pasta", "Meu Cartão.ps2");
        Check("não duplica caminho equivalente (..\\)", catalog.AddManual(roundabout).Outcome == AddOutcome.AlreadyManual && catalog.Manual.Count == 1);
        Check("cartão já detectado automaticamente não vai para a lista manual", catalog.AddManual(Path.Combine(autoDir, "Mcd001.ps2")).Outcome == AddOutcome.AlreadyAutomatic && catalog.Manual.Count == 1);
        Check("recusa arquivo que não é Memory Card", catalog.AddManual(garbage).Outcome == AddOutcome.Invalid && catalog.Manual.Count == 1);
        Check("recusa arquivo inexistente", catalog.AddManual(Path.Combine(work, "fantasma.ps2")).Outcome == AddOutcome.Invalid);
        Check("recusa caminho vazio", catalog.AddManual("").Outcome == AddOutcome.Invalid);
        Check("remove da lista sem apagar o arquivo", catalog.RemoveManual(a1.Entry!) && catalog.Manual.Count == 0 && File.Exists(manualCard));

        // ---------- Seletor (controle de interface) ----------
        // Controles do WinForms precisam de uma thread STA própria (a thread do teste é do pool).
        RunSta(() =>
        {
        string settingsPath = Path.Combine(work, "settings.json");
        var settings = AppSettings.Load(settingsPath);
        var catalog2 = new CardCatalog(autoDir);
        using (var picker = new CardPickerCard(catalog2, settings))
        {
            Check("modo automático seleciona o primeiro cartão válido", !picker.ManualMode && picker.SelectedCard?.FileName == "Mcd001.ps2");
            picker.AddManualCards(new[] { manualCard, manualCard });
            Check("seleção manual: adiciona uma vez, muda para o modo manual e seleciona", picker.ManualMode && catalog2.Manual.Count == 1 && picker.SelectedCard?.FullPath == manualFull, picker.SelectedCard?.FullPath);
            picker.AddManualCards(new[] { garbage });
            Check("seleção manual: arquivo inválido não entra na lista", catalog2.Manual.Count == 1);
            picker.AddManualCards(new[] { Path.Combine(autoDir, "Mcd002.ps2") });
            Check("seleção manual de cartão já detectado volta ao automático, sem duplicar", !picker.ManualMode && catalog2.Manual.Count == 1 && picker.SelectedCard?.FileName == "Mcd002.ps2");
            picker.SyncSettings();
            settings.Save();
        }
        var settings2 = AppSettings.Load(settingsPath);
        Check("preferências persistem os cartões manuais", settings2.ManualCards.Count == 1 && settings2.ManualCards[0] == manualFull);
        var catalog3 = new CardCatalog(autoDir);
        using (var picker2 = new CardPickerCard(catalog3, settings2))
        {
            Check("cartões manuais são restaurados ao reabrir", catalog3.Manual.Count == 1 && catalog3.Manual[0].FullPath == manualFull);
        }

                });

        // ---------- Importação (PSU) em CÓPIA de cartão real ----------
        string target = Path.Combine(work, "alvo.ps2");
        File.Copy(realCopy ?? blank, target);
        string shaBefore = Sha(target);
        var lsBefore = Lines((await mymc.RunAsync(target, "ls", "/")).Output);
        var tempBefore = TempLeftovers().Length;

        var service = new ImportService(mymc, () => false);
        var prog = new ListProgress();
        var res = await service.RunAsync(psu, target, prog, _ => Task.FromResult(false));
        Check("importa .psu com sucesso", res.Outcome == ImportOutcome.Success, res.Message);
        Check("backup foi criado e é idêntico ao cartão original", res.BackupPath is not null && File.Exists(res.BackupPath) && Sha(res.BackupPath) == shaBefore);
        Check("nome do backup segue o padrão .backup-AAAAMMDD-HHMMSS", res.BackupPath is not null && System.Text.RegularExpressions.Regex.IsMatch(res.BackupPath, @"\.backup-\d{8}-\d{6}$"));
        Check("cartão de destino foi atualizado", Sha(target) != shaBefore);
        var lsAfter = Lines((await mymc.RunAsync(target, "ls", "/")).Output);
        Check("save importado aparece no cartão", lsAfter.Any(l => l.Contains("BESLES-TESTSAVE")));
        // As entradas "." e ".." descrevem o próprio diretório raiz (contagem e data mudam ao adicionar um save).
        Check("saves que já existiam foram preservados", lsBefore.Where(l => !l.Contains("BESLES-TESTSAVE") && !l.EndsWith(" .") && !l.EndsWith(" ..")).All(lsAfter.Contains),
            string.Join(" | ", lsBefore.Where(l => !l.EndsWith(" .") && !l.EndsWith(" ..")).Select(l => l.Split(' ').Last())));
        Check("arquivo temporário foi apagado", TempLeftovers().Length == tempBefore);
        var order = prog.Items.Where(u => u.State == StepState.Running).Select(u => u.Step).Distinct().ToList();
        Check("etapas executadas na ordem esperada", order.SequenceEqual(new[] { ImportStep.Preflight, ImportStep.Backup, ImportStep.TempCopy, ImportStep.Import, ImportStep.Validate, ImportStep.Apply }));
        Check("todas as 6 etapas terminam concluídas", Enum.GetValues<ImportStep>().All(s => prog.Items.Last(u => u.Step == s).State == StepState.Done));

        // ---------- Save já existente: cancelar e substituir ----------
        await Task.Delay(1200);
        string shaAfterFirst = Sha(target);
        string? asked = null;
        var cancel = await service.RunAsync(psu, target, new ListProgress(), f => { asked = f; return Task.FromResult(false); });
        Check("detecta save existente e pergunta antes de substituir", asked == "BESLES-TESTSAVE", asked);
        Check("recusar a substituição cancela sem alterar o cartão", cancel.Outcome == ImportOutcome.Cancelled && Sha(target) == shaAfterFirst);
        Check("backup é mantido mesmo ao cancelar", cancel.BackupPath is not null && File.Exists(cancel.BackupPath));

        await Task.Delay(1200);
        var replace = await service.RunAsync(psu, target, new ListProgress(), _ => Task.FromResult(true));
        Check("aceitar a substituição conclui a importação", replace.Outcome == ImportOutcome.Success && replace.ReplacedFolder == "BESLES-TESTSAVE", replace.Message);
        var lsReplaced = (await mymc.RunAsync(target, "ls", "/")).Output;
        Check("save substituído aparece uma única vez", System.Text.RegularExpressions.Regex.Matches(lsReplaced, "BESLES-TESTSAVE").Count == 1);

        // ---------- Outros formatos e falhas ----------
        string target2 = Path.Combine(work, "alvo-max.ps2");
        File.Copy(blank, target2);
        await Task.Delay(1100);
        var resMax = await service.RunAsync(max, target2, new ListProgress(), _ => Task.FromResult(false));
        Check("importa .max com sucesso", resMax.Outcome == ImportOutcome.Success, resMax.Message);
        Check(".max importado aparece no cartão", (await mymc.RunAsync(target2, "ls", "/")).Output.Contains("BESLES-TESTSAVE"));

        string bad = Path.Combine(work, "corrompido.psu");
        File.WriteAllBytes(bad, Random(5000, 9));
        string target3 = Path.Combine(work, "alvo-falha.ps2");
        File.Copy(blank, target3);
        string sha3 = Sha(target3);
        var resBad = await service.RunAsync(bad, target3, new ListProgress(), _ => Task.FromResult(false));
        Check("save corrompido falha na importação", resBad.Outcome == ImportOutcome.Failed && resBad.FailedStep == ImportStep.Import, resBad.Message.Split('\n')[0]);
        Check("falha não altera o cartão original", Sha(target3) == sha3 && !resBad.OriginalMayBeAffected);
        Check("backup da tentativa que falhou existe e é fiel", resBad.BackupPath is not null && Sha(resBad.BackupPath) == sha3);
        Check("nenhum temporário sobra após a falha", TempLeftovers().Length == tempBefore);

        // ---------- Proteção do PCSX2 ----------
        string target4 = Path.Combine(work, "alvo-pcsx2.ps2");
        File.Copy(blank, target4);
        string sha4 = Sha(target4);
        var guarded = new ImportService(mymc, () => true);
        var issue = guarded.Validate(psu, target4);
        Check("PCSX2 aberto bloqueia a importação (texto original)", issue?.Title == "PCSX2 aberto" && issue.Message == "Feche o PCSX2 antes de importar o save.");
        var blocked = await guarded.RunAsync(psu, target4, new ListProgress(), _ => Task.FromResult(true));
        Check("com PCSX2 aberto nada é criado nem alterado", blocked.Outcome == ImportOutcome.Failed && blocked.BackupPath is null && Backups(target4).Length == 0 && Sha(target4) == sha4);

        int calls = 0;
        var late = new ImportService(mymc, () => ++calls >= 2);
        await Task.Delay(1100);
        var lateRes = await late.RunAsync(psu, target4, new ListProgress(), _ => Task.FromResult(true));
        Check("PCSX2 aberto durante a importação aborta antes de gravar no cartão", lateRes.Outcome == ImportOutcome.Failed && lateRes.FailedStep == ImportStep.Apply && Sha(target4) == sha4 && !lateRes.OriginalMayBeAffected, lateRes.Message);

        // ---------- Validações de entrada ----------
        string zip = Path.Combine(work, "save.zip");
        File.WriteAllBytes(zip, new byte[10]);
        Check("formato não suportado é recusado", service.Validate(zip, target4)?.Title == "Formato não suportado");
        Check("sem arquivo de save é recusado", service.Validate(Path.Combine(work, "x.psu"), target4)?.Title == "Arquivo de save");
        Check("sem cartão é recusado", service.Validate(psu, Path.Combine(work, "x.ps2"))?.Title == "Memory Card");
        var noTool = new ImportService(new MyMcRunner(Path.Combine(work, "nao-existe.exe")), () => false);
        Check("mymc.exe ausente é informado", noTool.Validate(psu, target4)?.Title == "Componente ausente");

        // ---------- Cartões reais intactos ----------
        var realAfter = Directory.Exists(realDir)
            ? Directory.GetFiles(realDir).OrderBy(f => f).Select(f => (f, new FileInfo(f).Length, Sha(f))).ToList()
            : new List<(string, long, string)>();
        Check("cartões reais do PCSX2 não foram tocados (mesmos arquivos, tamanhos e hashes)", realBefore.SequenceEqual(realAfter));

        Console.WriteLine();
        Console.WriteLine($"Resultado: {_pass} passaram, {_fail} falharam.");
        return _fail == 0 ? 0 : 1;
    }
}
