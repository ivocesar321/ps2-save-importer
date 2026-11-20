namespace Ps2SaveImporter.Core;

internal enum CardSource { Automatic, Manual }

/// <summary>Um Memory Card que pode ser usado como destino.</summary>
internal sealed class CardEntry
{
    public CardEntry(string fullPath, CardSource source, CardValidation validation)
    {
        FullPath = fullPath;
        Source = source;
        Validation = validation;
    }

    public string FullPath { get; }
    public CardSource Source { get; }
    public CardValidation Validation { get; }
    public bool IsValid => Validation.IsValid;
    public string FileName => Path.GetFileName(FullPath);
    public string Folder => Path.GetDirectoryName(FullPath) ?? string.Empty;

    public static CardEntry Create(string fullPath, CardSource source) =>
        new(fullPath, source, MemoryCardValidator.Validate(fullPath));
}

internal enum AddOutcome { Added, AlreadyManual, AlreadyAutomatic, Invalid }

internal readonly record struct AddResult(AddOutcome Outcome, CardEntry? Entry, string Message);

/// <summary>
/// Lista de destinos: cartões detectados automaticamente na pasta padrão do PCSX2
/// e cartões adicionados manualmente. Nunca contém o mesmo arquivo duas vezes.
/// </summary>
internal sealed class CardCatalog
{
    private static readonly StringComparer PathComparer = StringComparer.OrdinalIgnoreCase;

    private readonly List<CardEntry> _automatic = new();
    private readonly List<CardEntry> _manual = new();

    public static string DefaultDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PCSX2", "memcards");

    public CardCatalog(string? automaticDirectory = null) =>
        AutomaticDirectory = automaticDirectory ?? DefaultDirectory;

    public string AutomaticDirectory { get; }
    public IReadOnlyList<CardEntry> Automatic => _automatic;
    public IReadOnlyList<CardEntry> Manual => _manual;

    /// <summary>Detecção automática (mesmo comportamento original). Retorna uma mensagem de erro, se houver.</summary>
    public string? RefreshAutomatic()
    {
        _automatic.Clear();
        try
        {
            Directory.CreateDirectory(AutomaticDirectory);
            foreach (var p in Directory.GetFiles(AutomaticDirectory, "*.ps2").OrderBy(Path.GetFileName))
            {
                if (string.Equals(Path.GetExtension(p), ".ps2", StringComparison.OrdinalIgnoreCase))
                    _automatic.Add(CardEntry.Create(p, CardSource.Automatic));
            }
            return null;
        }
        catch (Exception e)
        {
            return e.Message;
        }
    }

    /// <summary>Adiciona um cartão escolhido manualmente, evitando duplicados e validando o arquivo.</summary>
    public AddResult AddManual(string path)
    {
        string full;
        try { full = Path.GetFullPath(path); }
        catch (Exception) { return new(AddOutcome.Invalid, null, "O caminho informado não é válido."); }

        string key = Key(full);

        var inAuto = _automatic.FirstOrDefault(c => PathComparer.Equals(Key(c.FullPath), key));
        if (inAuto is not null)
            return new(AddOutcome.AlreadyAutomatic, inAuto, "Este cartão já está na lista de detectados automaticamente.");

        var inManual = _manual.FirstOrDefault(c => PathComparer.Equals(Key(c.FullPath), key));
        if (inManual is not null)
            return new(AddOutcome.AlreadyManual, inManual, "Este cartão já foi adicionado.");

        var entry = CardEntry.Create(full, CardSource.Manual);
        if (!entry.IsValid)
            return new(AddOutcome.Invalid, entry, entry.Validation.Message);

        _manual.Add(entry);
        return new(AddOutcome.Added, entry, "Cartão adicionado à lista.");
    }

    public bool RemoveManual(CardEntry entry) => _manual.Remove(entry);

    /// <summary>Chave de comparação: caminho completo normalizado (resolve "..", nomes curtos 8.3 e links simbólicos).</summary>
    private static string Key(string path)
    {
        string full;
        try { full = Path.GetFullPath(path); }
        catch { full = path; }

        try
        {
            if (new FileInfo(full).ResolveLinkTarget(true) is { } target) full = target.FullName;
        }
        catch
        {
            // Sem permissão para resolver o link: usa o caminho normalizado.
        }

        return full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
