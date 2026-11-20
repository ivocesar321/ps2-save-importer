using System.Text.RegularExpressions;

namespace Ps2SaveImporter.Core;

internal enum ImportStep { Preflight, Backup, TempCopy, Import, Validate, Apply }
internal enum StepState { Pending, Running, Done, Failed, Skipped }
internal enum ImportOutcome { Success, Cancelled, Failed }

internal readonly record struct StepUpdate(ImportStep Step, StepState State, string? Detail = null);
internal sealed record RequestIssue(string Title, string Message);
internal sealed record ImportResult(
    ImportOutcome Outcome,
    string Message,
    string? BackupPath,
    string? ReplacedFolder,
    ImportStep? FailedStep,
    bool OriginalMayBeAffected = false);

internal sealed class ImportException : Exception
{
    public ImportException(string message) : base(message) { }
}

/// <summary>
/// Pipeline de importação. A sequência é a mesma do programa original:
/// backup → cópia temporária → importar na cópia → (confirmar substituição) → validar → só então atualizar o cartão original.
/// </summary>
internal sealed class ImportService
{
    public static readonly string[] StepTitles =
    {
        "Verificar arquivos e PCSX2",
        "Criar backup do Memory Card",
        "Preparar cópia temporária",
        "Importar o save",
        "Validar o resultado",
        "Atualizar o Memory Card",
    };

    private readonly MyMcRunner _mymc;
    private readonly Func<bool> _pcsx2Running;

    public ImportService(MyMcRunner mymc, Func<bool>? pcsx2Running = null)
    {
        _mymc = mymc;
        _pcsx2Running = pcsx2Running ?? Pcsx2Guard.IsRunning;
    }

    /// <summary>Mesmas verificações (e textos) do programa original, antes de pedir confirmação.</summary>
    public RequestIssue? Validate(string savePath, string cardPath)
    {
        bool folderSave = SaveFormats.IsFolderSave(savePath);
        if (!folderSave && !File.Exists(savePath))
            return new("Arquivo de save", "Escolha um arquivo de save ou uma pasta válida do PCSX2.");
        if (!File.Exists(cardPath))
            return new("Memory Card", "Selecione um cartão de memória válido.");
        if (_pcsx2Running())
            return new("PCSX2 aberto", "Feche o PCSX2 antes de importar o save.");
        if (!folderSave && !SaveFormats.IsSupported(savePath))
            return new("Formato não suportado", "Use MAX, PSU, CBS, SPS, PSV ou uma pasta de save válida do PCSX2.");
        if (!_mymc.Exists)
            return new("Componente ausente", "Componente mymc.exe não encontrado junto do programa.");
        return null;
    }

    public async Task<ImportResult> RunAsync(
        string savePath,
        string cardPath,
        IProgress<StepUpdate> progress,
        Func<string, Task<bool>> confirmReplace)
    {
        string backup = cardPath + ".backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
        string temp = Path.Combine(Path.GetTempPath(), "PS2SaveImport-" + Guid.NewGuid().ToString("N") + ".ps2");
        var current = ImportStep.Preflight;
        string? replaced = null;
        bool backupMade = false;

        void Report(ImportStep step, StepState state, string? detail = null)
        {
            current = step;
            progress.Report(new StepUpdate(step, state, detail));
        }

        try
        {
            Report(ImportStep.Preflight, StepState.Running);
            var issue = Validate(savePath, cardPath);
            if (issue is not null) throw new ImportException(issue.Message);
            Report(ImportStep.Preflight, StepState.Done);

            Report(ImportStep.Backup, StepState.Running);
            await Task.Run(() => File.Copy(cardPath, backup, false));
            backupMade = true;
            Report(ImportStep.Backup, StepState.Done, Path.GetFileName(backup));

            Report(ImportStep.TempCopy, StepState.Running);
            await Task.Run(() => File.Copy(cardPath, temp, false));
            Report(ImportStep.TempCopy, StepState.Done);

            Report(ImportStep.Import, StepState.Running, "Importando na cópia…");
            MyMcResult imported;
            if (SaveFormats.IsFolderSave(savePath))
            {
                string folderName = Path.GetFileName(Path.GetFullPath(savePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (string.IsNullOrWhiteSpace(folderName) || folderName.Contains('/') || folderName.Contains('\\'))
                    throw new ImportException("Nome da pasta de save inválido.");

                var listing = await _mymc.RunAsync(temp, "ls", "/");
                if (listing.ExitCode != 0) throw new ImportException("Não foi possível verificar o conteúdo do cartão temporário:\r\n" + listing.Output);
                bool exists = listing.Output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                    .Any(line => line.TrimEnd().EndsWith(" " + folderName, StringComparison.OrdinalIgnoreCase));
                if (exists)
                {
                    replaced = folderName;
                    if (!await confirmReplace(folderName))
                    {
                        progress.Report(new StepUpdate(ImportStep.Import, StepState.Skipped, "Cancelado"));
                        return new ImportResult(ImportOutcome.Cancelled, "Importação cancelada. Cartão original intacto.", backup, folderName, null);
                    }
                    var deletedFolder = await _mymc.RunAsync(temp, "delete", "/" + folderName);
                    if (deletedFolder.ExitCode != 0) throw new ImportException("Não foi possível remover o save antigo na cópia temporária:\r\n" + deletedFolder.Output);
                }

                var created = await _mymc.RunAsync(temp, "mkdir", "/" + folderName);
                if (created.ExitCode != 0) throw new ImportException("Não foi possível criar a pasta do save no cartão:\r\n" + created.Output);

                string[] files = Directory.GetFiles(savePath, "*", SearchOption.TopDirectoryOnly);
                if (files.Length == 0) throw new ImportException("A pasta do save não contém arquivos.");
                var addArgs = new List<string> { "add", "-d", "/" + folderName };
                addArgs.AddRange(files);
                imported = await _mymc.RunAsync(temp, addArgs.ToArray());
                if (imported.ExitCode != 0) throw new ImportException("Não foi possível adicionar os arquivos da pasta ao cartão:\r\n" + imported.Output);
                imported = new MyMcResult(0, "Importing folder save to " + folderName);
            }
            else
            {
                imported = await _mymc.RunAsync(temp, "import", savePath);
            }
            if (imported.ExitCode != 0)
            {
                var match = Regex.Match(imported.Output, @"(?m)^/([^:\r\n]+): directory exists");
                if (!match.Success)
                    throw new ImportException("Não foi possível importar este arquivo:\r\n" + imported.Output);

                string folder = match.Groups[1].Value;
                replaced = folder;
                Report(ImportStep.Import, StepState.Running, "Save existente: " + folder);

                if (!await confirmReplace(folder))
                {
                    progress.Report(new StepUpdate(ImportStep.Import, StepState.Skipped, "Cancelado"));
                    return new ImportResult(ImportOutcome.Cancelled,
                        "Importação cancelada. Cartão original intacto.", backup, folder, null);
                }

                Report(ImportStep.Import, StepState.Running, "Removendo o save antigo na cópia…");
                var deleted = await _mymc.RunAsync(temp, "delete", "/" + folder);
                if (deleted.ExitCode != 0)
                    throw new ImportException("Não foi possível remover o save antigo na cópia temporária:\r\n" + deleted.Output);

                Report(ImportStep.Import, StepState.Running, "Importando novamente…");
                imported = await _mymc.RunAsync(temp, "import", savePath);
                if (imported.ExitCode != 0)
                    throw new ImportException("A segunda tentativa de importação falhou:\r\n" + imported.Output);
            }
            Report(ImportStep.Import, StepState.Done, replaced is null ? null : "Save substituído");

            Report(ImportStep.Validate, StepState.Running);
            var root = await _mymc.RunAsync(temp, "ls", "/");
            if (root.ExitCode != 0)
                throw new ImportException("Não foi possível validar o cartão temporário:\r\n" + root.Output);

            // O comando de importação informa o diretório de destino exato.
            var destination = Regex.Match(imported.Output, @"Importing .+ to ([^\r\n]+)");
            if (destination.Success && !root.Output.Contains(destination.Groups[1].Value, StringComparison.OrdinalIgnoreCase))
                throw new ImportException("O save não apareceu na listagem do cartão após a importação.");
            Report(ImportStep.Validate, StepState.Done);

            Report(ImportStep.Apply, StepState.Running, "Atualizando o cartão original…");
            if (_pcsx2Running())
                throw new ImportException("O PCSX2 foi aberto durante a importação. O cartão original não foi alterado.");
            await Task.Run(() => File.Copy(temp, cardPath, true));
            Report(ImportStep.Apply, StepState.Done);

            return new ImportResult(ImportOutcome.Success, "Save importado com sucesso.", backup, replaced, null);
        }
        catch (Exception e)
        {
            progress.Report(new StepUpdate(current, StepState.Failed));
            // Só a etapa final grava no cartão original; um erro de E/S ali pode deixá-lo incompleto.
            bool mayBeAffected = current == ImportStep.Apply && e is not ImportException;
            return new ImportResult(ImportOutcome.Failed, e.Message, backupMade ? backup : null, replaced, current, mayBeAffected);
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch { /* arquivo temporário: melhor esforço */ }
        }
    }
}
